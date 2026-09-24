using Microsoft.EntityFrameworkCore;

namespace CargoTrustServer.Data;

/// <summary>
/// CargoTrustServer DB 컨텍스트 — DB: cargotrust, 스키마: cargotrust.
///
/// **스키마는 이 코드가 만들지 않는다** — deploy/sql/cargotrust-schema-2026-09-24.sql 이 만든다.
/// 마이그레이션도 EnsureCreated 도 쓰지 않는다. 다른 서비스에서 초기 마이그레이션이 유실돼
/// 빈 DB 를 못 세운 일이 있었다 — 여기서는 SQL 파일 하나가 정본이다.
/// </summary>
public class CargoTrustDbContext(DbContextOptions<CargoTrustDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<CargoTransaction> Transactions => Set<CargoTransaction>();
    public DbSet<PaymentRecord> Payments => Set<PaymentRecord>();
    public DbSet<TransactionReview> Reviews => Set<TransactionReview>();
    public DbSet<TransactionDispute> Disputes => Set<TransactionDispute>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<CompanyView> CompanyViews => Set<CompanyView>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("cargotrust");

        // 테이블 이름은 클래스 이름의 snake_case 가 아니라 SQL 파일의 이름을 그대로 적는다
        // (DbSet 이름을 따라가면 users · transactions 가 되어 버린다).
        modelBuilder.Entity<Company>(e =>
        {
            e.ToTable("company");
            e.HasKey(x => x.CompanyId);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("app_user");
            e.HasKey(x => x.UserId);
            e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId);
        });

        modelBuilder.Entity<CargoTransaction>(e =>
        {
            e.ToTable("cargo_transaction");
            e.HasKey(x => x.TransactionId);
            e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
            e.Property(x => x.Amount).HasPrecision(15, 2);
            e.Property(x => x.PaidAmount).HasPrecision(15, 2);
        });

        modelBuilder.Entity<PaymentRecord>(e =>
        {
            e.ToTable("payment_record");
            e.HasKey(x => x.PaymentId);
            e.Property(x => x.PaidAmount).HasPrecision(15, 2);
        });

        modelBuilder.Entity<TransactionReview>(e =>
        {
            e.ToTable("transaction_review");
            e.HasKey(x => x.ReviewId);
            e.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.TransactionId);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
        });

        modelBuilder.Entity<TransactionDispute>(e =>
        {
            e.ToTable("transaction_dispute");
            e.HasKey(x => x.DisputeId);
            e.HasOne(x => x.Transaction).WithMany().HasForeignKey(x => x.TransactionId);
            e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId);
            e.HasOne(x => x.Requester).WithMany().HasForeignKey(x => x.RequesterUserId);
        });

        modelBuilder.Entity<Report>(e =>
        {
            e.ToTable("report");
            e.HasKey(x => x.ReportId);
            e.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterUserId);
        });

        modelBuilder.Entity<CompanyView>(e =>
        {
            e.ToTable("company_view");
            e.HasKey(x => new { x.UserId, x.CompanyId });
            e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.ToTable("audit_log");
            e.HasKey(x => x.AuditId);
            e.Property(x => x.BeforeData).HasColumnType("jsonb");
            e.Property(x => x.AfterData).HasColumnType("jsonb");
        });

        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // 열 이름은 속성 이름의 snake_case (CompanyId → company_id)
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));

            // 코드값 열은 VARCHAR 다. enum 을 이름 문자열로 읽고 쓴다.
            foreach (var property in entity.GetProperties())
            {
                var type = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (type.IsEnum)
                    property.SetProviderClrType(typeof(string));
            }
        }
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return System.Text.RegularExpressions.Regex
            .Replace(input, "([a-z0-9])([A-Z])", "$1_$2").ToLower();
    }
}
