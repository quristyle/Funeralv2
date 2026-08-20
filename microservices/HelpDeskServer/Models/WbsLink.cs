using HelpDeskServer.Services;

namespace HelpDeskServer.Models;

/// <summary>
/// WBS 항목 간의 연결(의존성)을 정의하는 엔티티
/// </summary>
public class WbsLink : BaseEntity
{
    /// <summary>
    /// 소스 WBS 항목 ID
    /// </summary>
    public int SourceWbsId { get; set; }
    /// <summary>
    /// 소스 WBS 항목 (Navigation property)
    /// </summary>
    public Wbs SourceWbs { get; set; } // Navigation property

    /// <summary>
    /// 타겟 WBS 항목 ID
    /// </summary>
    public int TargetWbsId { get; set; }
    /// <summary>
    /// 타겟 WBS 항목 (Navigation property)
    /// </summary>
    public Wbs TargetWbs { get; set; } // Navigation property

    /// <summary>
    /// 연결 타입 (e.g., "0" for finish-to-start)
    /// </summary>
    public string Type { get; set; } = "0"; // e.g., "0" for finish-to-start
}