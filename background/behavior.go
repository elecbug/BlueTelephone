package main

import (
	"bytes"
	"context"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net"
	"strings"
	"time"

	"github.com/libp2p/go-libp2p"
	pubsub "github.com/libp2p/go-libp2p-pubsub"
	"github.com/libp2p/go-libp2p/core/host"
	"github.com/libp2p/go-libp2p/core/network"
	"github.com/libp2p/go-libp2p/core/peer"
	"github.com/libp2p/go-libp2p/core/protocol"
	"github.com/libp2p/go-libp2p/p2p/muxer/yamux"
	"github.com/libp2p/go-libp2p/p2p/security/noise"
	"github.com/libp2p/go-libp2p/p2p/transport/tcp"
	"github.com/multiformats/go-multiaddr"
)

// 호스트를 생성하고, 프로토콜을 세팅하는 함수
func CreateHostAndSetProtocol(ctx context.Context, rendezvous string, name string, friends []PeerName, conn net.Conn) (host.Host, *pubsub.PubSub) {
	// 호스트 생성
	host, err := libp2p.New(
		libp2p.ListenAddrStrings("/ip4/0.0.0.0/tcp/0"),
		libp2p.Security(noise.ID, noise.New),
		libp2p.Transport(tcp.NewTCPTransport),
		libp2p.Muxer(yamux.ID, yamux.DefaultTransport),
	)

	if err != nil {
		WritePacket(conn, PanicError, []string{err.Error()})
		log.Fatalln(err)
	} else {
		// 에러가 없을 시 주소 정보를 폼으로 전송
		addrs := make([]string, len(host.Addrs()))

		for i, v := range host.Addrs() {
			addrs[i] = v.String()
		}

		WritePacket(conn, CreateHost, append(addrs, host.ID().String()))
		log.Println("Self:", host.Addrs(), host.ID())
	}

	// Gossipsub 생성
	ps, err := pubsub.NewGossipSub(ctx, host)

	if err != nil {
		WritePacket(conn, PanicError, []string{err.Error()})
		log.Fatalln(err)
	}

	// 스레드 분할 시작
	go FoundPeerAndExchangeInfo(ctx, host, conn, rendezvous, name, friends)
	go RemoveFailedPeer(ctx, host, conn, friends)

	return host, ps
}

// 활동하지 않는 피어를 삭제하는 함수
func RemoveFailedPeer(ctx context.Context, host host.Host, conn net.Conn, friends []PeerName) {
	for {
		// 10초마다
		time.Sleep(10 * time.Second)

		// 상대가 대답이 없다면 친구 목록에서 제거하고 삭제되었음을 폼으로 전송
		for i, v := range friends {
			err := host.Connect(ctx, v.info)

			if err != nil {
				WritePacket(conn, RemovePeer, []string{v.info.ID.String()})
				log.Println("Remove:", v.info.ID)

				friends = append(friends[:i], friends[i+1:]...)

				continue
			}
		}
	}
}

// 피어를 찾고 주위 피어에게 이름을 알려주는 함수
func FoundPeerAndExchangeInfo(ctx context.Context, host host.Host, conn net.Conn, rendezvous string, name string, friends []PeerName) {
	// MDNS 프로토콜 설정
	peerChan := InitMDNS(host, rendezvous, conn)

	// 이름 교환 프로토콜에서 메시지를 받을 시 해당 피어의 이름 + 정보를 폼으로 전달
	host.SetStreamHandler(protocol.ID(NameExchangeProtocol), func(stream network.Stream) {
		buf := make([]byte, 1024)
		stream.Read(buf)

		buf = bytes.Trim(buf, "\x00")

		friends = append(friends, PeerName{
			peer.AddrInfo{
				ID:    stream.Conn().RemotePeer(),
				Addrs: []multiaddr.Multiaddr{stream.Conn().RemoteMultiaddr()},
			},
			string(buf),
		})

		WritePacket(conn, FoundPeer, []string{stream.Conn().RemoteMultiaddr().String(), stream.Conn().RemotePeer().String(), string(buf)})
		log.Println("Add:", stream.Conn().RemoteMultiaddr().String(), stream.Conn().RemotePeer(), string(buf))
	})

	// 피어를 찾는다면
	for {
		peer := <-peerChan

		// 피어를 스토어에 저장
		// ** 절대 직접 커넥션하면 안됨 - noise handshake에서 오류가 발생 **
		host.Peerstore().AddAddrs(peer.ID, peer.Addrs, 1*time.Hour)

		// 스트림을 형성하고 자신의 이름을 상대에게 전송
		stream, err := host.NewStream(ctx, peer.ID, protocol.ID(NameExchangeProtocol))

		if err != nil {
			WritePacket(conn, GoodError, []string{err.Error()})
			log.Println(err)
			continue
		}

		_, err = stream.Write([]byte(name))

		if err != nil {
			WritePacket(conn, GoodError, []string{err.Error()})
			log.Println(err)
			continue
		}

		err = stream.Close()

		if err != nil {
			WritePacket(conn, GoodError, []string{err.Error()})
			log.Println(err)
			continue
		}
	}
}

// 폼과 통신하며 메시지 패킷을 핸들링하는 함수
func MessageHandler(ctx context.Context, host host.Host, gossip *pubsub.PubSub, conn net.Conn) {
	// 구독한 토픽 목록
	topics := []Topic{}

	for {
		buf := make([]byte, 1024)

		_, err := io.ReadFull(conn, buf)

		// 메시지 못읽겠으면 오류 전달
		if err != nil {
			WritePacket(conn, GoodError, []string{err.Error()})
			log.Println(err)
			continue
		}

		var packet Packet

		err = json.Unmarshal([]byte(strings.TrimRight(string(buf), "\x00")), &packet)

		// 받은 패킷이 json으로 안풀리면 오류 전달
		if err != nil {
			WritePacket(conn, GoodError, []string{err.Error()})
			log.Println(err)
			continue
		}

		// 메시지 코드에 따라 동작
		switch packet.MsgCode {
		// Gossip 참여 요청시 참여 및 구독 처리
		case JoinGossip:
			topic, err := gossip.Join(packet.Msg[0])

			if err != nil {
				WritePacket(conn, GoodError, []string{err.Error()})
				log.Println(err)
				continue
			}
			sub, err := topic.Subscribe()

			if err != nil {
				WritePacket(conn, GoodError, []string{err.Error()})
				log.Println(err)
				continue
			}

			// 해당 토픽을 위한 자식 context 형성
			topicCtx, cancelCtx := context.WithCancel(ctx)

			// 토픽 목록에 추가
			topics = append(topics, Topic{
				topic:  topic,
				sub:    sub,
				ctx:    topicCtx,
				cancel: cancelCtx,
			})

			// 해당 토픽만을 위한 별도의 스레드 동작
			// 토픽 취소할 때 함께 소멸
			go func() {
				for {
					// 만약 메시지를 받으면 폼으로 전달
					msg, err := sub.Next(topicCtx)

					// 실패시 오류 핸들링
					// 이긴 한데, 그 오류가 context canceled 즉, 토픽 구독 취소라면 Success 코드로 전송
					if err != nil {
						if err.Error() != "context canceled" {
							WritePacket(conn, GoodError, []string{err.Error()})
							log.Println(err)
						} else {
							WritePacket(conn, Success, []string{err.Error()})
							log.Println(err)

							break
						}
					} else {
						WritePacket(conn, GotGossip, []string{topic.String(), msg.ReceivedFrom.String(), string(msg.Data)})
						log.Println("got msg", string(msg.Data), "from", msg.ReceivedFrom.String(), "in topic", topic.String())
					}
				}
			}()

			// 토픽 참여 성공 메시지 전달
			WritePacket(conn, Success, []string{"Success joins topic"})
			log.Println("Success joins topic")

		// 토픽에서 나가고 싶다면
		case ExitGossip:
			// 토픽을 목록에서 찾아서 이탈
			for i, v := range topics {
				if v.topic.String() == packet.Msg[0] {
					v.cancel()
					v.sub.Cancel()
					err = v.topic.Close()

					// 이탈 실패시 발생
					// 아직까지 발생한적 없음
					if err != nil {
						WritePacket(conn, GoodError, []string{err.Error()})
						log.Println(err)
						continue
					}

					// 토픽 목록 정리
					topics = append(topics[:i], topics[i+1:]...)

					// 성공했다고 전달
					WritePacket(conn, Success, []string{"Success exits topic"})
					log.Println("Success exits topic")

					break
				}
			}

		// 메시지 전달 요청 시
		case Publish:
			// 토픽에서 찾아보고
			for _, v := range topics {
				// 해당 토픽에서
				if v.topic.String() == packet.Msg[0] {
					// 퍼블리시하고
					err = v.topic.Publish(ctx, []byte(packet.Msg[1]))

					// 실패 알림
					// 이것도 발생한 적 없음
					if err != nil {
						WritePacket(conn, GoodError, []string{err.Error()})
						log.Println(err)
						continue
					}

					// 성공했다고 전달
					WritePacket(conn, Success, []string{"Success publish topic"})
					log.Println("Success publish topic")

					break
				}
			}

		// 직접 피어 추가 요청시
		case PlzFindPeer:
			info, err := peer.AddrInfoFromString(packet.Msg[0])

			// 실패 알림
			if err != nil {
				WritePacket(conn, GoodError, []string{err.Error()})
				log.Println(err)
				continue
			}

			for _, v := range info.Addrs {
				host.Peerstore().AddAddr(info.ID, v, time.Hour)
			}

			// 성공했다고 전달
			msg := fmt.Sprintf("Success add peer (%s)", info.ID)

			WritePacket(conn, Success, []string{msg})
			log.Println(msg)
		}
	}
}
