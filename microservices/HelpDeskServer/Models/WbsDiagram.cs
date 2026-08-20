using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HelpDeskServer.Models;

/// <summary>
/// WBS 항목별 다이어그램 데이터를 저장하는 엔티티
/// </summary>
[Table("wbs_diagram")]
public class WbsDiagram : BaseEntity
{
    /// <summary>다이어그램 고유 식별자</summary>
    [Key]
    public int WbsDiagramRid { get; set; }

    /// <summary>관련 WBS 항목 ID</summary>
    public int WbsRid { get; set; }
    
    /// <summary>관련 WBS 항목 (Navigation Property)</summary>
    [ForeignKey("WbsRid")]
    public Wbs? Wbs { get; set; }

    /// <summary>다이어그램 데이터 (XML 또는 JSON 문자열)</summary>
    public string? DiagramData { get; set; }
}
