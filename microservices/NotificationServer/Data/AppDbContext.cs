using Microsoft.EntityFrameworkCore;
using NotificationServer.Entities;

namespace NotificationServer.Data;

/// <summary>알림 서비스 DB 컨텍스트. 포털 DB(jsiniportal / scom)를 쓴다(구독·계정·파일이 서로를 참조하므로). 익명 입력의 SiteServer 만 따로 둔다(jsinisite).</summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;

    /// <summary>사람별 알림 수신 설정. 구독(기기)과 달리 사람 하나에 한 행이다.</summary>
    public DbSet<NotificationPreference> NotificationPreferences { get; set; } = null!;

    /// <summary>알림 이벤트 카탈로그. 「어떤 일이 일어났을 때 보내는 알림인가」의 목록이고 코드가 아니라 표가 정본이다.</summary>
    public DbSet<NotificationEventRow> NotificationEvents { get; set; } = null!;

    /// <summary>알림 정책 — 역할 × 이벤트 × 길. 사람의 뜻인 설정 표와 달리 회사의 규칙이다.</summary>
    public DbSet<NotificationPolicy> NotificationPolicies { get; set; } = null!;

    /// <summary>보낸 기록. 보낸 쪽이 자기 기록을 갖는다(다른 서비스 표에 끼어 쓰지 않는다).</summary>
    public DbSet<PushSendLog> PushSendLogs { get; set; } = null!;

    /// <summary>쪽지. 두드림(푸시·메일)이 다 막혀도 남아야 하는 글이라 발송 기록과 따로 둔다.</summary>
    public DbSet<Note> Notes { get; set; } = null!;

    /// <summary>사람이 손으로 써 보낸 메일 한 통. 발송 기록은 미리보기뿐이라 보낸 글 전문은 이쪽에 둔다.</summary>
    public DbSet<PortalMail> PortalMails { get; set; } = null!;

    /// <summary>지나온 자리. 설정 표의 좌표는 지금 위치라 덮어쓰고, 이쪽은 잴 때마다 쌓는다.</summary>
    public DbSet<LocationTrack> LocationTracks { get; set; } = null!;

    // ── scom 계정·역할 (읽기 전용) ──────────────────────────
    // 「이 역할 사용자들의 이메일」을 푸는 조회 전용 매핑이다. 정본은 AuthServer 다.
    public DbSet<RoleAccountRow> RoleAccounts => Set<RoleAccountRow>();
    public DbSet<AccountRow> Accounts => Set<AccountRow>();
    public DbSet<AccountProfileDetailRow> AccountProfileDetails => Set<AccountProfileDetailRow>();

    /// <summary>부서 이름. 쪽지 받는 사람을 고를 때 같은 이름을 가른다.</summary>
    public DbSet<DepartmentRow> Departments => Set<DepartmentRow>();

    /// <summary>권한 역할. 「알림관리」가 고를 목록이고, 저장할 때 없는 역할을 막는다.</summary>
    public DbSet<RoleRow> Roles => Set<RoleRow>();

    /// <summary>메뉴·메뉴권한. <b>「이 화면을 볼 수 있나」를 사이드바와 같은 표에 묻는다</b>.</summary>
    public DbSet<SystemMenuRow> SystemMenus => Set<SystemMenuRow>();

    /// <inheritdoc cref="SystemMenus" />
    public DbSet<RoleMenuRow> RoleMenus => Set<RoleMenuRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 같은 endpoint 가 오면 새 행을 만들지 않는다 — 같은 기기에 두 번 보내게 된다.
        modelBuilder.Entity<PushSubscription>()
            .HasIndex(s => s.Endpoint)
            .IsUnique();

        // 발송은 "이 주인들에게" 로만 훑는다.
        modelBuilder.Entity<PushSubscription>()
            .HasIndex(s => new { s.OwnerType, s.OwnerKey });

        // 알림 설정은 사람 하나에 한 행이다. 두 행이 생기면 어느 쪽이 참인지 알 수 없다.
        modelBuilder.Entity<NotificationPreference>()
            .HasIndex(p => new { p.OwnerType, p.OwnerKey })
            .IsUnique();

        // 정책은 (이벤트, 역할)에 한 줄이다 — 두 줄이면 껐는데 가는 쪽으로 틀린다.
        modelBuilder.Entity<NotificationPolicy>()
            .HasIndex(p => new { p.EventCode, p.RoleId })
            .IsUnique();

        // 발송 경로에서 읽는 유일한 표다. 이벤트 코드 하나로 그 줄들을 가져온다.
        modelBuilder.Entity<NotificationPolicy>()
            .HasIndex(p => p.EventCode);

        // 기록은 **언제나 시간으로 훑는다** — 목록도 통계도 기간이 첫 조건이다.
        modelBuilder.Entity<PushSendLog>()
            .HasIndex(l => l.SentAt);

        // 「이 사람에게 무엇이 갔나」도 자주 묻는다.
        modelBuilder.Entity<PushSendLog>()
            .HasIndex(l => new { l.OwnerType, l.OwnerKey });

        // 알림구분 조회는 언제나 기간과 함께 온다 — 그래서 구분만이 아니라 시각도 색인에 담는다.
        modelBuilder.Entity<PushSendLog>()
            .HasIndex(l => new { l.Category, l.SentAt });

        // 쪽지는 받은함과 보낸함으로만 훑는다 — 사람 하나 + 시간순 색인이 둘 필요하다.
        modelBuilder.Entity<Note>()
            .HasIndex(n => new { n.ReceiverKey, n.SentAt });

        modelBuilder.Entity<Note>()
            .HasIndex(n => new { n.SenderKey, n.SentAt });

        // 전환 메일 배치가 5분마다 훑으므로 부분 색인으로 둔다 — 셋이 채워지면 색인에서 빠져 표가 커져도 색인은 처리 안 된 몇 줄만 든다.
        modelBuilder.Entity<Note>()
            .HasIndex(n => n.SentAt)
            .HasDatabaseName("IX_notes_fallback")
            .HasFilter("read_at IS NULL AND delivered_at IS NULL AND fallback_email_at IS NULL");

        // 보낸 메일 보관함은 언제나 내 것 · 최근 순으로만 묻는다 — 주인 키가 빠진 조회가 생기지 않게.
        modelBuilder.Entity<PortalMail>()
            .HasIndex(m => new { m.SenderKey, m.SentAt });

        // 지나온 자리는 주인과 시각을 한 색인에 담는다 — 하루치를 뽑을 때 표를 통째로 읽지 않으려고.
        modelBuilder.Entity<LocationTrack>()
            .HasIndex(t => new { t.OwnerType, t.OwnerKey, t.RecordedAt });

        // ── 컬럼명을 snake_case 로 맞춘다 ──────────────────────
        // AuthServer 와 같은 방식이다. 이것이 없으면 EF 가 상속받은 Id·CreatedAt 을 PascalCase 로 찾아 깨진다.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnakeCase(entity.GetTableName()));
            entity.SetSchema("scom");

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }
    }

    /// <summary>PascalCase → snake_case. AuthServer 의 것과 같은 규칙이다.</summary>
    private static string ToSnakeCase(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return System.Text.RegularExpressions.Regex
            .Replace(input, "([a-z0-9])([A-Z])", "$1_$2")
            .ToLower();
    }
}
