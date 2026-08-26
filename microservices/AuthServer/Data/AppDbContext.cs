using Microsoft.EntityFrameworkCore;
using AuthServer.Entities;
using System.Security.Claims;
using System.Text.RegularExpressions;
using JSini.Shared.Domain;

namespace AuthServer.Data;

public class AppDbContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor) 
        : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public DbSet<Company> Companies { get; set; }
    public DbSet<Account> Accounts { get; set; }
    public DbSet<AccountProfileDetail> AccountProfileDetails { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<SystemMenu> SystemMenus { get; set; }
    public DbSet<I18nResource> I18nResources { get; set; }
    public DbSet<CommonCodeGroup> CommonCodeGroups { get; set; }
    public DbSet<CommonCode> CommonCodes { get; set; }
    public DbSet<BizSelectConfig> BizSelectConfigs { get; set; }
    public DbSet<RoleAccount> RoleAccounts { get; set; }
    public DbSet<RoleMenu> RoleMenus { get; set; }
    public DbSet<Notice> Notices { get; set; }
    public DbSet<NoticeFile> NoticeFiles { get; set; }
    public DbSet<Faq> Faqs { get; set; }
    public DbSet<QnaPost> QnaPosts { get; set; }
    public DbSet<HelpArchive> HelpArchives { get; set; }
    public DbSet<HelpArchiveFile> HelpArchiveFiles { get; set; }
    public DbSet<AccountLoginLog> AccountLoginLogs { get; set; }
    public DbSet<AccountPreference> AccountPreferences { get; set; }
    public DbSet<MenuFavorite> MenuFavorites { get; set; }
    public DbSet<RoleCompany> RoleCompanies { get; set; }
    public DbSet<RoleDepartment> RoleDepartments { get; set; }
    public DbSet<ReleaseRun> ReleaseRuns { get; set; }
    public DbSet<ReleaseRunEvent> ReleaseRunEvents { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RoleAccount>()
            .HasIndex(ra => new { ra.RoleId, ra.AccountId })
            .IsUnique();

        modelBuilder.Entity<RoleMenu>()
            .HasIndex(rm => new { rm.RoleId, rm.MenuId })
            .IsUnique();

        // 같은 회사·부서에 같은 역할을 두 번 걸지 못하게 한다.
        modelBuilder.Entity<RoleCompany>()
            .HasIndex(rc => new { rc.RoleId, rc.CompanyId })
            .IsUnique();

        modelBuilder.Entity<RoleDepartment>()
            .HasIndex(rd => new { rd.RoleId, rd.DepartmentId })
            .IsUnique();

        // 역할·회사가 사라지면 매핑도 함께 지운다.
        modelBuilder.Entity<RoleCompany>()
            .HasOne(rc => rc.Role).WithMany()
            .HasForeignKey(rc => rc.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RoleCompany>()
            .HasOne(rc => rc.Company).WithMany()
            .HasForeignKey(rc => rc.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<RoleDepartment>()
            .HasOne(rd => rd.Role).WithMany()
            .HasForeignKey(rd => rd.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        // 부서는 (company_id, id) 복합 대체키를 갖고 있어 EF 가 기본키로 자동 연결하지 못한다.
        // 부서 식별자만으로 잇고, 부서를 지울 때 매핑은 서비스에서 함께 정리한다.
        modelBuilder.Entity<RoleDepartment>()
            .HasIndex(rd => rd.DepartmentId);

        // 같은 사람이 같은 메뉴를 두 번 담지 못하게 한다. 등록 API 는 이미 있으면 그대로 두지만,
        // 두 창에서 동시에 눌러도 중복이 생기지 않도록 DB 쪽에서도 막는다.
        modelBuilder.Entity<MenuFavorite>()
            .HasIndex(f => new { f.AccountId, f.MenuId })
            .IsUnique();

        // 계정이나 메뉴가 사라지면 즐겨찾기도 함께 지운다.
        // 남겨 두면 아무 곳도 가리키지 않는 행이 쌓이고, 사이드바 조회에서 매번 걸러야 한다.
        modelBuilder.Entity<MenuFavorite>()
            .HasOne(f => f.Account)
            .WithMany()
            .HasForeignKey(f => f.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MenuFavorite>()
            .HasOne(f => f.Menu)
            .WithMany()
            .HasForeignKey(f => f.MenuId)
            .OnDelete(DeleteBehavior.Cascade);

        // 계정 하나에 환경설정 한 행. 두 창에서 동시에 저장해도 갈라지지 않게 한다.
        modelBuilder.Entity<AccountPreference>()
            .HasIndex(p => p.AccountId)
            .IsUnique();

        // 계정이 사라지면 설정도 함께 지운다.
        modelBuilder.Entity<AccountPreference>()
            .HasOne(p => p.Account)
            .WithMany()
            .HasForeignKey(p => p.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── 배포 실행 ──────────────────────────────────────────
        //
        // 같은 대상을 동시에 두 번 배포하지 못하게 한다. 화면에서 버튼을 잠그는 것만으로는
        // 두 사람이 동시에 누르는 것을 막지 못하고, 그러면 같은 체크아웃에서 스크립트 둘이 돈다.
        // 서비스가 먼저 확인하기도 하지만 경합은 이 인덱스에서만 확실히 막힌다.
        //
        // 'dispatched' 는 넣지 않는다 — 보고가 오지 않는 대상이라 영원히 안 풀린다.
        modelBuilder.Entity<ReleaseRun>()
            .HasIndex(r => r.TargetKey)
            .IsUnique()
            .HasFilter("status IN ('queued', 'running') AND is_deleted = false");

        // 화면이 sinceSeq 로 이어 받으므로 같은 순번이 두 번 들어오면 안 된다.
        modelBuilder.Entity<ReleaseRunEvent>()
            .HasIndex(e => new { e.RunId, e.Seq })
            .IsUnique();

        // run 을 지우면 로그도 함께 지운다.
        modelBuilder.Entity<ReleaseRunEvent>()
            .HasOne(e => e.Run)
            .WithMany(r => r.Events)
            .HasForeignKey(e => e.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        // Department 엔티티에 (CompanyId, Id) 복합 고유 키(AlternateKey) 설정
        modelBuilder.Entity<Department>()
            .HasAlternateKey(d => new { d.CompanyId, d.Id });

        // Account 엔티티에 복합 외래키 설정 (회사-부서 데이터 무결성 보장)
        modelBuilder.Entity<Account>()
            .HasOne(a => a.Department)
            .WithMany(d => d.Accounts)
            .HasForeignKey(a => new { a.CompanyId, a.DepartmentId })
            .HasPrincipalKey(d => new { d.CompanyId, d.Id })
            .OnDelete(DeleteBehavior.Restrict);

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // 테이블명 snake_case 변환
            entity.SetTableName(ToSnakeCase(entity.GetTableName()));
            entity.SetSchema("scom");

            foreach (var property in entity.GetProperties())
            {
                // 컬럼명 snake_case 변환
                property.SetColumnName(ToSnakeCase(property.Name));
            }
        }

        modelBuilder.ApplyXmlComments();
    }

    // PascalCase -> snake_case 변환 헬퍼
    private string ToSnakeCase(string? input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        return Regex.Replace(input, "([a-z0-9])([A-Z])", "$1_$2").ToLower();
    }

    public override int SaveChanges()
    {
        HandleAuditing();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        HandleAuditing();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void HandleAuditing()
    {
        var userId = _httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                     ?? _httpContextAccessor.HttpContext?.User?.Identity?.Name 
                     ?? "System";

        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            // 엔티티가 BaseEntity (int형) 이거나 BaseEntity<TKey>를 상속받았는지 확인
            var entityType = entry.Entity.GetType();
            bool isBaseEntity = false;
            
            var currentType = entityType;
            while (currentType != null && currentType != typeof(object))
            {
                if (currentType.IsGenericType && currentType.GetGenericTypeDefinition() == typeof(BaseEntity<>))
                {
                    isBaseEntity = true;
                    break;
                }
                if (currentType == typeof(BaseEntity))
                {
                    isBaseEntity = true;
                    break;
                }
                currentType = currentType.BaseType;
            }

            if (!isBaseEntity) continue;

            dynamic entity = entry.Entity;

            if (entry.State == EntityState.Added)
            {
                entity.CreatedAt = now;
                entity.CreatedBy = userId;
            }
            
            entity.UpdatedAt = now;
            entity.UpdatedBy = userId;
        }
    }
}
