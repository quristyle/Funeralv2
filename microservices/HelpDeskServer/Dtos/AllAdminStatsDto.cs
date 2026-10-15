namespace HelpDeskServer.Dtos
{
    /// <summary>
    /// 모든 관리자의 업무 통계 DTO
    /// </summary>
    public class AllAdminStatsDto
    {
        /// <summary>관리자 ID</summary>
        public int AdminId { get; set; }
        /// <summary>관리자 이름</summary>
        public string AdminName { get; set; } = string.Empty;
        /// <summary>관리자 사진 URL</summary>
        public string? AdminPhoto { get; set; }
        /// <summary>
        /// 포털 로그인 아이디(<c>scom.accounts.user_id</c>). 이 담당자가
        /// <b>포털의 누구인가</b>를 가리킨다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// 이름과 사진의 정본은 포털(AuthServer)이다 — 헬프데스크의
        /// <c>admin.username</c>·<c>admin.photo</c> 는 담당자 줄을 만들 때
        /// 한 번 베껴 둔 값이라, 포털에서 이름을 고치거나 사진을 바꿔도
        /// 따라오지 않는다(<c>AssigneeProvisioner</c>).
        /// </para>
        /// <para>
        /// 그래서 <b>이 아이디만 실어 보낸다.</b> 화면은 이것으로
        /// <c>auth/user/faces</c> 에 한 번 물어 이름과 얼굴을 받는다 —
        /// 서버가 사람마다 포털을 다시 부르면 담당자 수만큼 왕복이 난다.
        /// 비어 있으면(옛 자료로 만들어진 줄) 화면은 <c>AdminName</c> 으로
        /// 물러선다.
        /// </para>
        /// </remarks>
        public string? LoginId { get; set; }
        /// <summary>대기 중인 요청 수 (관리 팀 업체)</summary>
        public int PendingCount { get; set; }
        /// <summary>진행 중인 요청 수</summary>
        public int InProgressCount { get; set; }
        /// <summary>완료한 요청 수</summary>
        public int CompletedCount { get; set; }
        /// <summary>반려한 요청 수</summary>
        public int RejectedCount { get; set; }
        /// <summary>협의 요청 수</summary>
        public int ConsultationCount { get; set; }
        /// <summary>논의 요청 수</summary>
        public int NegotiationCount { get; set; }
        /// <summary>전체 요청 대비 처리율</summary>
        public double AcceptanceRate { get; set; }
        /// <summary>처리한 요청 대비 완료율</summary>
        public double CompletionRate { get; set; }
        /// <summary>총 처리한 요청 수</summary>
        public int TotalHandled { get; set; }
    }
}
