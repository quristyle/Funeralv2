namespace ProjMngServer.Models;

/// <summary>메일 본문 HTML 템플릿 한 건 — <c>projmng.mail_template</c>.</summary>
public sealed class MailTemplate
{
    /// <summary>번호. <b>등록할 때는 서버가 정한다.</b></summary>
    public long TplId { get; set; }

    /// <summary>쓰는 자리. <c>SCHEDULE</c> = 프로젝트 일정표 메일발송.</summary>
    public string TplKind { get; set; } = MailTemplateKinds.Schedule;

    public string? TplNm { get; set; }
    public string? TplDesc { get; set; }

    /// <summary>제목 틀. 비우면 화면이 기본 제목을 쓴다.</summary>
    public string? Subject { get; set; }

    public string? BodyHtml { get; set; }
    public bool UseYn { get; set; } = true;
    public int SortNo { get; set; }

    public string? CreId { get; set; }
    public DateTime? CreDt { get; set; }
    public string? ModId { get; set; }
    public DateTime? ModDt { get; set; }
}

public static class MailTemplateKinds
{
    public const string Schedule = "SCHEDULE";
}
