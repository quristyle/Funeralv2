using AIAgentServer.Services.Gmail;

using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Data;

/// <summary>AI 사용량과 AI쳇 대화를 적고 읽는다. 이 서비스가 가진 유일한 DB 접점이다. 포털 DB(scom)를 쓰며, 계정·메뉴·회사는 읽기만 한다(정본은 AuthServer).
/// 연결 문자열이 없으면 적는 쪽이 조용히 건너뛴다 — 사용량은 곁다리고 대화가 본일이다. 마이그레이션은 두지 않고 표는 SQL(deploy/sql)이 만든다.</summary>
public class AiUsageDbContext(DbContextOptions<AiUsageDbContext> options) : DbContext(options)
{
    /// <summary>AI 호출 한 건. 쌓기만 하고 고치지 않는다.</summary>
    public DbSet<AiUsageRow> AiUsageLogs => Set<AiUsageRow>();

    /// <summary>계정 이름을 붙이려고 읽는 매핑. <b>조회 전용이다.</b></summary>
    public DbSet<AiUsageAccountRow> Accounts => Set<AiUsageAccountRow>();

    // 메뉴 권한은 읽기 전용이다. 정본은 AuthServer 이고 사이드바와 같은 표에 묻는다(Services/MenuAccess.cs).

    public DbSet<SystemMenuRow> SystemMenus => Set<SystemMenuRow>();

    public DbSet<RoleMenuRow> RoleMenus => Set<RoleMenuRow>();

    // AI쳇 대화 보관(deploy/sql/ai-chat-2026-10-08.sql).

    public DbSet<AiChatSessionRow> AiChatSessions => Set<AiChatSessionRow>();

    public DbSet<AiChatMessageRow> AiChatMessages => Set<AiChatMessageRow>();

    // RAG 문서 보관. 파일 실물은 디스크·파일 서버에 있고 여기에는 경로와 색인 상태만 있다(deploy/sql/rag-documents-2026-10-16.sql).

    /// <summary>소속 회사 이름을 붙이려고 읽는 매핑. 조회 전용이다.</summary>
    public DbSet<CompanyRow> Companies => Set<CompanyRow>();

    /// <summary>소속 부서 이름을 붙이려고 읽는 매핑. 조회 전용이다.</summary>
    public DbSet<DepartmentRow> Departments => Set<DepartmentRow>();

    public DbSet<RagCategoryRow> RagCategories => Set<RagCategoryRow>();

    public DbSet<RagFolderRow> RagFolders => Set<RagFolderRow>();

    public DbSet<RagDocumentRow> RagDocuments => Set<RagDocumentRow>();

    // 업무 비서 기록 표. 권한 밖 자료를 넘겨다본 흔적을 남긴다(deploy/sql/ai-assistant-audit-2026-10-25.sql).

    public DbSet<AssistantAuditRow> AssistantAudits => Set<AssistantAuditRow>();

    // Gmail 보관함. 길 셋(takeout·api·imap)이 한 표에 쌓인다(deploy/sql/gmail-archive-2026-10-25.sql).

    public DbSet<GmailAccountRow> GmailAccounts => Set<GmailAccountRow>();

    public DbSet<GmailMessageRow> GmailMessages => Set<GmailMessageRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AiUsageRow>(e =>
        {
            e.ToTable("ai_usage_logs", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.OccurredAt).HasColumnName("occurred_at");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Feature).HasColumnName("feature");
            e.Property(x => x.ProviderKey).HasColumnName("provider_key");
            e.Property(x => x.Model).HasColumnName("model");
            e.Property(x => x.PromptTokens).HasColumnName("prompt_tokens");
            e.Property(x => x.CompletionTokens).HasColumnName("completion_tokens");
            e.Property(x => x.TotalTokens).HasColumnName("total_tokens");
            e.Property(x => x.LatencyMs).HasColumnName("latency_ms");
            e.Property(x => x.Ok).HasColumnName("ok");
            e.Property(x => x.FailReason).HasColumnName("fail_reason");
        });

        modelBuilder.Entity<AiUsageAccountRow>(e =>
        {
            e.ToTable("accounts", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.UserName).HasColumnName("user_name");
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.DepartmentId).HasColumnName("department_id");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        });

        modelBuilder.Entity<SystemMenuRow>(e =>
        {
            e.ToTable("system_menus", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Path).HasColumnName("path");
            e.Property(x => x.RouteKey).HasColumnName("route_key");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        });

        modelBuilder.Entity<AiChatSessionRow>(e =>
        {
            e.ToTable("ai_chat_sessions", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.UserId).HasColumnName("user_id");
            e.Property(x => x.Title).HasColumnName("title");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        modelBuilder.Entity<AiChatMessageRow>(e =>
        {
            e.ToTable("ai_chat_messages", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.SessionId).HasColumnName("session_id");
            e.Property(x => x.Role).HasColumnName("role");
            e.Property(x => x.Content).HasColumnName("content");
            e.Property(x => x.Seq).HasColumnName("seq");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<RoleMenuRow>(e =>
        {
            e.ToTable("role_menus", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.RoleId).HasColumnName("role_id");
            e.Property(x => x.MenuId).HasColumnName("menu_id");
            e.Property(x => x.CanView).HasColumnName("can_view");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        });

        modelBuilder.Entity<CompanyRow>(e =>
        {
            e.ToTable("companies", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        });

        modelBuilder.Entity<DepartmentRow>(e =>
        {
            e.ToTable("departments", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Name).HasColumnName("name");
        });

        modelBuilder.Entity<RagCategoryRow>(e =>
        {
            e.ToTable("rag_categories", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.CompanyId).HasColumnName("company_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.Description).HasColumnName("description");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.IndexedAt).HasColumnName("indexed_at");
            e.Property(x => x.ChunkCount).HasColumnName("chunk_count");
            e.Property(x => x.DocCount).HasColumnName("doc_count");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");

            e.HasOne(x => x.Company)
                .WithMany()
                .HasForeignKey(x => x.CompanyId)
                .IsRequired(false);
        });

        modelBuilder.Entity<RagFolderRow>(e =>
        {
            e.ToTable("rag_folders", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CategoryId).HasColumnName("category_id");
            e.Property(x => x.ParentId).HasColumnName("parent_id");
            e.Property(x => x.Name).HasColumnName("name");
            e.Property(x => x.SortNo).HasColumnName("sort_no");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.UpdatedBy).HasColumnName("updated_by");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        });

        modelBuilder.Entity<RagDocumentRow>(e =>
        {
            e.ToTable("rag_documents", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.CategoryId).HasColumnName("category_id");
            e.Property(x => x.FolderId).HasColumnName("folder_id");
            e.Property(x => x.FileName).HasColumnName("file_name");
            e.Property(x => x.ContentType).HasColumnName("content_type");
            e.Property(x => x.SizeBytes).HasColumnName("size_bytes");
            e.Property(x => x.Sha256).HasColumnName("sha256");
            e.Property(x => x.FileId).HasColumnName("file_id");
            e.Property(x => x.StoredPath).HasColumnName("stored_path");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.ChunkCount).HasColumnName("chunk_count");
            e.Property(x => x.Error).HasColumnName("error");
            e.Property(x => x.IndexedAt).HasColumnName("indexed_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.CreatedBy).HasColumnName("created_by");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
        });

        modelBuilder.Entity<AssistantAuditRow>(e =>
        {
            e.ToTable("ai_assistant_audit", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.AskedBy).HasColumnName("asked_by");
            e.Property(x => x.AskedAt).HasColumnName("asked_at");
            e.Property(x => x.Question).HasColumnName("question");
            e.Property(x => x.ScopeCategory).HasColumnName("scope_category");
            e.Property(x => x.ToolCalls).HasColumnName("tool_calls").HasColumnType("jsonb");
            e.Property(x => x.Sources).HasColumnName("sources").HasColumnType("jsonb");
            e.Property(x => x.Answer).HasColumnName("answer");
            e.Property(x => x.ElapsedMs).HasColumnName("elapsed_ms");
            e.Property(x => x.Error).HasColumnName("error");
        });

        modelBuilder.Entity<GmailAccountRow>(e =>
        {
            e.ToTable("gmail_accounts", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Email).HasColumnName("email");
            e.Property(x => x.OwnerLoginId).HasColumnName("owner_login_id");
            e.Property(x => x.Kind).HasColumnName("kind");
            e.Property(x => x.Enabled).HasColumnName("enabled");
            e.Property(x => x.LastHistoryId).HasColumnName("last_history_id");
            e.Property(x => x.LastUid).HasColumnName("last_uid");
            e.Property(x => x.UidValidity).HasColumnName("uid_validity");
            e.Property(x => x.LastSyncAt).HasColumnName("last_sync_at");
            e.Property(x => x.LastError).HasColumnName("last_error");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<GmailMessageRow>(e =>
        {
            e.ToTable("gmail_messages", "scom");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
            e.Property(x => x.AccountId).HasColumnName("account_id");
            e.Property(x => x.Source).HasColumnName("source");
            e.Property(x => x.ProviderMsgId).HasColumnName("provider_msg_id");
            e.Property(x => x.ThreadId).HasColumnName("thread_id");
            e.Property(x => x.Rfc822MessageId).HasColumnName("rfc822_message_id");
            e.Property(x => x.Subject).HasColumnName("subject");
            e.Property(x => x.FromAddr).HasColumnName("from_addr");
            e.Property(x => x.FromName).HasColumnName("from_name");
            e.Property(x => x.ToAddrs).HasColumnName("to_addrs").HasColumnType("jsonb");
            e.Property(x => x.CcAddrs).HasColumnName("cc_addrs").HasColumnType("jsonb");
            e.Property(x => x.SentAt).HasColumnName("sent_at");
            e.Property(x => x.Labels).HasColumnName("labels").HasColumnType("jsonb");
            e.Property(x => x.Snippet).HasColumnName("snippet");
            e.Property(x => x.BodyText).HasColumnName("body_text");
            e.Property(x => x.BodyHtml).HasColumnName("body_html");
            e.Property(x => x.HasAttachments).HasColumnName("has_attachments");
            e.Property(x => x.Attachments).HasColumnName("attachments").HasColumnType("jsonb");
            e.Property(x => x.RagDocId).HasColumnName("rag_doc_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });
    }
}

/// <summary>AI 호출 한 건. 칸의 뜻은 deploy/sql/ai-usage-2026-10-08.sql 에 있다.</summary>
public class AiUsageRow
{
    public long Id { get; set; }

    /// <summary>언제. <b>UTC 다.</b></summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>로그인 아이디. 사람 없이 난 내부 호출이면 <c>null</c>.</summary>
    public string? UserId { get; set; }

    /// <summary><c>chat</c> · <c>chat-stream</c> · <c>suggest-code</c> ….</summary>
    public string Feature { get; set; } = string.Empty;

    /// <summary><b>실제로 답한</b> 공급자. 고른 것과 다를 수 있다.</summary>
    public string ProviderKey { get; set; } = string.Empty;

    public string? Model { get; set; }

    /// <summary><c>null</c> 은 「공급자가 안 알려 줬다」지 0 이 아니다.</summary>
    public int? PromptTokens { get; set; }

    /// <inheritdoc cref="PromptTokens"/>
    public int? CompletionTokens { get; set; }

    /// <inheritdoc cref="PromptTokens"/>
    public int? TotalTokens { get; set; }

    public int? LatencyMs { get; set; }

    public bool Ok { get; set; } = true;

    public string? FailReason { get; set; }
}

/// <summary>이름을 붙이려고 읽는 계정 한 줄. <b>조회 전용이다.</b></summary>
public class AiUsageAccountRow
{
    public string Id { get; set; } = string.Empty;

    /// <summary>로그인 아이디. 사용량 줄의 <c>user_id</c> 와 맞춘다.</summary>
    public string UserId { get; set; } = string.Empty;

    public string? UserName { get; set; }

    /// <summary>소속 회사 식별자. 비어 있을 수 있다.</summary>
    public string? CompanyId { get; set; }

    /// <summary>소속 부서 식별자. 비어 있을 수 있다(61명 중 37명만 찼다).</summary>
    public string? DepartmentId { get; set; }

    public bool IsDeleted { get; set; }
}

/// <summary>부서 이름을 붙이려고 읽는 한 줄. <b>조회 전용이다.</b></summary>
public class DepartmentRow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>메뉴 한 줄. <b>조회 전용이다</b> — 열쇠로 메뉴를 찾는 데만 쓴다.</summary>
public class SystemMenuRow
{
    public string Id { get; set; } = string.Empty;

    /// <summary>DB 의 경로. 아직 Vue 시절 값이 남아 있을 수 있다.</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>화면이 선언한 열쇠(<c>RouteKeyAttribute</c>). 이것으로 찾는다.</summary>
    public string? RouteKey { get; set; }

    public bool IsDeleted { get; set; }
}

/// <summary>역할이 메뉴에 가진 권한. <b>조회 전용이다.</b></summary>
public class RoleMenuRow
{
    public int Id { get; set; }

    public string RoleId { get; set; } = string.Empty;

    public string MenuId { get; set; } = string.Empty;

    /// <summary>그 화면을 열 수 있는가. 사이드바가 보는 것과 같은 칸이다.</summary>
    public bool CanView { get; set; }

    public bool IsDeleted { get; set; }
}

/// <summary>대화 한 줄기(주제 하나). 칸의 뜻은 deploy/sql/ai-chat-2026-10-08.sql 에 있다.</summary>
public class AiChatSessionRow
{
    public string Id { get; set; } = string.Empty;

    /// <summary>주인의 로그인 아이디. <b>남의 것은 열 수도 지울 수도 없다.</b></summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>첫 질문에서 따온 주제. 아직 말이 없으면 <c>null</c>.</summary>
    public string? Title { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>마지막으로 말이 오간 때. <b>목록 차례의 기준</b>이다.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>대화 속 한 마디. <b>system 지시문은 담지 않는다.</b></summary>
public class AiChatMessageRow
{
    public long Id { get; set; }

    public string SessionId { get; set; } = string.Empty;

    /// <summary><c>user</c> · <c>assistant</c>.</summary>
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    /// <summary>차례. 같은 초에 들어온 질문과 답을 가른다.</summary>
    public int Seq { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>회사 한 줄. <b>조회 전용이다.</b></summary>
public class CompanyRow
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
}

/// <summary>RAG 단위 하나. 색인 파일 하나에 대응한다.</summary>
/// <summary>RAG 단위 하나. 색인 파일 하나에 대응하며, 섞으면 안 되는 자료(사내 규정과 제품 설명서)를 가르는 자리다.</summary>
public class RagCategoryRow
{
    public string Id { get; set; } = string.Empty;

    /// <summary>소속 회사 식별자. 회사별 카테고리 분리 관리용이다.</summary>
    public string? CompanyId { get; set; }

    /// <summary>소속 회사 정보 (조회 전용 네비게이션).</summary>
    public CompanyRow? Company { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; } = 1;

    /// <summary>마지막으로 색인을 만든 때. 한 번도 안 만들었으면 null.</summary>
    public DateTime? IndexedAt { get; set; }

    public int ChunkCount { get; set; }
    public int DocCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>분류 한 칸. 카테고리 안의 트리다. AI쳇에서 고르면 그 분류(하위 포함)의 문서만 찾는다.</summary>
public class RagFolderRow
{
    public long Id { get; set; }
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>null 이면 카테고리 바로 아래.</summary>
    public long? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int SortNo { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>올려 둔 문서 한 건. <b>실물은 디스크에 있다.</b></summary>
public class RagDocumentRow
{
    public long Id { get; set; }
    public string CategoryId { get; set; } = string.Empty;
    public long? FolderId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public long SizeBytes { get; set; }

    /// <summary>같은 내용을 색인에 두 번 넣지 않으려고 적어 둔다.</summary>
    public string? Sha256 { get; set; }

    /// <summary>파일 서버가 붙인 아이디(실물은 그쪽에 있다). 비어 있으면 옛 StoredPath 로 읽는 줄이다.</summary>
    public Guid? FileId { get; set; }

    /// <summary>Rag:UploadRoot 기준 상대 경로. 옛 길이며 FileId 가 없는 줄만 이것으로 읽는다.</summary>
    public string StoredPath { get; set; } = string.Empty;

    /// <summary><c>pending</c> · <c>indexed</c> · <c>failed</c> · <c>skipped</c></summary>
    public string Status { get; set; } = "pending";

    public int ChunkCount { get; set; }
    public string? Error { get; set; }
    public DateTime? IndexedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public bool IsDeleted { get; set; }
}

/// <summary>업무 비서가 권한 밖 자료를 넘겨다본 흔적 한 줄. <b>답은 자르지 않는다</b> — 무엇이 나갔는지가
/// 기록의 전부다. 쌓기만 하고 고치지 않는다.</summary>
public class AssistantAuditRow
{
    public long Id { get; set; }

    /// <summary>물은 사람의 로그인 아이디.</summary>
    public string AskedBy { get; set; } = string.Empty;

    /// <summary>물은 때. <b>UTC 다.</b></summary>
    public DateTime AskedAt { get; set; }

    public string Question { get; set; } = string.Empty;

    /// <summary>사내 문서 검색 범위로 고른 RAG 카테고리. 안 골랐으면 <c>null</c>.</summary>
    public string? ScopeCategory { get; set; }

    /// <summary>모델이 부른 도구와 인자. JSON 배열 글.</summary>
    public string ToolCalls { get; set; } = "[]";

    /// <summary>답의 근거로 삼은 자료. JSON 배열 글.</summary>
    public string Sources { get; set; } = "[]";

    /// <summary><b>통째로</b> 담는다 — 요약이 아니라 실제로 나간 답 그대로.</summary>
    public string Answer { get; set; } = string.Empty;

    public long ElapsedMs { get; set; }

    public string? Error { get; set; }
}
