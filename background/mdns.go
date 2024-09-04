package main

import (
	"log"
	"net"

	"github.com/libp2p/go-libp2p/core/host"
	"github.com/libp2p/go-libp2p/core/peer"
	"github.com/libp2p/go-libp2p/p2p/discovery/mdns"
)

// MDNS용 구조체
type DiscoveryNotifee struct {
	PeerChan chan peer.AddrInfo
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
