using System;

namespace HelpDeskServer.Dtos
{
    /// <summary>
    /// 프로젝트 대시보드 통계 DTO
    /// </summary>
    public class ProjectDashboardStatsDto
    {
        /// <summary>프로젝트 이름</summary>
        public string ProjectName { get; set; }
        /// <summary>담당 팀 이름</summary>
        public string TeamName { get; set; }
        /// <summary>프로젝트 시작일</summary>
        public DateTime? StartDate { get; set; }
        /// <summary>프로젝트 종료일</summary>
        public DateTime? EndDate { get; set; }
        /// <summary>전체 진행률</summary>
        public double OverallProgress { get; set; }
        /// <summary>총 WBS 항목 수</summary>
        public int TotalWbsCount { get; set; }
        /// <summary>진행 중인 WBS 항목 수</summary>
        public int InProgressWbsCount { get; set; }
        /// <summary>
        /// 완료된 WBS 항목 수
        /// </summary>
        public int CompletedWbsCount { get; set; }
        /// <summary>
        /// 대기 중인 WBS 항목 수
        /// </summary>
        public int PendingWbsCount { get; set; }
    }
}
