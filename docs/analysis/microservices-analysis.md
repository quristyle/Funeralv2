# Funeralv2 마이크로서비스 분석 보고서

> 분석 범위: `microservices/` 디렉터리 전체 (.NET 백엔드 4개 서비스 + 공유 라이브러리 3개)
> 기준 문서: `/home/quri/Funeralv2/AI.md` (Repository 패턴 의무화, Materialized View 표준 등)
> 작성일: 2026-08-14

---

## 1. 개요

Funeralv2는 장례식장 관리 및 디지털 사이니지(전광판) 표출 시스템으로, 다음 4개의 독립 .NET 8 마이크로서비스와 3개의 공유 라이브러리로 구성됩니다. 모든 서비스는 **Minimal API + PostgreSQL(Npgsql/EF Core)** 조합을 사용합니다.

| 서비스 | 책임 | TFM | 주요 스택 | DB(스키마) | 포트 |
|---|---|---|---|---|---|
| **AuthServer** | 인증/JWT 발급, 사용자·회사·부서·역할·권한·메뉴·공통코드·i18n 관리 | net8.0 | EF Core 8.0.0, Npgsql 8.0.0, JwtBearer 8.0.0, Mapster | `jsinicore` (scom) | (미지정) |
| **funeralv2Api** | 핵심 비즈니스 API: 건물/층/호실/장비/미디어소스/고인(deceased) 관리, SignalR 실시간 장비 제어 | net8.0 | EF Core 8.0.11, Npgsql, **SignalR**, Serilog, FluentValidation, Dapper(미사용) | `funeralv2` (smfr) | 5320 |
| **AIAgentServer** | LLM 프록시(OpenAI 호환): 공통코드 추천, i18n 번역, 챗/스트리밍 | net8.0 | HttpClient, 자체 DB 없음 | 없음 | (미지정) |
| **FileServer** | 파일 업로드/저장, ffmpeg 트랜스코딩, ImageSharp 썸네일 생성 | net8.0 | EF Core 8.0.11, SixLabors.ImageSharp, Serilog, FluentValidation | `jsinifileconn` (scom) | 5350 |

**공유 라이브러리 (`Common/`)**
- `Funeralv2.Shared.Domain`: `BaseEntity<TKey>` — Id/CreatedAt/CreatedBy/UpdatedAt/UpdatedBy/IsDeleted(소프트 삭제) 공통 필드 (`Common/Funeralv2.Shared.Domain/BaseEntity.cs`).
- `Funeralv2.Shared.DTOs`: `ApiResponse<T>` 표준 응답 봉투 + `ErrorDetail`, Mapster 설정 (`ApiResponse.cs`, `MapsterConfig.cs`).
- `Funeralv2.Shared.Infrastructure`: `GlobalExceptionMiddleware`, `ApiResponseFilter`(Minimal API 결과 자동 래핑) (`GlobalExceptionMiddleware.cs`, `Filters/ApiResponseFilter.cs`).

---

## 2. 아키텍처 및 서비스 간 관계

```
                    [ Frontend / 사이니지 플레이어 ]
                       |         |          |
        (JWT 발급)     |         | (SignalR /hubs/device)
   AuthServer <--------+         |
                                 v
   AIAgentServer <----(직접호출)-- funeralv2Api  ---- HTTP ----> FileServer
        |                            |  (썸네일 URL 조회 GET /group/{id})
        v                            |
   외부 LLM(OpenAI 호환 API)          +-- HTTP 상태 콜백 <---- FileServer
```

- **인증 흐름**: AuthServer가 `/login`에서 JWT를 발급(`AuthServer/Endpoints/AuthEndpoints.cs:19`). 발급 토큰은 `Issuer=funeralv2-auth`, `Audience=funeralv2-services`로 서비스 간 공용 설계이나, **실제로 토큰을 검증하는 서비스는 AuthServer 자신뿐**입니다(3장·5장 참고).
- **서비스 간 통신**: funeralv2Api → FileServer는 순수 HTTP 호출로 썸네일 URL을 조회(`funeralv2Api/Services/BuildingService.cs:224-235`). FileServer는 트랜스코딩 완료 후 상태를 HTTP 콜백으로 통지(`FileServer/Services/FileService.cs:1061`). 서비스 디스커버리/게이트웨이 없이 URL 설정 기반 직접 호출.
- **실시간 통신**: `DeviceHub`(`funeralv2Api/Hubs/DeviceHub.cs`)가 장비 등록/해제, 30초 유예 오프라인 판정, 재접속 감지를 담당. `DeviceStatusCleanupService`(BackgroundService)가 미응답 ONLINE 장비를 주기적으로 정리하고, 앱 기동 시 잔류 ONLINE을 일괄 초기화(`funeralv2Api/Program.cs`).
- **데이터**: 각 서비스가 별도 스키마/DB를 소유(DB per service 지향). 연결 문자열은 `appsettings.Local.json`(gitignore 처리됨, `.gitignore:18`)에서 주입.

---

## 3. 강점 (잘 되어 있는 점)

1. **일관된 Minimal API + 엔드포인트 그룹화 패턴**: `MapGroup` + 서비스별 확장 메서드(`MapBuildingEndpoints` 등)로 라우팅이 깔끔하게 분리됨(`funeralv2Api/Endpoints/BuildingEndpoints.cs`). 계층(Endpoints/Services/DTOs/Entities) 분리가 4개 서비스에 걸쳐 일관됨.
2. **표준 응답 봉투 + 자동 래핑**: `ApiResponse<T>`와 `ApiResponseFilter`가 성공/실패/페이징 응답 구조(`{result, page:{total}}`)를 자동 통일하여 프론트엔드 계약이 일관됨(`Common/Funeralv2.Shared.DTOs/ApiResponse.cs`, `Filters/ApiResponseFilter.cs`).
3. **공통 예외 처리 미들웨어**: `UseGlobalExceptionHandler()`가 모든 서비스에 동일 적용되어 미처리 예외를 표준 포맷 + TraceId로 반환(`GlobalExceptionMiddleware.cs`).
4. **공통 도메인 베이스**: `BaseEntity`의 소프트 삭제(`IsDeleted`) + 감사 필드 표준화. 실제 쿼리에서 `!b.IsDeleted` 필터 일관 적용.
5. **SignalR Hub 설계 견고성**: `ConcurrentDictionary` 기반 스레드 안전 매핑, 재접속 시 오프라인 타이머 취소, 30초 유예 판정 등 연결 불안정 상황을 실무적으로 처리(`funeralv2Api/Hubs/DeviceHub.cs`).
6. **구조적 로깅(Serilog)**: funeralv2Api/FileServer는 Serilog + `UseSerilogRequestLogging()`로 요청 로깅 및 부트스트랩 로깅 적용.
7. **비밀 설정 분리 원칙 시도**: 연결 문자열을 `appsettings.Local.json`로 분리하고 gitignore 처리(`.gitignore:18`).

---

## 4. 개선점 (우선순위별)

### 우선순위 높음 (Critical)

#### 4.1 비밀번호 평문 저장/비교 — 인증 근간 결함
- **문제**: 로그인 시 비밀번호를 해시 없이 평문 문자열로 직접 비교.
- **근거**: `AuthServer/Services/AuthService.cs:39` `account.Password != request.Password`, 동일 로직 `AuthServer/Endpoints/AuthEndpoints.cs:27`. 코드베이스 전체에 BCrypt/PBKDF2/SHA 등 해싱 흔적 전무(grep 결과 0건).
- **개선방안**: BCrypt.Net 또는 ASP.NET Core `PasswordHasher<T>`로 즉시 전환. 기존 계정은 마이그레이션 스크립트로 재해싱. 로그인 실패 응답 시간 일정화로 타이밍 공격 완화.

#### 4.2 비즈니스/파일/AI 서비스에 인증이 전혀 적용되지 않음
- **문제**: funeralv2Api, FileServer, AIAgentServer는 `Microsoft.AspNetCore.Authentication.JwtBearer` 패키지를 참조하면서도 `AddAuthentication`/`UseAuthentication`/`UseAuthorization`를 **호출하지 않으며**, 어떤 엔드포인트에도 `RequireAuthorization()`이 없음. 즉 건물/장비/고인 정보 CRUD, 파일 업로드, LLM 호출이 **전부 무인증 공개**.
- **근거**: grep 결과 `AddAuthentication`/`RequireAuthorization`은 오직 `AuthServer/Program.cs:33,82-83`, `AuthServer/Endpoints/AuthEndpoints.cs:71,82`에만 존재. `funeralv2Api/Program.cs`·`FileServer/Program.cs`·`AIAgentServer/Program.cs`에 인증 미들웨어 없음.
- **개선방안**: 각 리소스 서비스에 JwtBearer 인증(공용 Issuer/Audience 검증) 구성 추가 → `app.UseAuthentication(); app.UseAuthorization();` → 엔드포인트 그룹에 `.RequireAuthorization()`. 공유 라이브러리에 `AddFuneralJwtAuth()` 확장 메서드를 만들어 4개 서비스 동일 적용. SignalR 허브도 `[Authorize]` 및 액세스 토큰 쿼리스트링 처리.

#### 4.3 JWT 시크릿·VAPID 개인키가 소스에 하드코딩되어 커밋됨
- **문제**: 프로덕션 JWT 서명 키가 소스에 평문 상수로 존재하고 3개 서비스에서 동일 문자열을 공유. VAPID **개인키**도 커밋됨.
- **근거**:
  - `AuthServer/appsettings.json` `JwtSettings:SecretKey = "a-very-secret-key-that-is-long-enough-for-security"`.
  - `FileServer/appsettings.json`, 코드 폴백 `AuthServer/Program.cs:29`, `AuthService.cs:66`, `AuthEndpoints.cs:35`에 동일 문자열.
  - `funeralv2Api/appsettings.json`의 `Vapid.PrivateKey`, `Vapid.PublicKey` 및 `appsettings.Development.json`의 개발용 JWT 키까지 커밋.
- **개선방안**: 모든 시크릿을 환경변수/시크릿 매니저(예: dotnet user-secrets, KeyVault, `appsettings.Local.json`)로 이전하고 커밋본에서 제거. 이미 노출된 키는 **로테이션 필수**. `git` 히스토리 정리 검토.

#### 4.4 Repository 패턴 미준수 — AI.md 핵심 표준 위반
- **문제**: AI.md는 "비즈니스 서비스에서 `AppDbContext`를 직접 주입받지 말고 Repository를 경유"하도록 **의무화**하나, 실제로는 24개 전 서비스가 `AppDbContext`/`FileDbContext`를 직접 주입해 EF 쿼리를 수행. `IRepository<T>`·`RepositoryBase<T>` 자체가 코드베이스에 존재하지 않음.
- **근거**: grep `IRepository|RepositoryBase` 매칭 0건. DbContext 직접 주입 확인 예: `funeralv2Api/Services/BuildingService.cs:17-24`, `AuthServer/Services/AuthService.cs:26`, `FileServer/Services/FileService.cs` 등 `*/Services/*` 전부.
- **개선방안**: `Funeralv2.Shared.Infrastructure`에 제네릭 `IRepository<T>`/`RepositoryBase<T>`(GetByIdAsync/GetAllAsync/AddAsync/Update/Delete/SaveChangesAsync/GetQueryable) 신설. 복합 조회는 커스텀 리포지토리로 확장. 신규/변경 코드부터 점진 적용.

#### 4.5 CORS 설정이 자격증명 노출 위험
- **문제**: funeralv2Api가 `SetIsOriginAllowed(_ => true)` + `AllowCredentials()` 조합을 사용. 이는 임의 Origin을 그대로 반사(reflect)하면서 쿠키/자격증명을 허용하는 사실상 최악의 조합으로, CSRF/자격증명 탈취에 취약.
- **근거**: `funeralv2Api/Program.cs`의 `AllowAll` 정책(`SetIsOriginAllowed(_ => true).AllowCredentials()`). AIAgentServer/FileServer는 `AllowAnyOrigin()`(자격증명 없음이라 상대적으로 덜 위험하나 여전히 전면 개방).
- **개선방안**: 허용 Origin 화이트리스트를 구성에서 주입. 자격증명이 필요하면 명시적 Origin 목록만 허용, 불필요하면 `AllowCredentials` 제거.

#### 4.6 미처리 예외의 전체 스택트레이스가 클라이언트로 노출
- **문제**: `GlobalExceptionMiddleware`가 환경 구분 없이 `exception.ToString()` 전문을 응답 `realmessage`로 반환 → 프로덕션에서 내부 구조/경로/스택 정보 노출.
- **근거**: `Common/Funeralv2.Shared.Infrastructure/GlobalExceptionMiddleware.cs` `realMessage: exception.ToString()`.
- **개선방안**: `IsDevelopment()`에서만 상세 노출, 프로덕션은 TraceId만 반환하고 상세는 로그로. 예외 타입별 상태코드 매핑(404/400/409 등) 추가.

#### 4.7 연결 문자열이 표준 출력으로 유출
- **문제**: 부팅 시 DB 연결 문자열(자격증명 포함 가능)을 `Console.WriteLine`으로 그대로 출력하는 디버그 코드가 잔존.
- **근거**: `funeralv2Api/Program.cs:54` `Console.WriteLine($"aaaaaaaaaaaaaaaaaaaaaaaaaaa funeralv2api connectionString: {connectionString}");`.
- **개선방안**: 즉시 삭제.

### 우선순위 중간 (Major)

#### 4.8 테스트 부재
- **문제**: 단위/통합 테스트 프로젝트가 하나도 없음(`*test*.csproj` 0건). 인증·권한 필터·SignalR 상태 전이 등 회귀 위험이 높은 로직이 무검증.
- **개선방안**: 최소한 AuthService(해시/토큰), ApiResponseFilter, DeviceHub 상태 전이에 대한 xUnit 테스트 도입. CI 게이트 연결.

#### 4.9 헬스체크 부재
- **문제**: 어떤 서비스에도 `AddHealthChecks`/`MapHealthChecks`가 없어 오케스트레이터(K8s/로드밸런서) readiness/liveness 프로빙 및 DB 연결 상태 노출 불가.
- **근거**: grep `HealthCheck|MapHealth` 0건.
- **개선방안**: 각 서비스에 `/health`(liveness), `/health/ready`(DB·의존 서비스 포함) 추가. `AspNetCore.HealthChecks.NpgSql` 활용.

#### 4.10 `new HttpClient()` 남용 — 소켓 고갈 위험 + 회복탄력성 부재
- **문제**: 서비스 간/외부 HTTP 호출에서 `IHttpClientFactory` 대신 `using var client = new HttpClient()`를 매 호출 생성. 소켓 고갈(SNAT exhaustion) 및 재시도/타임아웃/서킷브레이커 부재.
- **근거**: 8개 지점 — `funeralv2Api/Services/BuildingService.cs:225`, `DeceasedService.cs:1125,1165`, `MediaSourceService.cs:176,396,443,491`, `FileServer/Services/FileService.cs:1026`.
- **개선방안**: `IHttpClientFactory`(named/typed client)로 전환하고 Polly로 재시도/타임아웃/서킷브레이커 정책 부여. AIAgentServer는 이미 `AddHttpClient()`를 쓰므로 이를 표준으로 확산.

#### 4.11 FileServer의 광범위한 `Console.WriteLine` 로깅
- **문제**: FileServer가 ffmpeg 처리 전 과정을 `Console.WriteLine`으로 출력(약 50여 개소). Serilog를 구성해 놓고도 정작 파일 처리 로그는 구조화되지 않아 상관관계·레벨·집계 불가.
- **근거**: `FileServer/Services/FileService.cs` 전반(예: :139, :218, :338, :1061 등 다수), `FileServer/Endpoints/FileEndpoints.cs:74,90`. 그 외 `AuthServer/Services/UserService.cs:402` 등.
- **개선방안**: `ILogger<T>` 구조적 로깅으로 일괄 치환, 적정 레벨(Debug/Warning/Error) 부여.

#### 4.12 Materialized View 표준 미적용
- **문제**: AI.md는 대시보드/정산 등 집계에 PostgreSQL Materialized View(`mv_...`) + BackgroundService 리프레시를 최우선으로 권고하나 코드베이스에 `mv_` 뷰나 `FromSql` 집계가 전무.
- **근거**: grep `mv_|Materialized|FromSql` 0건.
- **개선방안**: 집계성 화면이 도입/존재한다면 MV 모델을 `AppDbContext`에 등록하고 주기적 `REFRESH MATERIALIZED VIEW`를 BackgroundService로 구성.

#### 4.13 서비스 간 설정·버전·매핑 불일치
- **문제**:
  - JWT 설정 키 상이: AuthServer는 `JwtSettings:SecretKey`, funeralv2Api/FileServer는 `Jwt:Key` (`AuthServer/Program.cs:29` vs `funeralv2Api/appsettings.json`).
  - 패키지 버전 드리프트: EF/Npgsql `8.0.0`(AuthServer) vs `8.0.11`(funeralv2Api/FileServer), Spectre.Console `0.55.2` vs `0.57.0`, AIAgentServer는 net8 대상에 `System.Net.Http.Json 10.0.9`(비정상 버전).
  - 매핑 방식 혼재: AuthServer 일부는 Mapster `.Adapt<>` 사용(`CompanyService.cs` 등), funeralv2Api는 장문의 수기 매핑(`BuildingService.cs`에서 동일 매핑 5회 반복).
  - AuthServer만 Serilog 미적용(기본 로깅).
- **개선방안**: `Directory.Packages.props`로 중앙 버전 관리, 공용 JWT 설정 키/확장 메서드 통일, Mapster 매핑을 funeralv2Api로 확산해 보일러플레이트 제거.

#### 4.14 FileServer 기동 시 자동 마이그레이션 + 하드코딩 경로
- **문제**: FileServer가 부팅 시 `context.Database.Migrate()`를 실행(프로덕션 자동 스키마 변경 위험). 저장 경로가 특정 개발자 홈으로 하드코딩.
- **근거**: `FileServer/Program.cs`(DB 초기화 블록), `FileServer/appsettings.json` `Storage.LocalPath = "/home/lee/funeralv2_storage"`, `FallbackUrl` 하드코딩. funeralv2Api의 FileServer URL 폴백도 하드코딩(`BuildingService.cs` `?? "http://localhost:5350"`).
- **개선방안**: 마이그레이션은 배포 파이프라인에서 분리 실행. 경로/URL은 환경 구성으로 외부화.

### 우선순위 낮음 (Minor)

- **API 버저닝 부재**: 라우트에 `/v1` 등 버전 세그먼트가 없어 하위호환 관리 어려움. Asp.Versioning 도입 검토.
- **미사용 의존성**: funeralv2Api가 `Dapper`를 참조하나 사용처 없음(grep 0건) → 제거.
- **JWT 만료 정책**: 액세스 토큰 7일 고정, 리프레시 토큰 없음(`AuthService.cs:80`, `AuthEndpoints.cs:50`). 짧은 액세스 + 리프레시 토큰 도입 권고.
- **서명 알고리즘 키 인코딩**: `Encoding.ASCII.GetBytes(key)` 사용(`Program.cs:30`, `AuthService.cs:67`) — UTF8 및 충분한 키 길이 권장.
- **AIAgentServer Swagger 비활성**: `app.UseSwagger()`가 주석 처리됨(`AIAgentServer/Program.cs`) — 의도라면 명시, 아니면 복구.
- **거대 서비스 클래스**: `FileService.cs`(1555줄), `DeceasedService.cs`(1267줄) — 책임 분할 및 리포지토리 도입 시 자연 감소 기대.
- **N+1 조회**: `BuildingService.GetBuildingsAsync`가 건물마다 FileServer를 개별 HTTP 호출(`BuildingService.cs:57-64`) — 배치 조회 또는 캐싱 검토.

---

## 5. 보안 점검 요약

| 항목 | 상태 | 근거 |
|---|---|---|
| 비밀번호 해싱 | ❌ 평문 비교 | `AuthService.cs:39`, `AuthEndpoints.cs:27` |
| 리소스 서비스 인증 | ❌ 전면 무인증 | funeralv2Api/FileServer/AIAgentServer Program.cs (인증 미들웨어 없음) |
| 시크릿 관리 | ❌ 소스 커밋 | `AuthServer/appsettings.json`, `FileServer/appsettings.json`, `funeralv2Api/appsettings.json`(Vapid 개인키) |
| CORS | ⚠️ 과도 개방 | funeralv2Api `SetIsOriginAllowed(_=>true)+AllowCredentials`; 타 서비스 `AllowAnyOrigin` |
| 예외 정보 노출 | ⚠️ 스택 전문 반환 | `GlobalExceptionMiddleware.cs` `exception.ToString()` |
| 민감정보 로깅 | ⚠️ 연결문자열 stdout | `funeralv2Api/Program.cs:54` |
| 비밀 분리 원칙 | ✅ Local.json 분리 | `.gitignore:18` |
| HTTPS 리다이렉트 | ✅ 적용(리소스 서비스) | 각 `Program.cs` `UseHttpsRedirection()` |
| 업로드 크기 제한 | ⚠️ 500MB 전면 허용 | funeralv2Api/FileServer Kestrel/FormOptions — 파일 서비스 외에는 과도 |

**최우선 조치**: (1) 비밀번호 해싱, (2) 리소스 서비스 JWT 인증 활성화, (3) 커밋된 시크릿 로테이션·외부화, (4) CORS 화이트리스트, (5) 예외 상세 노출 차단.

---

## 6. 종합 권고

이 시스템은 **Minimal API 구조화, 표준 응답 봉투, 공통 예외 미들웨어, 견고한 SignalR 상태 관리** 등 애플리케이션 골격은 성숙하고 일관성이 좋습니다. 그러나 **보안 기반(인증·시크릿·비밀번호)이 심각하게 미비**하고, 프로젝트가 스스로 정한 **AI.md의 두 핵심 표준(Repository 패턴, Materialized View)이 전혀 적용되지 않은** 상태입니다.

권고 로드맵:

1. **즉시(보안 긴급)**: 비밀번호 해싱 도입 → 리소스 서비스 JWT 인증 활성화(공유 확장 메서드) → 시크릿 외부화·로테이션 → CORS 화이트리스트 → 예외 상세 노출/연결문자열 로그 제거.
2. **단기(표준 정합성)**: 공유 `IRepository<T>`/`RepositoryBase<T>` 도입 및 신규 코드 적용, 헬스체크 추가, `IHttpClientFactory`+Polly 전환, `Console.WriteLine` → `ILogger` 치환.
3. **중기(품질·운영)**: 테스트 프로젝트 신설과 CI 게이트, 중앙 패키지 버전 관리(`Directory.Packages.props`), 매핑 방식 Mapster 통일, 집계 화면에 Materialized View 적용, API 버저닝 및 서비스 디스커버리/게이트웨이 검토.

> 참고: `AuthServer`는 인증·JWT를 올바르게 구성한 유일한 서비스이므로, 그 인증 부트스트랩 코드를 공유 라이브러리로 승격해 나머지 3개 서비스에 재사용하는 것이 가장 비용 효율적인 출발점입니다.
