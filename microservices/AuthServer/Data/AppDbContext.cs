using Microsoft.EntityFrameworkCore;
using AuthServer.Entities;
using System.Security.Claims;
using System.Text.RegularExpressions;

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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
            .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Modified));

        var now = DateTime.UtcNow;

        foreach (var entry in entries)
        {
            var entity = (BaseEntity)entry.Entity;

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
