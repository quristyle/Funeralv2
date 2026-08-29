using Microsoft.EntityFrameworkCore;
using SiteServer.Entities;

namespace SiteServer.Data;

/// <summary>
/// 회사 소개 사이트의 데이터 컨텍스트.
/// </summary>
/// <remarks>
/// <b>DB 를 따로 쓴다</b> — 소개 사이트 전용인 <c>jsinisite</c> 이고, 그 안의 <c>site</c> 스키마다.
///
/// 처음에는 포털과 같은 <c>funeralv2</c> 안에 스키마만 나눠 두었다. 옮긴 이유는
/// **이 서비스만 로그인하지 않은 사람의 입력을 받기 때문**이다(문의 접수).
/// 익명 쓰기가 닿는 표와 업무 표가 같은 DB 에 있으면, 나중에 문제가 생겼을 때
/// 경계가 DB 권한이 아니라 코드 안에만 있게 된다. 지금 나눠 두면 그 경계가 물리적으로 생긴다.
///
/// 대가도 있다. 소개 사이트의 자료·대표 이미지는 FileServer 의 파일을 가리키는데
/// (<c>scom.filemetadatas</c>), 그것이 이제 **다른 DB** 다. 그래서 공지가 첨부의 공개 여부를
/// 맞출 때 쓴 방법(같은 DB 라 한 문장 UPDATE — AuthServer/Services/PublicFileSyncService.cs)을
/// 여기서는 쓸 수 없다. 필요해지면 <c>PUT /api/file/public/{id}</c> 를 거쳐야 한다.
/// 경계를 나눈 값을 치르는 자리라, 이것은 흠이 아니라 의도다.
///
/// <b>스키마는 이 코드가 만들지 않는다.</b> <c>docs/sql/site_schema.sql</c> 이 만든다.
/// FileServer 만 <c>Database.Migrate()</c> 를 쓰는데, <c>.gitignore</c> 가 <c>Migrations/</c> 를
/// 제외하고 있어 그 방식은 다른 장비로 가지 않는다. 나머지 서비스와 같은 방식을 따른다.
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
