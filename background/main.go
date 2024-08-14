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

type peerName struct {
	ma   multiaddr.Multiaddr
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
	rz := flag.String("group", "default", "group(mdns rendezvous)")
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

	_, err = pubsub.NewGossipSub(ctx, host)

	if err != nil {
		log.Fatalln(err)
	}

	peerChan := InitMDNS(host, rendezvous)

	go func() {
		host.SetStreamHandler(protocol.ID(NameExchangeProtocol), func(stream network.Stream) {
			buf := make([]byte, 1024)
			stream.Read(buf)

			fs = append(fs, peerName{
				stream.Conn().RemoteMultiaddr(),
				string(buf),
			})

			log.Println(stream.Conn().RemoteMultiaddr().String(), string(buf))
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

	err := ser.Start()

	if err != nil {
		log.Fatalln(err)
	}

	return n.PeerChan
}
