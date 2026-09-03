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
    /// 장비 리본 설정 DbSet
    /// </summary>
    public DbSet<DeviceRibbon> DeviceRibbons { get; set; } = null!;

    /// <summary>
    /// 장비 텍스트 오버레이 DbSet
    /// </summary>
    public DbSet<DeviceTextOverlay> DeviceTextOverlays { get; set; } = null!;

    // 알림 DbSet 둘(FuneralNotices · FuneralNoticeReads)은 2026-09-03 에 걷어냈다 —
    // `/info/notice` 화면을 쓰지 않아 함께 지웠다 (Endpoints/InfoEndpoints.cs 머리말).
    // 표도 마이그레이션 `RemoveFuneralNotices` 로 지웠다.

    /// <summary>
    /// 계정별 업무 설정 DbSet (옛 <c>t_account_conf</c>)
    /// </summary>
    public DbSet<AccountSetting> AccountSettings { get; set; } = null!;

    /// <summary>
    /// 건물별 음원 배정 DbSet (옛 <c>t_music_build</c>)
    /// </summary>
    public DbSet<BuildingMusic> BuildingMusics { get; set; } = null!;



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

        // 한 사람이 같은 설정을 두 줄 갖지 않게 막는다. 서비스는 그래도 가장 최근 것을
        // 고르도록 짜여 있는데(옛 데이터를 옮겨 올 때를 대비), 새로 생기는 것은 여기서 막는다.
        modelBuilder.Entity<AccountSetting>()
            .HasIndex(s => new { s.UserId, s.SettingCode })
            .IsUnique();

        // 같은 건물에 같은 음원을 두 번 배정하지 않는다.
        modelBuilder.Entity<BuildingMusic>()
            .HasIndex(m => new { m.BuildingId, m.MediaSourceId })
            .IsUnique();

        modelBuilder.ApplyXmlComments();
    }
}
