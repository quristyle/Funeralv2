using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace HelpDeskServer.Models;

/// <summary>
/// 프로젝트 WBS(Work Breakdown Structure) 엔티티
/// 대규모 프로젝트에서 작업 단위, 일정, 담당자, 진행률, 리스크 등을 관리
/// </summary>
[Table("wbs")]
public class Wbs : BaseEntity
{
    /// <summary>
    /// WBS 고유 식별자 (Primary Key)
    /// </summary>
    [Key]
    public int WbsRid { get; set; }



    /// <summary>프로젝트 ID (Foreign Key)</summary>
    public int? ProjectId { get; set; }
    /// <summary>소속 프로젝트 (Navigation property)</summary>
    public Project? Project { get; set; }

    /// <summary>관리자 ID (Foreign Key)</summary>
    public int? ManagerId { get; set; }
    /// <summary>담당 관리자 (Navigation property)</summary>
    public Admin? Manager { get; set; }

    /// <summary>책임자 ID (Foreign Key)</summary>
    public int? ResponsibleUserId { get; set; }
    /// <summary>책임자 (Navigation property)</summary>
    public Admin? ResponsibleUser { get; set; }

    /// <summary>개발자 ID (Foreign Key)</summary>
    public int? BuildUserId { get; set; }
    /// <summary>개발자 (Navigation property)</summary>
    public Admin? BuildUser { get; set; }

    /// <summary>QA 담당자 ID (Foreign Key)</summary>
    public int? QcUserId { get; set; }
    /// <summary>QA 담당자 (Navigation property)</summary>
    public Admin? QcUser { get; set; }

    /// <summary>고객 ID (Foreign Key)</summary>
    public int? CustomerId { get; set; }
    /// <summary>관련 고객 (Navigation property)</summary>
    public Customer? Customer { get; set; }

    /// <summary>고객사 ID (Foreign Key)</summary>
    public int? CustomerCompanyId { get; set; }
    /// <summary>관련 고객사 (Navigation property)</summary>
    public CustomerCompany? CustomerCompany { get; set; }


   /// <summary>부모 WBS ID (Foreign Key)</summary>
   public int? ParentWbsId { get; set; }

    /// <summary>
    /// 부모 WBS 객체 (Navigation Property)
    /// 상위 WBS ID (부모 WBS 참조, NULL이면 최상위 WBS)
    /// </summary>
    public Wbs? ParentWbs { get; set; }

    /// <summary>
    /// WBS 코드 (예: 1.1.2)  
    /// 계층 구조를 표현하기 위한 식별용 코드
    /// </summary>
    [MaxLength(100)]
    public string WbsCode { get; set; } = string.Empty;

    /// <summary>
    /// WBS 이름 (작업 단위 명칭)
    /// </summary>
    [MaxLength(1000)]
    public string WbsName { get; set; } = string.Empty;

    /// <summary>
    /// WBS 유형 (Task, Milestone 등)
    /// </summary>
    [MaxLength(100)]
    public string? WbsType { get; set; }

    /// <summary>
    /// WBS 계층(Level)
    /// </summary>
    public int? WbsLevel { get; set; }

    // 고객 ID (Foreign Key)
    //public Customer? Customer { get; set; }

    // 고객 회사 ID (Foreign Key)
    //public CustomerCompany? CustomerCompany { get; set; }

    // 프로젝트 관리자 ID (Foreign Key)
    //public Admin? ManagerId { get; set; }


    /// <summary>
    /// 계획 시작일
    /// </summary>
    public DateTime? PlanStart { get; set; }

    /// <summary>
    /// 계획 종료일
    /// </summary>
    public DateTime? PlanEnd { get; set; }

    /// <summary>
    /// 실제 시작일
    /// </summary>
    public DateTime? ActualStart { get; set; }

    /// <summary>
    /// 실제 종료일
    /// </summary>
    public DateTime? ActualEnd { get; set; }

    /// <summary>
    /// 진행률(%)  
    /// 0 ~ 100 사이 값
    /// </summary>
    public decimal Progress { get; set; } = 0;

    /// <summary>
    /// 우선순위 (High, Medium, Low 등)
    /// </summary>
    [MaxLength(50)]
    public string? Priority { get; set; }

    /// <summary>
    /// WBS 상태 (Pending, In Progress, Completed 등)
    /// </summary>
    [MaxLength(100)]
    public string Status { get; set; } = "Pending";

    /// <summary>
    /// 리스크 수준 (Low, Medium, High 등)
    /// </summary>
    [MaxLength(50)]
    public string? RiskLevel { get; set; }

    /// <summary>예산 (계획 비용)</summary>
    public decimal? Budget { get; set; }

    /// <summary>실제 투입 비용</summary>
    public decimal? Cost { get; set; }

    // 작업 작성자/개발자
    //[MaxLength(1000)]
    //public Admin? BuildUser { get; set; }

    // 작업 상태(개발 상태)
    //[MaxLength(100)]
    //public string? BuildStatus { get; set; }

    /// <summary>개발 확인 여부 (e.g., "Y", "N")</summary>
    [MaxLength(100)]
    public string? DevCheck { get; set; }

    // QA 담당자
    //[MaxLength(1000)]
    //public Admin? QcUser { get; set; }

    /// <summary>QA 확인 여부 (e.g., "Y", "N")</summary>
    [MaxLength(100)]
    public string? QcCheck { get; set; }

    /// <summary>QA 확인 일자</summary>
    public DateTime? QcCheckDate { get; set; }

    /// <summary>작업 관련 코멘트/비고</summary>
    public string? Comments { get; set; }

    /// <summary>
    /// 일정 유형 (Normal, Milestone 등)
    /// </summary>
    [MaxLength(100)]
    public string? ScheduleType { get; set; }

    /// <summary>
    /// 하위 WBS 컬렉션 (트리 구조 탐색용 Navigation Property)
    /// </summary>
    /// <remarks>
    /// 이 속성은 데이터베이스의 'parent_wbs_id' 외래 키에 의해 채워집니다.
    /// </remarks>
    public ICollection<Wbs>? ChildWbs { get; set; }



    /// <summary>
    /// 정렬 순서 인덱스
    /// </summary>
    public int? OrderIndex { get; set; }



}
