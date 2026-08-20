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
        public string AdminName { get; set; }
        /// <summary>관리자 사진 URL</summary>
        public string? AdminPhoto { get; set; }
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
