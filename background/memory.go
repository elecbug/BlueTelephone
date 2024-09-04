package main

import (
	"context"

	pubsub "github.com/libp2p/go-libp2p-pubsub"
	"github.com/libp2p/go-libp2p/core/peer"
)

// 피어 이름 구조체
type PeerName struct {
	// libp2p에서의 정보
	info peer.AddrInfo
	// 피어가 자칭한 닉네임
	name string
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
