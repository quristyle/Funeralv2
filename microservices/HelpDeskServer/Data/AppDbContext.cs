using Microsoft.EntityFrameworkCore;
using HelpDeskServer.Models;
using HelpDeskServer.Services;
using HelpDeskServer.Utilities;

using System.Reflection;
using System.Xml.Linq;



namespace HelpDeskServer.Data;

/// <summary>
/// 애플리케이션의 데이터베이스 컨텍스트 클래스
/// </summary>
public class AppDbContext : DbContext {
  private readonly IHttpContextAccessor? _httpContextAccessor;

  /// <summary>
  /// 마이그레이션 도구를 위한 생성자
  /// </summary>
  /// <param name="options">DB 컨텍스트 옵션</param>
  public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

  /// <summary>
  /// 의존성 주입을 위한 생성자
  /// </summary>
  /// <param name="options">DB 컨텍스트 옵션</param>
  /// <param name="httpContextAccessor">HTTP 컨텍스트 접근자</param>
  public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor httpContextAccessor) : base(options) {
    _httpContextAccessor = httpContextAccessor;
  }

  /// <summary>고객사 테이블</summary>
  public DbSet<CustomerCompany> Companies { get; set; }

  /// <summary>고객 테이블</summary>
  public DbSet<Customer> Customers { get; set; }

  /// <summary>관리자 테이블</summary>
  public DbSet<Admin> Admins { get; set; }

  /// <summary>관리자-팀 매핑 테이블</summary>
  public DbSet<AdminTeam> AdminTeams { get; set; }

  /// <summary>팀 테이블</summary>
  public DbSet<Team> Teams { get; set; }

  /// <summary>팀-회사 매핑 테이블</summary>
  public DbSet<TeamCompany> TeamCompanies { get; set; }

  /// <summary>개선요청 테이블</summary>
  public DbSet<ImprovementRequest> Requests { get; set; }

  /// <summary>덧글 테이블</summary>
  public DbSet<ImprovementComment> Comments { get; set; }

  /// <summary>첨부파일 테이블</summary>
  public DbSet<Attachment> Attachments { get; set; }

  /// <summary>공지사항 테이블</summary>
  public DbSet<Notice> Notices { get; set; }


  /// <summary>고객사 테이블 (Companies와 동일)</summary>
  public DbSet<CustomerCompany> CustomerCompanies => Set<CustomerCompany>();




  /// <summary>WBS 테이블</summary>
  public DbSet<Wbs> Wbs { get; set; }

  /// <summary>WBS 다이어그램 테이블</summary>
  public DbSet<WbsDiagram> WbsDiagrams { get; set; }

  /// <summary>프로젝트 테이블</summary>
  public DbSet<Project> Projects { get; set; }
  /// <summary>WBS 연결 정보 테이블</summary>
  public DbSet<WbsLink> WbsLinks { get; set; } // WbsLink DbSet 추가

  /// <summary>Web Push 구독 정보 테이블</summary>
  public DbSet<PushSubscription> PushSubscriptions { get; set; }

  /// <summary>푸시 알림 발송 로그</summary>
  public DbSet<PushNotificationLog> PushNotificationLogs { get; set; }



  /// <summary>
  /// 푸시 메시지 테이블
  /// </summary>
  public DbSet<PushMessage> PushMessages { get; set; } // 추가

  /// <summary>
  /// 푸시 메시지 수신자 테이블
  /// </summary>
  public DbSet<PushMessageRecipient> PushMessageRecipients { get; set; } // 추가

  /// <summary>사용자 속성 테이블</summary>
  public DbSet<UserProperty> UserProperties { get; set; }

  /// <summary>시스템 운영전환 체크리스트 테이블</summary>
  public DbSet<Checklist> Checklists { get; set; }

  /// <summary>메뉴 관리 테이블</summary>
  public DbSet<Menu> Menus { get; set; }

  /// <summary>메뉴별 권한 테이블</summary>
  public DbSet<MenuRole> MenuRoles { get; set; }

  /// <summary>권한 그룹 테이블</summary>
  public DbSet<AppRole> Roles { get; set; }

  /// <summary>사용자별 권한 매핑 테이블</summary>
  public DbSet<AppUserRole> UserRoles { get; set; }

  /// <summary>역할별 메뉴 상세 권한 테이블</summary>
  public DbSet<RoleMenuPermission> RoleMenuPermissions { get; set; }

  /// <summary>일정 관리 테이블</summary>
  public DbSet<Schedule> Schedules { get; set; }

  /// <summary>MC 모델 테이블</summary>
  public DbSet<MC_Models> MC_Models { get; set; }

  /// <summary>파싱 아이템 테이블</summary>
  public DbSet<ParseItem> ParseItems { get; set; }

  /// <summary>태그 아이템 테이블</summary>
  public DbSet<TagItem> TagItems { get; set; }

  /// <summary>ACK 매칭 테이블</summary>
  public DbSet<MC_ACK_FIND> MC_AckFinds { get; set; }

  /// <summary>바이너리 파서 샘플 테이블</summary>
  public DbSet<BinarySample> BinarySamples { get; set; }

  /// <summary>funeralv2(AuthServer) 계정 ↔ 헬프데스크 계정 매핑</summary>
  public DbSet<AuthUserLink> AuthUserLinks { get; set; }





  /// <summary>
  /// 데이터베이스에 대한 변경사항을 저장하고 감사 속성을 설정합니다.
  /// </summary>
  public override int SaveChanges() {
    SetAuditProperties();
    return base.SaveChanges();
  }

  /// <summary>
  /// 데이터베이스에 대한 변경사항을 비동기적으로 저장하고 감사 속성을 설정합니다.
  /// </summary>
  public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) {
    SetAuditProperties();
    return base.SaveChangesAsync(cancellationToken);
  }

  /// <summary>
  /// 감사 속성을 설정합니다.
  /// </summary>
  private void SetAuditProperties() {
    var entries = ChangeTracker
        .Entries<BaseEntity>()
        .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

    foreach (var entityEntry in entries) {
      var now = DateTime.UtcNow;
      var httpContextForAudit = _httpContextAccessor?.HttpContext;
      string user;

      if (httpContextForAudit?.User?.Identity?.IsAuthenticated ?? false) {
        // 포털 계정으로 들어온 요청이면 JSini 로그인 아이디를 남긴다.
        // 예전에는 헬프데스크 내부 숫자 ID('uid')만 남아 나중에 누구인지 알아보기 어려웠다.
        // 헬프데스크 자체 토큰으로 들어온 요청은 예전과 같이 내부 ID 를 남긴다.
        user = httpContextForAudit.AuditUser();
      }
      else {
        user = "system"; // 인증되지 않은 요청의 경우
      }

      if (entityEntry.State == EntityState.Added) {
        entityEntry.Entity.CreatedAt = now;
        if (string.IsNullOrEmpty(entityEntry.Entity.CreatedBy)) {
          entityEntry.Entity.CreatedBy = user; // 생성자 자동 설정
        }
      }

      entityEntry.Entity.ModifiedAt = now;
      entityEntry.Entity.ModifiedBy = user; // 수정자 자동 설정

      entityEntry.Entity.ActionService = _httpContextAccessor?.HttpContext?.Request.Headers["X-Service-Name"]; // ActionService 자동 설정
      entityEntry.Entity.MenuContext = _httpContextAccessor?.HttpContext?.Request.Headers["X-Menu-Name"]; // MenuContext 자동 설정

      if (string.IsNullOrEmpty(entityEntry.Entity.MenuContext)) entityEntry.Entity.MenuContext = user; // MenuContext 기본값

      // 접속클라이언트의 아이피를 설정한다.
      var httpContext = _httpContextAccessor?.HttpContext;
      string remoteIpAddress = "";
      if (httpContext != null) {
        // X-Forwarded-For 헤더 확인 (가장 일반적인 프록시 헤더)
        var forwardedFor = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor)) {
          // 쉼표로 구분된 IP 목록 중 첫 번째 IP를 사용
          remoteIpAddress = forwardedFor.Split(',').FirstOrDefault()?.Trim() ?? "";
        }

        // 프록시 헤더가 없는 경우 직접 연결된 IP 주소 사용
        if (string.IsNullOrEmpty(remoteIpAddress) && httpContext.Connection.RemoteIpAddress != null) {
          remoteIpAddress = httpContext.Connection.RemoteIpAddress.MapToIPv4().ToString();
        }
      }
      entityEntry.Entity.RemoteAddr = remoteIpAddress;
    }
  }



  /*

      protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
      {
          // 개발 환경에서만 SQL 쿼리를 콘솔에 로깅합니다.
          // 실제 운영 환경에서는 성능에 영향을 줄 수 있으므로 주의해야 합니다.
          optionsBuilder.LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information);
          base.OnConfiguring(optionsBuilder);
      }
  */


  // protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
  // {
  //     if (!optionsBuilder.IsConfigured)
  //     {
  //         optionsBuilder.UseNpgsql("YourConnectionStringHere",
  //             x => x.MigrationsHistoryTable("__EFMigrationsHistory", "public"));
  //     }
  // }


  /// <summary>
  /// 데이터베이스 옵션을 구성합니다.
  /// </summary>
  /// <param name="optionsBuilder"></param>
  /// <exception cref="InvalidOperationException"></exception>
  protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) {
    if (!optionsBuilder.IsConfigured) {
      var conn = Environment.GetEnvironmentVariable("helpdesk")
                 ?? Environment.GetEnvironmentVariable("Help_JSINI");

      if (string.IsNullOrEmpty(conn))
        throw new InvalidOperationException("환경변수 helpdesk (구 Help_JSINI) 에 DB 접속 문자열이 설정되어 있지 않습니다.");

      optionsBuilder.UseNpgsql(
          conn,
          npgsqlOptions => npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "jsini")
      );

      // 로그를 보고자 하는 코드.. 이것은 까묵지 말고 꼭 나중에 지워야함. 쿼리 노출
      optionsBuilder.LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information);


    }

    base.OnConfiguring(optionsBuilder);
  }



  /*

  protected override void OnModelCreating(ModelBuilder modelBuilder)
  {
      modelBuilder.HasDefaultSchema("jsini");

      // BaseEntity 상속 구조를 명시적으로 TPT로 지정
      foreach (var entityType in modelBuilder.Model.GetEntityTypes())
      {
          if (entityType.ClrType.IsSubclassOf(typeof(BaseEntity)))
          {
              modelBuilder.Entity(entityType.ClrType).ToTable(entityType.ClrType.Name.ToLower());
          }
      }

      ...
  }


  */

  /// <summary>
  /// 
  /// </summary>
  /// <param name="modelBuilder"></param>
  protected override void OnModelCreating(ModelBuilder modelBuilder) {
    // set default schema
    modelBuilder.HasDefaultSchema("jsini");

    //BaseEntity 상속 구조를 명시적으로 TPT로 지정
    foreach (var entityType in modelBuilder.Model.GetEntityTypes()) {
      if (entityType.ClrType.IsSubclassOf(typeof(BaseEntity))) {
        modelBuilder.Entity(entityType.ClrType).ToTable(entityType.ClrType.Name.ToLower());
      }
    }

    // 팀-회사 N:N 관계 키 설정
    modelBuilder.Entity<TeamCompany>()
        .HasKey(tc => new { tc.TeamId, tc.CompanyId });

    modelBuilder.Entity<TeamCompany>()
        .HasOne(tc => tc.Team)
        .WithMany(t => t.TeamCompanies)
        .HasForeignKey(tc => tc.TeamId);

    modelBuilder.Entity<TeamCompany>()
        .HasOne(tc => tc.Company)
        .WithMany()
        .HasForeignKey(tc => tc.CompanyId);

    // 관리자-팀 N:N 관계 키 설정
    modelBuilder.Entity<AdminTeam>()
        .HasKey(at => new { at.AdminId, at.TeamId });

    modelBuilder.Entity<AdminTeam>()
        .HasOne(at => at.Admin)
        .WithMany(a => a.AdminTeams)
        .HasForeignKey(at => at.AdminId);

    modelBuilder.Entity<AdminTeam>()
        .HasOne(at => at.Team)
        .WithMany(t => t.AdminTeams)
        .HasForeignKey(at => at.TeamId);

    // 개선요청과 덧글 관계
    modelBuilder.Entity<ImprovementComment>()
        .HasOne(c => c.Request)
        .WithMany(r => r.Comments)
        .HasForeignKey(c => c.RequestId);

    // 덧글의 계층 구조(대댓글)를 위한 자체 참조 관계 설정
    modelBuilder.Entity<ImprovementComment>()
        .HasOne(c => c.ParentComment) // 각 덧글은 하나의 부모를 가질 수 있음
        .WithMany(c => c.Children)    // 각 덧글은 여러 자식 덧글을 가질 수 있음
        .HasForeignKey(c => c.ParentCommentId) // 외래 키는 ParentCommentId
        .OnDelete(DeleteBehavior.Restrict); // 순환 참조 또는 다중 캐스케이드 경로 문제를 방지하기 위해 Restrict 사용

    // 개선요청과 관리자 관계
    modelBuilder.Entity<ImprovementRequest>()
        .HasOne(r => r.Admin)
        .WithMany(a => a.AssignedRequests)
        .HasForeignKey(r => r.AdminId)
        .OnDelete(DeleteBehavior.SetNull);

    // 개선요청과 고객 관계
    modelBuilder.Entity<ImprovementRequest>()
        .HasOne(r => r.Customer)
        .WithMany(c => c.ImprovementRequests)
        .HasForeignKey(r => r.CustomerId)
        .OnDelete(DeleteBehavior.Cascade);

    // 첨부파일: 다형성 관계 (EntityType, EntityId)
    modelBuilder.Entity<Attachment>()
        .Property(a => a.EntityType)
        .IsRequired()
        .HasMaxLength(50);

    // PushSubscription의 기본 키를 Endpoint로 설정
    modelBuilder.Entity<PushSubscription>()
        .HasKey(ps => ps.Endpoint);

    // UserProperty 복합 인덱스 설정 (UserId, UserType, Key)
    modelBuilder.Entity<UserProperty>()
        .HasIndex(p => new { p.UserId, p.UserType, p.Key })
        .IsUnique();

    // Admin Soft Delete Filter
    modelBuilder.Entity<Admin>().HasQueryFilter(a => !a.IsDeleted);
    // Customer Soft Delete Filter
    modelBuilder.Entity<Customer>().HasQueryFilter(c => !c.IsDeleted);

    // MC_Models mapping
    modelBuilder.Entity<MC_Models>().ToTable("mc_models");
    modelBuilder.Entity<ParseItem>().ToTable("parse_items");
    modelBuilder.Entity<TagItem>().ToTable("tag_items");
    modelBuilder.Entity<MC_ACK_FIND>().ToTable("mc_ack_finds");

    modelBuilder.Entity<ParseItem>()
        .Property(p => p.Keys)
        .HasConversion(
            v => v.ToArray(),
            v => v.ToList()
        );

    modelBuilder.Entity<ParseItem>()
        .HasOne<MC_Models>()
        .WithMany(m => m.ParseItems)
        .HasForeignKey(p => p.MC_ModelsId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<TagItem>()
        .HasOne<ParseItem>()
        .WithMany(p => p.TagItems)
        .HasForeignKey(t => t.ParseItemId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<MC_ACK_FIND>()
        .HasOne<MC_Models>()
        .WithMany(m => m.AckFinds)
        .HasForeignKey(a => a.MC_ModelsId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<BinarySample>()
        .HasOne<MC_Models>()
        .WithMany(m => m.Samples)
        .HasForeignKey(s => s.MC_ModelsId)
        .OnDelete(DeleteBehavior.Cascade);


    // --- 추가: UserProperty 메타데이터 정리 및 명시 매핑 ---
    // var upEntity = modelBuilder.Entity<UserProperty>().Metadata;

    // // shadow 또는 잘못 생성된 AdminId 프로퍼티가 있으면,
    // // 먼저 해당 프로퍼티를 사용하는 FK들을 제거한 뒤 프로퍼티 제거
    // var shadowAdminProp = upEntity.FindProperty("AdminId") ?? upEntity.FindProperty("adminid");
    // if (shadowAdminProp != null) {
    //   // 해당 프로퍼티를 사용하는 모든 FK 수집 및 제거
    //   var fks = upEntity.GetForeignKeys().Where(fk => fk.Properties.Any(p => p.Name == shadowAdminProp.Name)).ToList();
    //   foreach (var fk in fks) {
    //     upEntity.RemoveForeignKey(fk);
    //   }
    //   upEntity.RemoveProperty(shadowAdminProp);
    // }



    //foreach (var entity in modelBuilder.Model.GetEntityTypes())        {
    // 테이블/컬럼명 소문자 변환 및 XML 주석을 DB 코멘트로 적용
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";

    //var xmlPath = Path.Combine(AppContext.BaseDirectory, "HelpDeskServer.xml");
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    XDocument xmlDoc = null;
    if (File.Exists(xmlPath)) {
      xmlDoc = XDocument.Load(xmlPath);
    }

    // BaseEntity를 상속하는 모든 엔티티의 Id 속성에 대해 자동 증가(Identity) 설정

    // BaseEntity를 상속하는 모든 엔티티의 Id 속성에 대해 자동 증가(Identity) 설정
    foreach (var entityType in modelBuilder.Model.GetEntityTypes()
        .Where(e =>
            e.ClrType != null &&
            e.ClrType.IsSubclassOf(typeof(BaseEntity))
            && e.GetTableName() != "__EFMigrationsHistory"

            )) {



      // foreach (var entityType in modelBuilder.Model.GetEntityTypes()
      //     .Where(e => e.ClrType.IsSubclassOf(typeof(BaseEntity))))
      // {
      // modelBuilder.Entity(entityType.ClrType)
      //     .ToTable(entityType.ClrType.Name.ToLower(), "jsini"); // 테이블명과 스키마를 명시적으로 설정

      modelBuilder.Entity(entityType.ClrType)
          .Property(nameof(BaseEntity.Id))
          .UseIdentityByDefaultColumn();

      // BaseEntity에 새로 추가된 속성을 모든 하위 엔티티에 적용합니다.
      // modelBuilder.Entity(entityType.ClrType)
      //     .Property(nameof(BaseEntity.RemoteAddress))
      //     .HasColumnType("varchar(50)"); // 데이터베이스 컬럼 타입을 명시적으로 지정합니다.
    }


    foreach (var entity in modelBuilder.Model.GetEntityTypes()) {
      // 테이블명 소문자화
      var tableName = entity.GetTableName();

      if (entity.GetTableName() == "__EFMigrationsHistory") {
        continue; // 마이그레이션 히스토리 테이블이 왜 잡히나?
      }


      if (!string.IsNullOrEmpty(tableName)) {
        entity.SetTableName(tableName.ToLower());
      }

      if (xmlDoc != null) {
        // 클래스(테이블) 주석 설정
        var typeMemberName = $"T:{entity.ClrType.FullName}";
        var typeSummary = xmlDoc.Descendants("member")
            .FirstOrDefault(m => m.Attribute("name")?.Value == typeMemberName)?
            .Element("summary")?.Value.Trim();

        if (!string.IsNullOrEmpty(typeSummary)) {
          entity.SetComment(typeSummary);
        }
      }

      foreach (var property in entity.GetProperties()) {
        // 컬럼명 소문자화
        property.SetColumnName(property.Name.ToLower());

        if (xmlDoc != null) {
          // 속성(컬럼) 주석 설정
          var propMemberName = $"P:{entity.ClrType.FullName}.{property.Name}";
          var propSummary = xmlDoc.Descendants("member")
              .FirstOrDefault(m => m.Attribute("name")?.Value == propMemberName)?
              .Element("summary")?.Value.Trim();

          if (!string.IsNullOrEmpty(propSummary)) {
            property.SetComment(propSummary);
          }
        }
      }

    }

  }
}
