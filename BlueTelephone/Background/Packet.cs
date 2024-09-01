namespace BlueTelephone.Background
{
    /// <summary>
    /// 패킷 형태
    /// </summary>
    public class Packet
    {
        /// <summary>
        /// 타임스탬프,
        /// 활용 방안은 미정
        /// </summary>
        public required string TS { get; set; }
        /// <summary>
        /// 메시지 코드,
        /// 백그라운드와 공유
        /// </summary>
        public required int MsgCode { get; set; }
        /// <summary>
        /// 메시지,
        /// 문자열의 리스트로 이루어짐
        /// </summary>
        public required List<string> Msg { get; set; }
    }
}
