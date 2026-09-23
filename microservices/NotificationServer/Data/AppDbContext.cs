using Microsoft.EntityFrameworkCore;
using NotificationServer.Entities;

namespace NotificationServer.Data;

/// <summary>
/// 알림 서비스 DB 컨텍스트
/// </summary>
/// <remarks>
/// 포털 DB(<c>jsiniportal</c> / <c>scom</c>)를 쓴다.
/// (2026-08-29 전에는 같은 스키마가 <c>funeralv2</c> 안에 있었다.)
///
/// <para>
/// <b>서비스별 DB 가 정석이지만 지금은 그렇게 하지 않았다.</b> 구독 표 하나뿐이라
/// AuthServer · FileServer 와 같은 <c>scom</c> 에 둔다. 셋은 포털이라는 한 덩어리를
/// 이루고 계정 · 파일 · 구독이 서로를 참조하는데, DB 를 갈라 놓으면 그 참조가
/// 코드 안의 약속으로만 남는다. 반대로 <c>SiteServer</c> 는 익명 입력을 받아
/// 경계가 필요하므로 갈라 두었다(<c>jsinisite</c>).
/// </para>
/// </remarks>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;

    /// <summary>사람별 알림 수신 설정. 구독(기기)과 달리 사람 하나에 한 행이다.</summary>
    public DbSet<NotificationPreference> NotificationPreferences { get; set; } = null!;

    /// <summary>
    /// 보낸 기록. <b>보낸 쪽이 자기 기록을 갖는다</b> — 그 전에는 아무 데도
    /// 안 남아서 포털관리의 현황·이력 화면이 늘 비어 있었다(PushSendLog 머리말).
    /// </summary>
    public DbSet<PushSendLog> PushSendLogs { get; set; } = null!;

    /// <summary>
    /// 쪽지. <b>발송 기록과 갈래가 다르다</b> — 두드림(푸시·메일)이 다 막혀도
    /// 남아야 하는 글이라 표를 따로 둔다(<see cref="Note"/> 머리말).
    /// </summary>
    public DbSet<Note> Notes { get; set; } = null!;

    // ── scom 계정·역할 (읽기 전용) ──────────────────────────
    // "이 역할 사용자들의 이메일" 을 풀기 위한 조회 전용 매핑이다.
    // 정본은 AuthServer 이고 여기서는 절대 쓰지 않는다 (ScomIdentityRows.cs 머리말).
    public DbSet<RoleAccountRow> RoleAccounts => Set<RoleAccountRow>();
    public DbSet<AccountRow> Accounts => Set<AccountRow>();
    public DbSet<AccountProfileDetailRow> AccountProfileDetails => Set<AccountProfileDetailRow>();

    /// <summary>부서 이름. 쪽지 받는 사람을 고를 때 같은 이름을 가른다.</summary>
    public DbSet<DepartmentRow> Departments => Set<DepartmentRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 같은 브라우저가 다시 구독하면 같은 endpoint 가 온다. 새 행을 만들면
        // 같은 기기에 두 번 보내게 되므로 유일해야 한다.
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

        // 기록은 **언제나 시간으로 훑는다** — 목록도 통계도 기간이 첫 조건이다.
        modelBuilder.Entity<PushSendLog>()
            .HasIndex(l => l.SentAt);

        // 「이 사람에게 무엇이 갔나」도 자주 묻는다.
        modelBuilder.Entity<PushSendLog>()
            .HasIndex(l => new { l.OwnerType, l.OwnerKey });

        // 쪽지는 **받은함과 보낸함**으로만 훑는다. 둘 다 사람 한 명 + 시간순이라
        // 색인이 둘 필요하다 — 하나로 두면 보낸함이 표를 통째로 읽는다.
        modelBuilder.Entity<Note>()
            .HasIndex(n => new { n.ReceiverKey, n.SentAt });

        modelBuilder.Entity<Note>()
            .HasIndex(n => new { n.SenderKey, n.SentAt });

        // ── 컬럼명을 snake_case 로 맞춘다 ──────────────────────
        //
        // AuthServer 의 AppDbContext 와 같은 방식이다. **이것이 없으면 EF 가
        // BaseEntity 의 속성을 PascalCase 그대로 찾는다** — 엔티티에 [Column] 을 달아 둬도
        // 상속받은 Id·CreatedAt 등은 안 달려 있어서 `column p.Id does not exist` 로 깨진다
        // (실제로 겪었다).
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
