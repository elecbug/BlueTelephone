namespace BlueTelephone.Background
{
    /// <summary>
    /// 메시지 코드
    /// </summary>
    public enum MsgCode
    {
        /// <summary>
        /// 이건 못살리는 에러(수)
        /// </summary>
        PanicError = -1,
        /// <summary>
        /// 살릴만한 에러(수)
        /// </summary>
        GoodError = 0,
        /// <summary>
        /// 백그라운드로 전달한 명령 성공(수)
        /// </summary>
        Success = 1,
        /// <summary>
        /// 호스트가 생성되었음(수)
        /// </summary>
        CreateHost = 2,
        /// <summary>
        /// 주위 피어를 찾았음(수)
        /// </summary>
        FoundPeer = 3,
        /// <summary>
        /// 주위 피어가 사라짐(수)
        /// </summary>
        RemovePeer = 4,
        /// <summary>
        /// 채팅방에 참여하고 싶음(송)
        /// </summary>
        JoinGossip = 5,
        /// <summary>
        /// 채팅방에서 나가고 싶음(송)
        /// </summary>
        ExitGossip = 6,
        /// <summary>
        /// 메시지를 게시하고 싶음(송)
        /// </summary>
        Publish = 7,
        /// <summary>
        /// 메시지가 왔음(수)
        /// </summary>
        GotGossip = 8,
        /// <summary>
        /// 피어를 찾아주셈(수)
        /// </summary>
        PlzFindPeer = 9,
    }
}
