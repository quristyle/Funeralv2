using Microsoft.EntityFrameworkCore;
using funeralv2Api.Entities;

namespace funeralv2Api.Data;

/// <summary>
/// 애플리케이션 데이터베이스 컨텍스트 클래스
/// </summary>
public class AppDbContext : DbContext
{
    /// <summary>
    /// AppDbContext 생성자
    /// </summary>
    /// <param name="options">DbContext 옵션</param>
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// 건물 DbSet
    /// </summary>
    public DbSet<Building> Buildings { get; set; } = null!;

    /// <summary>
    /// 층 DbSet
    /// </summary>
    public DbSet<Floor> Floors { get; set; } = null!;

    /// <summary>
    /// 호실 DbSet
    /// </summary>
    public DbSet<Room> Rooms { get; set; } = null!;

    /// <summary>
    /// 장비 DbSet
    /// </summary>
    public DbSet<Device> Devices { get; set; } = null!;

    /// <summary>
    /// 장비 속성 DbSet
    /// </summary>
    public DbSet<DeviceAttribute> DeviceAttributes { get; set; } = null!;

    /// <summary>
    /// 장비 기본 설정 DbSet
    /// </summary>
    public DbSet<DeviceConfig> DeviceConfigs { get; set; } = null!;

    /// <summary>
    /// 미디어 소스 DbSet
    /// </summary>
    public DbSet<MediaSource> MediaSources { get; set; } = null!;

    /// <summary>
    /// 고인 DbSet
    /// </summary>
    public DbSet<Deceased> Deceaseds { get; set; } = null!;

    /// <summary>
    /// 고인 상주 DbSet
    /// </summary>
    public DbSet<DeceasedMourner> DeceasedMourners { get; set; } = null!;

    /// <summary>
    /// 고인 계약자 DbSet
    /// </summary>
    public DbSet<DeceasedContractor> DeceasedContractors { get; set; } = null!;

    /// <summary>
    /// 고인 장례 담당자 DbSet
    /// </summary>
    public DbSet<DeceasedManager> DeceasedManagers { get; set; } = null!;

    /// <summary>
    /// 고인 시설 이용 내역 DbSet
    /// </summary>
    public DbSet<DeceasedFacility> DeceasedFacilities { get; set; } = null!;

    /// <summary>
    /// 고인 호실 배정 이력 DbSet
    /// </summary>
    public DbSet<DeceasedRoom> DeceasedRooms { get; set; } = null!;



    /// <summary>
    /// 모델 생성 시 규칙 정의 (Fluent API)
    /// </summary>
    /// <param name="modelBuilder">ModelBuilder 객체</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // 모든 엔티티에 대해 공통 규칙 적용
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            // 테이블 이름을 소문자로 강제 설정
            var tableName = entity.GetTableName()?.ToLower();
            entity.SetTableName(tableName);
            
            // 모든 테이블을 'smfr' 스키마에 배치
            entity.SetSchema("smfr");

            // 모든 컬럼 이름을 소문자로 강제 설정
            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(property.GetColumnName().ToLower());
            }
        }

        modelBuilder.Entity<DeviceConfig>()
            .HasIndex(c => c.DeviceId)
            .IsUnique();
    }
}
