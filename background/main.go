package main

import (
	"bytes"
	"context"
	"encoding/json"
	"flag"
	"fmt"
	"io"
	"log"
	"math/rand"
	"net"
	"strings"
	"time"

	"github.com/libp2p/go-libp2p"
	pubsub "github.com/libp2p/go-libp2p-pubsub"
	"github.com/libp2p/go-libp2p/core/host"
	"github.com/libp2p/go-libp2p/core/network"
	"github.com/libp2p/go-libp2p/core/peer"
	"github.com/libp2p/go-libp2p/core/protocol"
	"github.com/libp2p/go-libp2p/p2p/discovery/mdns"
	"github.com/libp2p/go-libp2p/p2p/muxer/yamux"
	"github.com/libp2p/go-libp2p/p2p/security/noise"
	"github.com/libp2p/go-libp2p/p2p/transport/tcp"
	"github.com/multiformats/go-multiaddr"
)

// 주위 피어를 찾았을 때 닉네임을 전달하는 프로토콜
const NameExchangeProtocol = "/blue-telephone/name-exchange/1.0.0"

// 메인 함수
func main() {
	// 플래그 전달 받음
	group, name, port := CreateFlag()

	// 폼으로 다이얼
	// 이 시점에서 폼은 리스너가 준비됨
	conn, err := net.Dial("tcp4", fmt.Sprintf("127.0.0.1:%d", port))

	// 즉 아래는 폼이 문제가 없다면 발생하지 않음
	if err != nil {
		log.Fatalln(err)
	}

	// 메인 context 형성
	ctx := context.Background()
	// 발견한 친구 목록
	friends := []PeerName{}
	// 구독한 토픽 목록
	topics := []Topic{}

	// Gossip 형성
	_, gossip := CreateHostAndExchangeInfo(ctx, group, name, friends, conn)

	// 무한 대기하며 폼과 통신
	for {
		buf := make([]byte, 1024)

		_, err := io.ReadFull(conn, buf)

		// 메시지 못읽겠으면 오류 전달
		if err != nil {
			WritePacket(conn, DeniedError, []string{err.Error()})
			log.Println(err)
			continue
		}

		var packet Packet

		err = json.Unmarshal([]byte(strings.TrimRight(string(buf), "\x00")), &packet)

		// 받은 패킷이 json으로 안풀리면 오류 전달
		if err != nil {
			WritePacket(conn, DeniedError, []string{err.Error()})
			log.Println(err)
			continue
		}

		// 메시지 코드에 따라 동작
		switch packet.MsgCode {
		// Gossip 참여 요청시 참여 및 구독 처리
		case JoinGossip:
			topic, err := gossip.Join(packet.Msg[0])

			if err != nil {
				WritePacket(conn, DeniedError, []string{err.Error()})
				log.Println(err)
				continue
			}
			sub, err := topic.Subscribe()

			if err != nil {
				WritePacket(conn, DeniedError, []string{err.Error()})
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
							WritePacket(conn, DeniedError, []string{err.Error()})
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
						WritePacket(conn, DeniedError, []string{err.Error()})
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
						WritePacket(conn, DeniedError, []string{err.Error()})
						log.Println(err)
						continue
					}

					// 성공했다고 전달
					WritePacket(conn, Success, []string{"Success publish topic"})
					log.Println("Success publish topic")

					break
				}
			}
		}
	}
}

// 실행 폼으로부터 플래그를 받는 함수, (그룹, 이름, 포트) 순서
func CreateFlag() (string, string, int) {
	group := flag.String("group", "default", "group(mdns rendezvous)")
	name := flag.String("name", fmt.Sprintf("BT-%d", rand.Int()), "user nick name")
	port := flag.Int("port", 12000, "local port")

	flag.Parse()

	return *group, *name, *port
}

// 호스트를 생성하고, 주위 피어를 찾거나 닉네임을 전파하는 역할을 하는 함수
func CreateHostAndExchangeInfo(ctx context.Context, rendezvous string, name string, friends []PeerName, conn net.Conn) (host.Host, *pubsub.PubSub) {
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

	// MDNS 프로토콜 설정
	peerChan := InitMDNS(host, rendezvous, conn)

	// 스레드 분할 시작
	// 피어 찾기 + 피어에게 이름 알려주기
	go func() {
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
				WritePacket(conn, DeniedError, []string{err.Error()})
				log.Println(err)
				continue
			}

			_, err = stream.Write([]byte(name))

			if err != nil {
				WritePacket(conn, DeniedError, []string{err.Error()})
				log.Println(err)
				continue
			}

			err = stream.Close()

			if err != nil {
				WritePacket(conn, DeniedError, []string{err.Error()})
				log.Println(err)
				continue
			}
		}
	}()

	// 피어 삭제 스레드
	go func() {
		for {
			// 10초마다
			time.Sleep(10 * time.Second)

			// 상ㄷ애가 대답이 없다면 친구 목록에서 제거하고 삭제되었음을 폼으로 전송
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
	}()

	return host, ps
}

// 폼으로 패킷을 전달하는 함수
func WritePacket(conn net.Conn, msgCode int, msg []string) {
	packet := &Packet{
		TS:      time.Now().String(),
		MsgCode: msgCode,
		Msg:     msg,
	}

	json, err := json.Marshal(packet)

	if err != nil {
		log.Fatalln(err)
	}

	buf := make([]byte, 1024)
	copy(buf, []byte(string(json)))

	_, err = conn.Write(buf)

	if err != nil {
		log.Fatalln(err)
	}
}

// 피어 이름 구조체
type PeerName struct {
	// libp2p에서의 정보
	info peer.AddrInfo
	// 피어가 자칭한 닉네임
	name string
}

// MDNS용 구조체
type DiscoveryNotifee struct {
	PeerChan chan peer.AddrInfo
}

// 패킷 구조체
type Packet struct {
	// 타임스탬프
	TS string
	// 메시지 코드
	MsgCode int
	// 메시지
	Msg []string
}

// 토픽 구조체
type Topic struct {
	// Gossipsub에서의 토픽
	topic *pubsub.Topic
	// Gossipsub에서의 구독 정보
	sub *pubsub.Subscription
	// 해당 토픽을 제어하기 위한 context
	ctx context.Context
	// 해당 토픽 context의 취소 명령
	cancel context.CancelFunc
}

// MDNS용 피어 발견 제어 핸들러
func (n *DiscoveryNotifee) HandlePeerFound(pi peer.AddrInfo) {
	n.PeerChan <- pi
}

// MDNS 형성
func InitMDNS(peerhost host.Host, rendezvous string, conn net.Conn) chan peer.AddrInfo {
	n := &DiscoveryNotifee{}
	n.PeerChan = make(chan peer.AddrInfo)

	ser := mdns.NewMdnsService(peerhost, rendezvous, n)

	err := ser.Start()

	if err != nil {
		WritePacket(conn, PanicError, []string{err.Error()})
		log.Fatalln(err)
	}

	return n.PeerChan
}
