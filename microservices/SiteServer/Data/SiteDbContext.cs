using Microsoft.EntityFrameworkCore;
using SiteServer.Entities;

namespace SiteServer.Data;

/// <summary>
/// 회사 소개 사이트의 데이터 컨텍스트.
/// </summary>
/// <remarks>
/// 포털과 같은 <c>funeralv2</c> 인스턴스를 쓰지만 스키마는 <c>site</c> 로 따로 판다.
/// 별도 인스턴스까지 가지 않는 이유는 운영할 것이 하나 더 늘기 때문이고,
/// 스키마를 나누는 이유는 공개 사이트가 쓰는 표와 업무 표가 섞이지 않게 하려는 것이다.
///
/// <b>스키마는 이 코드가 만들지 않는다.</b> <c>docs/sql/site_schema.sql</c> 이 만든다.
/// FileServer 만 <c>Database.Migrate()</c> 를 쓰는데, <c>.gitignore</c> 가 <c>Migrations/</c> 를
/// 제외하고 있어 그 방식은 다른 장비로 가지 않는다. 나머지 여섯 서비스와 같은 방식을 따른다.
/// </remarks>
public class SiteDbContext : DbContext
{
    public SiteDbContext(DbContextOptions<SiteDbContext> options) : base(options)
    {
    }

    public DbSet<SiteSection> Sections => Set<SiteSection>();
    public DbSet<SitePost> Posts => Set<SitePost>();
    public DbSet<SiteDownload> Downloads => Set<SiteDownload>();
    public DbSet<SiteInquiry> Inquiries => Set<SiteInquiry>();
    public DbSet<SiteVisit> Visits => Set<SiteVisit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SiteSection>().ToTable("sections");
        modelBuilder.Entity<SitePost>().ToTable("posts");
        modelBuilder.Entity<SiteDownload>().ToTable("downloads");
        modelBuilder.Entity<SiteInquiry>().ToTable("inquiries");
        modelBuilder.Entity<SiteVisit>().ToTable("visits");

        // 열쇠는 언어와 함께 유일하다. 같은 열쇠의 한국어판과 영어판이 각각 한 행이다.
        modelBuilder.Entity<SiteSection>()
            .HasIndex(x => new { x.SectionKey, x.Locale })
            .IsUnique();

        modelBuilder.Entity<SitePost>()
            .HasIndex(x => new { x.Slug, x.Locale })
            .IsUnique();

        // 공개 목록 조회가 가장 잦다. (언어 · 공개여부 · 공개시각) 으로 훑는다.
        modelBuilder.Entity<SitePost>()
            .HasIndex(x => new { x.Locale, x.IsPublished, x.PublishedAt });

        modelBuilder.Entity<SiteDownload>()
            .HasIndex(x => new { x.Locale, x.IsPublished, x.SortOrder });

        modelBuilder.Entity<SiteVisit>()
            .HasIndex(x => new { x.VisitDate, x.Path, x.Locale })
            .IsUnique();

        // 표 이름과 컬럼 이름을 모두 소문자로, 스키마는 site 로 강제한다.
        // 다른 서비스(scom · jsini)와 같은 규칙이다.
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(entity.GetTableName()?.ToLower());
            entity.SetSchema("site");

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(property.GetColumnName().ToLower());
            }
        }
    }
}
