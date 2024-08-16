package main

const (
	// 이건 못살리는 에러(송)
	PanicError = -1
	// 살릴만한 에러(송)
	DeniedError = 0
	// 백그라운드로 전달한 명령 성공(송)
	Success = 1
	// 호스트가 생성되었음(송)
	CreateHost = 2
	// 주위 피어를 찾았음(송)
	FoundPeer = 3
	// 주위 피어가 사라짐(송)
	RemovePeer = 4
	// 채팅방에 참여하고 싶음(수)
	JoinGossip = 5
	// 채팅방에서 나가고 싶음(수)
	ExitGossip = 6
	// 메시지를 게시하고 싶음(수)
	Publish = 7
	// 메시지가 왔음(송)
	GotGossip = 8
)
