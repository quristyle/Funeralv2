using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using JSini.Shared.Domain;

namespace NotificationServer.Entities;

/// <summary>
/// 포털에서 사람이 손으로 써 보낸 메일 <b>한 통</b>. 보낸 사람의 보관함이다.
/// </summary>
/// <remarks>
/// <para>
/// [<see cref="PushSendLog"/> 와 무엇이 다른가]
/// </para>
///
/// <para>
/// 발송 기록은 <b>받는 사람마다 한 줄</b>이고 본문은 500자 미리보기만 남는다
/// (<c>EmailSendLog</c> 머리말). 「저 사람에게 무엇이 나갔나」에 답하는 표라
/// 그 모양이 맞다. 그런데 <b>보낸 사람이 자기가 쓴 메일을 다시 열어 보는</b>
/// 자리에서는 그것으로 모자란다 — 참조 · 첨부 이름 · 서식 본문 전문이 거기
/// 없고, 한 통이 주소 수만큼 흩어져 있어 「내가 보낸 메일 목록」을 만들 수도
/// 없다.
/// </para>
///
/// <para>
/// 그래서 <b>보낸 글 자체</b>를 여기 둔다. 쪽지(<see cref="Note"/>)를 발송
/// 기록에 얹지 않은 것과 같은 까닭이다 — 두드림이 남긴 자국과 글 자체는
/// 수명이 다르다. 발송 기록은 그대로 함께 남는다(한 통에 주소 수만큼).
/// </para>
///
/// <para>
/// [첨부는 이름만 적는다]
/// </para>
///
/// <para>
/// 메일 첨부는 보낼 때 바이트를 그대로 실어 보내고 어디에도 안 올린다
/// (<c>EmailAttachmentDto</c> 머리말). 그 바이트를 보관함에 담으면 아무도
/// 지우지 않는 파일이 보낼 때마다 쌓인다. 보낸 사람이 나중에 묻는 것은
/// <b>「무엇을 붙여 보냈나」</b>이지 그 파일 자체가 아니다 — 그것은 자기
/// 장비에 있다.
/// </para>
/// </remarks>
[Table("portal_mails", Schema = "scom")]
public class PortalMail : BaseEntity<string>
{
    public PortalMail()
    {
        Id = Guid.NewGuid().ToString();
    }

    /// <summary>보낸 사람의 포털 로그인 아이디(<c>scom.accounts.user_id</c>).</summary>
    /// <remarks>
    /// <b>이 표의 주인 키다.</b> 목록도 상세도 최근 주소도 전부 「내 것」으로만
    /// 묻는다 — 남의 보낸함을 여는 길은 두지 않았다.
    /// </remarks>
    [Required]
    [Column("sender_key")]
    public string SenderKey { get; set; } = string.Empty;

    /// <summary>
    /// 보낸 사람의 이름을 <b>그때 모습 그대로</b> 적어 둔다.
    /// 까닭은 <see cref="Note.SenderName"/> 과 같다.
    /// </summary>
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

    /// <summary>
    /// 목록 줄에 적을 앞머리 — 태그를 걷어낸 본문 500자.
    /// </summary>
    /// <remarks>
    /// <b>본문이 바로 옆에 있는데 왜 따로 적는가.</b> 보낸함 목록은 서른 줄씩
    /// 훑는 자리이고 서식 메일 본문은 한 통에 수십 KB 다 — 목록을 그릴 때마다
    /// 그것을 전부 끌어와 태그를 걷으면, <b>읽지도 않을 글 1MB</b>가 왕복마다
    /// 오간다. 적는 값은 발송 이력과 같은 규칙으로 만든다
    /// (<c>EmailSendLog.Preview</c>).
    /// </remarks>
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

    /// <summary>
    /// 메일 서버가 받아 주었나.
    /// </summary>
    /// <remarks>
    /// <b>못 보낸 것도 남긴다.</b> 발송 기록이 실패를 남기는 까닭과 같다 —
    /// 안 남기면 「그 시각에 아무 일도 없었다」가 「보낸 적 없다」와 구분되지
    /// 않는다. 그리고 실패한 글이야말로 다시 열어 고쳐 보내야 하는 글이다.
    /// </remarks>
    [Column("is_success")]
    public bool IsSuccess { get; set; }

    /// <summary>못 보낸 까닭(사람이 읽는 말). 보냈으면 <c>null</c>.</summary>
    [Column("failure_reason")]
    public string? FailureReason { get; set; }

    /// <summary>
    /// 같은 발송의 <c>scom.push_send_logs</c> 줄을 묶는 열쇠.
    /// </summary>
    /// <remarks>
    /// 보관함과 발송 기록을 잇는 유일한 끈이다. 「이 메일이 실제로 어느
    /// 주소까지 갔나」를 묻는 날 두 표를 이 값으로 맞춘다.
    /// </remarks>
    [Column("batch_id")]
    public string? BatchId { get; set; }

    /// <summary>AI 가 만든 요약을 본문 머리에 넣었는가. 쓰임새를 세는 값이다.</summary>
    [Column("has_ai_summary")]
    public bool HasAiSummary { get; set; }
}
