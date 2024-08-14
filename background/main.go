package main

import (
	"context"
	"flag"
	"fmt"
	"log"
	"math/rand"

	"github.com/libp2p/go-libp2p"
	pubsub "github.com/libp2p/go-libp2p-pubsub"
	"github.com/libp2p/go-libp2p/core/host"
	"github.com/libp2p/go-libp2p/core/peer"
	"github.com/libp2p/go-libp2p/p2p/discovery/mdns"
	"github.com/libp2p/go-libp2p/p2p/muxer/yamux"
	"github.com/libp2p/go-libp2p/p2p/security/noise"
	"github.com/libp2p/go-libp2p/p2p/transport/tcp"
)

const NameTopic = "/blue-telephone/welcome/my-name"

type peerName struct {
	id   peer.ID
	name string
}

func main() {
	ctx := context.Background()
	fs := []peerName{}

	rz, name := CreateFlag()
	_ = CreateHostAndPublishName(ctx, rz, name, fs)

	select {}
}

func CreateFlag() (string, string) {
	rz := flag.String("rz", "default", "rendezvous string")
	name := flag.String("name", fmt.Sprintf("BT-%d", rand.Int()), "user nick name")

	flag.Parse()

	return *rz, *name
}

func CreateHostAndPublishName(ctx context.Context, rendezvous string, name string, fs []peerName) host.Host {
	host, err := libp2p.New(
		libp2p.ListenAddrStrings("/ip4/0.0.0.0/tcp/0"),
		libp2p.Security(noise.ID, noise.New),
		libp2p.Muxer(yamux.ID, yamux.DefaultTransport),
		libp2p.Transport(tcp.NewTCPTransport),
	)

	if err != nil {
		log.Fatalln(err)
	} else {
		log.Println(host.Addrs(), host.ID())
	}

	ps, err := pubsub.NewGossipSub(ctx, host)

	if err != nil {
		log.Fatalln(err)
	}

	nameTopic, err := ps.Join(NameTopic)

	if err != nil {
		log.Fatalln(err)
	}

	nameSub, err := nameTopic.Subscribe()

	if err != nil {
		log.Fatalln(err)
	}

	go func() {
		for {
			msg, err := nameSub.Next(ctx)

			if err != nil {
				log.Println(err)
			}

			id, err := peer.IDFromBytes(msg.From)

			if err != nil {
				log.Fatalln(err)
			}

			if host.ID() == id {
				continue
			}

			fs = append(fs, peerName{
				id,
				string(msg.Data),
			})

			log.Println(id.String(), string(msg.Data))
		}
	}()

	peerChan := InitMDNS(host, rendezvous)

	go func() {
		for {
			peer := <-peerChan

			if peer.ID > host.ID() {
				log.Println("Found peer: ", peer, ", id is greater than us, wait for it to connect to us")
			} else {
				log.Println("Found peer: ", peer, ", connecting")

				err = host.Connect(ctx, peer)

				if err != nil {
					log.Println(err)
					continue
				}
			}

			err = nameTopic.Publish(ctx, []byte(name))

			if err != nil {
				log.Fatalln(err)
			}
		}
	}()

	return host
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

	if err := ser.Start(); err != nil {
		log.Fatalln(err)
	}
	return n.PeerChan
}
