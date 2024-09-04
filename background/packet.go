package main

import (
	"encoding/json"
	"log"
	"net"
	"time"
)

// 패킷 구조체
type Packet struct {
	// 타임스탬프
	TS string
	// 메시지 코드
	MsgCode int
	// 메시지
	Msg []string
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
