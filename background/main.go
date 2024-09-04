package main

import (
	"context"
	"flag"
	"fmt"
	"log"
	"math/rand"
	"net"
)

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

	// Gossip 형성
	host, gossip := CreateHostAndSetProtocol(ctx, group, name, friends, conn)

	// 무한 대기하며 폼과 통신
	MessageHandler(ctx, host, gossip, conn)
}

// 실행 폼으로부터 플래그를 받는 함수, (그룹, 이름, 포트) 순서
func CreateFlag() (string, string, int) {
	group := flag.String("group", "default", "group(mdns rendezvous)")
	name := flag.String("name", fmt.Sprintf("BT-%d", rand.Int()), "user nick name")
	port := flag.Int("port", 12000, "local port")

	flag.Parse()

	return *group, *name, *port
}
