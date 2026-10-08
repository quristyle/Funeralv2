using Microsoft.EntityFrameworkCore;

namespace AIAgentServer.Data;

/// <summary>
/// 사람별 AI 사용량을 적고 읽는 자리. <b>이 서비스가 가진 유일한 DB 접점이다.</b>
/// </summary>
/// <remarks>
/// <para>
/// 포털 DB(<c>jsiniportal</c> / <c>scom</c>)를 쓴다. 서비스별 DB 가 정석이지만
/// 표 하나 때문에 DB 를 늘리지 않았다 — NotificationServer 가 구독 표 하나로
/// 같은 판단을 한 자리와 같다(그쪽 <c>AppDbContext</c> 머리말).
/// </para>
/// <para>
/// <b>여기서 계정을 고치지 않는다.</b> <see cref="Accounts"/> 는 「누구의
/// 사용량인가」에 이름을 붙이려고 읽기만 하는 매핑이고, 정본은 AuthServer 다.
/// </para>
/// <para>
/// [마이그레이션이 없다]
/// </para>
/// <para>
/// 이 프로젝트에는 <c>Migrations</c> 폴더가 없고 <c>scom</c> 의 이력은
/// AuthServer · FileServer 가 함께 쓴다. 여기에 이력을 하나 더 붙이면 같은
/// 스키마를 세 곳이 서로 모르고 고치게 된다. 표는 SQL 이 만든다 —
/// <c>deploy/sql/ai-usage-2026-10-08.sql</c>.
/// </para>
/// <para>
/// [연결이 없어도 서비스는 돈다]
/// </para>
/// <para>
/// 연결 문자열을 안 넣은 개발 장비가 있다. 그때 AI 기능이 통째로 멎으면 안 되므로
/// <b>적는 쪽이 조용히 건너뛴다</b>(<see cref="Services.AiUsageLog"/>) —
/// 사용량은 곁다리고 대화가 본일이다.
/// </para>
/// </remarks>
public class AiUsageDbContext(DbContextOptions<AiUsageDbContext> options) : DbContext(options)
{
    /// <summary>AI 호출 한 건. 쌓기만 하고 고치지 않는다.</summary>
    public DbSet<AiUsageRow> AiUsageLogs => Set<AiUsageRow>();

    /// <summary>
    /// 계정 이름을 붙이려고 읽는 매핑. <b>조회 전용이다.</b>
    /// </summary>
    public DbSet<AiUsageAccountRow> Accounts => Set<AiUsageAccountRow>();

    // ── 메뉴 권한 (읽기 전용) ───────────────────────────────
    //
    // 「이 화면을 볼 수 있는 사람인가」를 **사이드바와 같은 표에 묻는다.**
    // 정본은 AuthServer 이고 여기서는 절대 쓰지 않는다 — 계정 이름을 읽는
    // 것과 같은 자리다(Services/MenuAccess.cs 머리말).

    public DbSet<SystemMenuRow> SystemMenus => Set<SystemMenuRow>();

    public DbSet<RoleMenuRow> RoleMenus => Set<RoleMenuRow>();

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
    }
}

/// <summary>
/// AI 호출 한 건. 칸의 뜻은 <c>deploy/sql/ai-usage-2026-10-08.sql</c> 머리말에 있다.
/// </summary>
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
