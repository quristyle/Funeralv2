using LifeEnvServer.Models;
using Microsoft.EntityFrameworkCore;

namespace LifeEnvServer.Data;

/// <summary>
/// LifeEnvServer DB 컨텍스트(DB·스키마 ghub). 스키마는 이 코드가 만들지 않는다(docs/sql/ghub_schema.sql).
/// 테이블·컬럼은 원본과 같은 snake_case 라 옛 자료를 그대로 복사할 수 있다.
/// </summary>
public class LifeEnvDbContext(DbContextOptions<LifeEnvDbContext> options) : DbContext(options)
{
    // ── 기상 ─────────────────────────────────────────────────
    public DbSet<WeatherLocation> WeatherLocations => Set<WeatherLocation>();
    public DbSet<WeatherInfo> WeatherInfos => Set<WeatherInfo>();
    public DbSet<WeatherStandard> WeatherStandards => Set<WeatherStandard>();
    public DbSet<WeatherResponse> WeatherResponses => Set<WeatherResponse>();
    public DbSet<WeatherEventRecord> WeatherEventRecords => Set<WeatherEventRecord>();
    public DbSet<WeatherWarning> WeatherWarnings => Set<WeatherWarning>();
    public DbSet<WeatherLocationWarning> WeatherLocationWarnings => Set<WeatherLocationWarning>();
    public DbSet<WeatherWarningMsg> WeatherWarningMsgs => Set<WeatherWarningMsg>();
    public DbSet<WeatherWarningMsgSentence> WeatherWarningMsgSentences => Set<WeatherWarningMsgSentence>();
    public DbSet<WeatherWarningZone> WeatherWarningZones => Set<WeatherWarningZone>();
    public DbSet<WeatherWarningStatus> WeatherWarningStatuses => Set<WeatherWarningStatus>();
    public DbSet<GridCoordinate> GridCoordinates => Set<GridCoordinate>();
    public DbSet<WeatherMidTermForecast> WeatherMidTermForecasts => Set<WeatherMidTermForecast>();
    public DbSet<WeatherShortTermForecast> WeatherShortTermForecasts => Set<WeatherShortTermForecast>();
    public DbSet<WeatherUltraSrtForecast> WeatherUltraSrtForecasts => Set<WeatherUltraSrtForecast>();

    // 생일 정보는 포털(AuthServer)이 맡는다 — 여기에 테이블을 두지 않는다.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema("ghub");

        modelBuilder.Entity<GridCoordinate>().HasKey(g => g.AdministrativeCode);

        modelBuilder.Entity<WeatherWarningMsg>()
            .HasMany(m => m.Sentences)
            .WithOne(s => s.WeatherWarningMsg)
            .HasForeignKey(s => s.WeatherWarningMsgId);

        // 모든 테이블·컬럼·제약 이름을 snake_case 로 (원본 GHUB 과 같은 규칙)
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var tableName = entity.GetTableName();
            if (tableName != null && !tableName.StartsWith("__"))
                entity.SetTableName(ToSnakeCase(tableName));

            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.Name));

            foreach (var key in entity.GetKeys())
                if (key.GetName() is { } name)
                    key.SetName(ToSnakeCase(name));

            foreach (var foreignKey in entity.GetForeignKeys())
                if (foreignKey.GetConstraintName() is { } name)
                    foreignKey.SetConstraintName(ToSnakeCase(name));

            foreach (var index in entity.GetIndexes())
                if (index.GetDatabaseName() is { } name)
                    index.SetDatabaseName(ToSnakeCase(name));
        }
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return System.Text.RegularExpressions.Regex
            .Replace(input, "([a-z0-9])([A-Z])", "$1_$2").ToLower();
    }
}
