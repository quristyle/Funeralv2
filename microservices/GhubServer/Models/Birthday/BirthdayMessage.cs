using System.ComponentModel.DataAnnotations;

namespace GhubServer.Models;

/// <summary>
/// 생일 축하 메시지.
/// 원본(GHUB)은 UserProfile 네비게이션을 갖고 있었지만 이 DB 에는 사용자 표가
/// 없어(정본은 scom) 뺐다 — 이름 조인은 엔드포인트가 BirthdayProfiles 로 한다.
/// </summary>
public class BirthdayMessage : GhubBaseEntity
{
    /// <summary>받는 사람 ID (UserId)</summary>
    [MaxLength(100)]
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>보내는 사람 ID (UserId)</summary>
    [MaxLength(100)]
    public string SenderId { get; set; } = string.Empty;

    /// <summary>축하 메시지 내용</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>확인 여부</summary>
    public bool IsRead { get; set; } = false;
}
