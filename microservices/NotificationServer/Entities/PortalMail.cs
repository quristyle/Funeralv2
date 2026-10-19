using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>포털에서 사람이 손으로 써 보낸 메일 한 통(보낸 사람의 보관함). 발송 기록은 미리보기뿐이라 보낸 글 전문을 따로 둔다.
/// 첨부는 이름만 적고 바이트는 보낼 때만 싣는다.</summary>
[Table("portal_mails", Schema = "scom")]
public class PortalMail : BaseEntity<string>
{
    public PortalMail()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>보낸 사람의 포털 로그인 아이디. 이 표의 주인 키다 — 목록도 상세도 최근 주소도 내 것으로만 묻는다.</summary>
    [Required]
    [Column("sender_key")]
    public string SenderKey { get; set; } = string.Empty;

    /// <summary>보낸 사람의 이름을 그때 모습 그대로 적어 둔다(Note.SenderName 과 같은 까닭).</summary>
    [Column("sender_name")]
    public string? SenderName { get; set; }

    /// <summary>받는 사람. 여럿이면 쉼표로 잇는다 (보낼 때의 그 글자 그대로).</summary>
    [Required]
    [Column("to_addresses")]
    public string ToAddresses { get; set; } = string.Empty;

    /// <summary>참조(CC). 없으면 <c>null</c>.</summary>
    [Column("cc_addresses")]
    public string? CcAddresses { get; set; }

    [Column("subject")]
    public string? Subject { get; set; }

    /// <summary>본문 <b>전문</b>(HTML). 미리보기가 아니다 — 다시 열어 읽는 자리다.</summary>
    [Column("body")]
    public string? Body { get; set; }

    /// <summary>목록 줄에 적을 앞머리 — 태그를 걷어낸 본문 500자. 목록을 그릴 때마다 전문을 끌어와 걷지 않으려고 따로 적는다.</summary>
    [Column("preview")]
    public string? Preview { get; set; }

    /// <summary>본문이 HTML 인가. 지금 화면은 늘 참이지만 값으로 남긴다.</summary>
    [Column("is_html")]
    public bool IsHtml { get; set; } = true;

    /// <summary>붙인 파일의 개수.</summary>
    [Column("attachment_count")]
    public int AttachmentCount { get; set; }

    /// <summary>붙인 파일 이름들을 쉼표로 이은 것. 바이트는 담지 않는다(머리말).</summary>
    [Column("attachment_names")]
    public string? AttachmentNames { get; set; }

    /// <summary>붙인 파일의 총량(바이트).</summary>
    [Column("attachment_bytes")]
    public long AttachmentBytes { get; set; }

    /// <summary>보낸 때(UTC). 목록의 기본 정렬이고 기간 조건이 이 값을 본다.</summary>
    [Column("sent_at")]
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    /// <summary>메일 서버가 받아 주었나. 못 보낸 것도 남긴다 — 실패한 글이야말로 고쳐 다시 보낼 글이다.</summary>
    [Column("is_success")]
    public bool IsSuccess { get; set; }

    /// <summary>못 보낸 까닭(사람이 읽는 말). 보냈으면 <c>null</c>.</summary>
    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    /// <summary>같은 발송의 scom.push_send_logs 줄을 묶는 열쇠. 보관함과 발송 기록을 잇는 유일한 끈이다.</summary>
    [Column("batch_id")]
    public string? BatchId { get; set; }

    /// <summary>AI 가 만든 요약을 본문 머리에 넣었는가. 쓰임새를 세는 값이다.</summary>
    [Column("has_ai_summary")]
    public bool HasAiSummary { get; set; }
}
