using Microsoft.EntityFrameworkCore;
using AuthServer.Entities;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Funeralv2.Shared.Domain;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<RoleAccount>()
            .HasIndex(ra => new { ra.RoleId, ra.AccountId })
            .IsUnique();

        modelBuilder.Entity<RoleMenu>()
            .HasIndex(rm => new { rm.RoleId, rm.MenuId })
            .IsUnique();

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
