using Microsoft.EntityFrameworkCore;
using SiteServer.Entities;

namespace SiteServer.Data;

/// <summary>회사 소개 사이트의 데이터 컨텍스트. 소개 사이트 전용 DB(jsinisite)를 따로 쓴다 — 익명 쓰기(문의 접수)가 닿는 표를 업무 DB 와 물리적으로 나누기 위해서다.
/// 대가는 FileServer 파일을 가리키는 것이 다른 DB 라 공개 여부를 한 문장 UPDATE 로 맞출 수 없고 PUT /api/file/public/{id} 를 거쳐야 한다는 것이다. 스키마는 docs/sql/site_schema.sql 이 만든다.</summary>
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
