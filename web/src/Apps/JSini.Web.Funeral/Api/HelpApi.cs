using JSini.Web.Http;

namespace JSini.Web.Funeral.Api;

// ── 도움말 (F.A.Q · Q&A · 자료실) ─────────────────────────────────
//
// 이 셋은 funeralv2Api 가 아니라 **AuthServer** 에 있다 — 포털 공통 자료라
// 모든 MSA 사용자에게 같은 내용이 보인다. Vue 원본:
// fronts/apps/jsini-portal/src/api/portal/{faq,qna,help-archive}/index.ts.
//
// 버튼을 켜고 끄는 판단은 서버가 준 값(canManage · canWrite · canEdit)을 쓴다.

/// <summary>F.A.Q 한 건</summary>
public sealed class Faq
{
    public string Id { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    /// <summary>답변 (HTML)</summary>
    public string? Answer { get; set; }
    /// <summary>분류. 비우면 화면이 '기타' 로 묶는다.</summary>
    public string? Category { get; set; }
    public int OrderNo { get; set; }
    /// <summary>0: 비활성, 1: 활성</summary>
    public int Status { get; set; } = 1;
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>F.A.Q 목록 응답</summary>
public sealed class FaqList
{
    public List<string> Categories { get; set; } = [];
    /// <summary>등록·수정·삭제할 수 있는 사용자인지 (= 관리자)</summary>
    public bool CanManage { get; set; }
    public List<Faq> Items { get; set; } = [];
}

/// <summary>Q&amp;A 질문·답글 (같은 모양이다)</summary>
public sealed class QnaPost
{
    public string Id { get; set; } = string.Empty;
    public string RootId { get; set; } = string.Empty;
    public string? ParentId { get; set; }
    /// <summary>제목. 질문(뿌리)만 갖는다.</summary>
    public string? Title { get; set; }
    /// <summary>본문 (HTML)</summary>
    public string Content { get; set; } = string.Empty;
    public int Depth { get; set; }
    public string? AuthorId { get; set; }
    public string? AuthorName { get; set; }
    public string? AuthorAvatar { get; set; }
    public bool IsAnswer { get; set; }
    public bool? IsAnswered { get; set; }
    public bool IsMine { get; set; }
    public bool IsPublic { get; set; }
    public bool CanEdit { get; set; }
    public int? ReplyCount { get; set; }
    public DateTime? LastPostedAt { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    /// <summary>답글. 깊이 제한 없이 같은 모양으로 이어진다.</summary>
    public List<QnaPost> Children { get; set; } = [];
}

/// <summary>Q&amp;A 목록 응답</summary>
public sealed class QnaPostList
{
    public bool CanManage { get; set; }
    public bool CanWrite { get; set; }
    public List<QnaPost> Items { get; set; } = [];
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    /// <summary>조건에 맞는 질문 수 (답글은 세지 않는다)</summary>
    public int Total { get; set; }
}

/// <summary>자료실 첨부파일 한 개</summary>
public sealed class ArchiveFile
{
    public string Id { get; set; } = string.Empty;
    public string FileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? ContentType { get; set; }
    /// <summary>AuthServer 를 거치는 내려받기 주소 — 다운로드 수를 세고 302 로 넘긴다.</summary>
    public string DownloadUrl { get; set; } = string.Empty;
    public int DownloadCount { get; set; }
    public int SortNo { get; set; }
}

/// <summary>자료실 자료 한 건</summary>
public sealed class Archive
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    /// <summary>자료 설명 (HTML)</summary>
    public string? Description { get; set; }
    public string? Category { get; set; }
    public int OrderNo { get; set; }
    /// <summary>0: 비활성, 1: 활성</summary>
    public int Status { get; set; } = 1;
    public int DownloadCount { get; set; }
    public List<ArchiveFile> Files { get; set; } = [];
    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>자료실 목록 응답</summary>
public sealed class ArchiveList
{
    public bool CanManage { get; set; }
    public List<string> Categories { get; set; } = [];
    public List<Archive> Items { get; set; } = [];
}

/// <summary>
/// AuthServer 의 도움말 API 호출 (F.A.Q · Q&amp;A · 자료실).
/// </summary>
public sealed class HelpApi(GatewayClient gateway)
{
    // ── F.A.Q ──────────────────────────────────────────────────

    public async Task<FaqList> GetFaqListAsync(string? category = null, string? keyword = null, CancellationToken ct = default)
        => await gateway.GetOneAsync<FaqList>(
               "auth/faqs" + FuneralApi.Query(("category", category), ("keyword", keyword)), ct)
           ?? new FaqList();

    public Task CreateFaqAsync(object data, CancellationToken ct = default)
        => gateway.PostAsync("auth/faqs", data, ct);

    public Task UpdateFaqAsync(string id, object data, CancellationToken ct = default)
        => gateway.PutAsync($"auth/faqs/{id}", data, ct);

    public Task DeleteFaqAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/faqs/{id}", ct);

    // ── Q&A ────────────────────────────────────────────────────

    public async Task<QnaPostList> GetQnaListAsync(
        string? filter = null, string? keyword = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
        => await gateway.GetOneAsync<QnaPostList>(
               "auth/qna" + FuneralApi.Query(("filter", filter), ("keyword", keyword), ("page", page), ("pageSize", pageSize)), ct)
           ?? new QnaPostList();

    /// <summary>글 하나가 속한 스레드를 뿌리부터. 답글을 단 뒤 그 스레드만 다시 그릴 때 쓴다.</summary>
    public Task<QnaPost?> GetQnaThreadAsync(string id, CancellationToken ct = default)
        => gateway.GetOneAsync<QnaPost>($"auth/qna/{id}", ct);

    /// <summary>질문(parentId 없음) · 답글(parentId 있음) 등록.</summary>
    public Task<QnaPost?> CreateQnaPostAsync(object data, CancellationToken ct = default)
        => gateway.PostAsync<QnaPost>("auth/qna", data, ct);

    public Task UpdateQnaPostAsync(string id, object data, CancellationToken ct = default)
        => gateway.PutAsync($"auth/qna/{id}", data, ct);

    /// <summary>삭제. 답글까지 함께 지워진다.</summary>
    public Task DeleteQnaPostAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/qna/{id}", ct);

    /// <summary>공개 여부 변경 (관리자 전용). includeReplies 가 참이면 답글까지 함께 바꾼다.</summary>
    public Task SetQnaVisibilityAsync(string id, bool isPublic, bool includeReplies = false, CancellationToken ct = default)
        => gateway.PutAsync($"auth/qna/{id}/visibility", new { includeReplies, isPublic }, ct);

    // ── 자료실 ──────────────────────────────────────────────────

    public async Task<ArchiveList> GetArchiveListAsync(string? category = null, string? keyword = null, CancellationToken ct = default)
        => await gateway.GetOneAsync<ArchiveList>(
               "auth/help/archives" + FuneralApi.Query(("category", category), ("keyword", keyword)), ct)
           ?? new ArchiveList();

    public Task<Archive?> GetArchiveAsync(string id, CancellationToken ct = default)
        => gateway.GetOneAsync<Archive>($"auth/help/archives/{id}", ct);

    public Task CreateArchiveAsync(object data, CancellationToken ct = default)
        => gateway.PostAsync("auth/help/archives", data, ct);

    public Task UpdateArchiveAsync(string id, object data, CancellationToken ct = default)
        => gateway.PutAsync($"auth/help/archives/{id}", data, ct);

    public Task DeleteArchiveAsync(string id, CancellationToken ct = default)
        => gateway.DeleteAsync($"auth/help/archives/{id}", ct);
}
