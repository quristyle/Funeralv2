using Microsoft.EntityFrameworkCore;
using NotificationServer.Entities;

namespace NotificationServer.Data;

/// <summary>
/// 알림 서비스 DB 컨텍스트
/// </summary>
/// <remarks>
/// 포털 DB(<c>funeralv2</c> / <c>scom</c>)를 쓴다.
///
/// <para>
/// <b>서비스별 DB 가 정석이지만 지금은 그렇게 하지 않았다.</b> 결정 D2(DB 통합)가
/// 아직 열려 있고, 여기서 DB 를 하나 더 만들면 정리해야 할 것이 늘어난다.
/// 구독 표 하나뿐이라 포털 DB 에 두고, D2 가 정해지면 함께 옮기는 편이 낫다고 보았다.
/// </para>
/// </remarks>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;

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
