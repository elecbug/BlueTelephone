package main

import (
	"context"
	"flag"
	"fmt"
	"log"
	"math/rand"
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

const NameExchangeProtocol = "/blue-telephone/name-exchange/1.0.0"

func main() {
	// conn, err := net.Dial("tcp4", "localhost:12000")

	// if err != nil {
	// 	log.Fatalln(err)
	// }
	// defer conn.Close()

	ctx := context.Background()
	friends := []peerName{}

	group, name := CreateFlag()
	_, _ = CreateHostAndExchangeInfo(ctx, group, name, friends)

	select {}
}

func CreateFlag() (string, string) {
	group := flag.String("group", "default", "group(mdns rendezvous)")
	name := flag.String("name", fmt.Sprintf("BT-%d", rand.Int()), "user nick name")

	flag.Parse()

	return *group, *name
}

func CreateHostAndExchangeInfo(ctx context.Context, rendezvous string, name string, friends []peerName) (host.Host, *pubsub.PubSub) {
	host, err := libp2p.New(
		libp2p.ListenAddrStrings("/ip4/0.0.0.0/tcp/0"),
		libp2p.Security(noise.ID, noise.New),
		libp2p.Muxer(yamux.ID, yamux.DefaultTransport),
		libp2p.Transport(tcp.NewTCPTransport),
	)

	if err != nil {
		log.Fatalln(err)
	} else {
		log.Println("Self:", host.Addrs(), host.ID())
	}

	ps, err := pubsub.NewGossipSub(ctx, host)

	if err != nil {
		log.Fatalln(err)
	}

	peerChan := InitMDNS(host, rendezvous)

	go func() {
		host.SetStreamHandler(protocol.ID(NameExchangeProtocol), func(stream network.Stream) {
			buf := make([]byte, 1024)
			stream.Read(buf)

			friends = append(friends, peerName{
				peer.AddrInfo{
					ID:    stream.Conn().RemotePeer(),
					Addrs: []multiaddr.Multiaddr{stream.Conn().RemoteMultiaddr()},
				},
				string(buf),
			})

			log.Println("Add:", stream.Conn().RemoteMultiaddr().String(), string(buf))
		})

		for {
			peer := <-peerChan

			err = host.Connect(ctx, peer)

			if err != nil {
				log.Println(err)
				continue
			}

			stream, err := host.NewStream(ctx, peer.ID, protocol.ID(NameExchangeProtocol))

			if err != nil {
				log.Println(err)
				continue
			}

			_, err = stream.Write([]byte(name))

			if err != nil {
				log.Println(err)
				continue
			}

			err = stream.Close()

			if err != nil {
				log.Println(err)
				continue
			}
		}
	}()

	go func() {
		for {
			time.Sleep(10 * time.Second)

			for i, v := range friends {
				err := host.Connect(ctx, v.info)

				if err != nil {
					log.Println("Remove:", v.info.ID)
					friends = append(friends[:i], friends[i+1:]...)

					continue
				}
			}
		}
	}()

	return host, ps
}

type peerName struct {
	info peer.AddrInfo
	name string
}

type discoveryNotifee struct {
	PeerChan chan peer.AddrInfo
}

func (n *discoveryNotifee) HandlePeerFound(pi peer.AddrInfo) {
	n.PeerChan <- pi
}

func InitMDNS(peerhost host.Host, rendezvous string) chan peer.AddrInfo {
	n := &discoveryNotifee{}
	n.PeerChan = make(chan peer.AddrInfo)

	ser := mdns.NewMdnsService(peerhost, rendezvous, n)

	err := ser.Start()

	if err != nil {
		log.Fatalln(err)
	}

	return n.PeerChan
}
