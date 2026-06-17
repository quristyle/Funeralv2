# GOLDB v3: Next-Generation B2B Jewelry Supply Chain Platform (AI 지침서)

> [!IMPORTANT]
> 이 파일은 AI 에이전트의 개발 연속성과 맥락 유지를 위해 생성되었습니다.
> **새로운 대화 세션이 시작될 때 AI는 이 파일을 가장 먼저 읽고 인지한 상태에서 작업을 시작해야 합니다.**
> 모든 설명, 소통 및 코드 주석은 **한국어**를 기본 언어로 사용합니다.

---

## 1. 프로젝트 정체성 (Identity)
* **프로젝트명**: GOLDB v3 (골든바)
* **도메인**: 차세대 B2B 주얼리 공급망 플랫폼 (Next-Generation B2B Jewelry Supply Chain Platform)
* **목적**: 주얼리 제조/유통 공급망 내의 제품 정보 관리, 실시간 원자재(금, 다이아몬드) 시세 반영 가격 산정, 파트너사 주문 및 여신(미수금) 관리 등을 처리하는 통합 관리 대시보드 및 API 시스템입니다.

---

## 2. 프로젝트 디렉터리 레이아웃 (Directory Layout)
```text
goldenbar/
├── GoldbApi/               # .NET 8.0 Minimal API 백엔드
│   ├── Data/               # Entity Framework DbContext
│   ├── Models/             # DB 엔티티 및 비즈니스 도메인 모델
│   ├── DTOs/               # 요청/응답 데이터 전송 객체
│   ├── Endpoints/          # Minimal API 매핑 레이어 (Controller 대체)
│   ├── Services/           # 핵심 비즈니스 로직 서비스 레이어
│   ├── Validators/         # FluentValidation 유효성 검사 파일들
│   └── Migrations/         # PostgreSQL 마이그레이션 이력
├── goldenbar_front/        # Vue 3 / Vite 프론트엔드
│   ├── src/
│   │   ├── icons/svg/      # SVG 스프라이트용 아이콘 폴더
│   │   └── ...             # 컴포넌트, 라우터, 상태(Pinia) 폴더
│   └── vite.config.ts      # Vite 빌드 및 DevServer 설정
├── clean_migration.py      # EF Core 마이그레이션 파일 내 bulk 데이터 제거 스크립트
├── goldenbar.sln           # 루트 솔루션 파일
└── AI.md                   # [본 파일] AI용 지침 및 컨텍스트 파일
```

---

## 3. 프론트엔드 아키텍처 및 개발 표준 (`goldenbar_front`)

### 3.1 기술 스택
* **Core**: Vue 3.5+, TypeScript (TypeScript 5.8+), Vite 6.3+
* **UI & Styling**: Element Plus v2.10+, TailwindCSS v4.0+, Sass (SCSS)
* **State & Routing**: Pinia v2.3+, Vue Router v4.5+
* **Data & Tools**: Axios (API 통신), ECharts v5.6+ (통계 시각화), XLSX/JSZip (엑셀 임포트/익스포트), Driver.js (초기 사용자 가이드)
* **Dev Tools**: `vite-plugin-vue-devtools` v8.1+ (**Antigravity IDE 연동 구성 완료**)

### 3.2 개발 및 환경 설정
* **개발 서버 포트**: `5252` (`http://localhost:5252`)
* **프록시(Proxy) 구성**:
  * `/api` -> `http://localhost:5077` (백엔드 API 서버로 타겟팅 및 헤더에 `VITE_COOKIE` 주입 가능)
  * `/uploads` -> `http://localhost:5077/uploads` (주얼리 업로드 이미지 정적 서빙 경로)
* **아이콘**: `src/icons/svg` 경로의 SVG 파일들을 `svg-sprite` 플러그인을 사용하여 `icon-[name]` 포맷으로 스프라이트하여 로드합니다.
* **에디터 연동**: `VueDevTools({ launchEditor: 'antigravity' })`로 설정되어 있어, 브라우저의 Component Inspector를 통해 코드 바로가기 시 **Antigravity IDE**로 자동 로드됩니다.

### 3.3 프론트엔드 개발 규칙
1. **컴포넌트 작성**: Vue 3 `<script setup lang="ts">` 스타일의 Composition API를 최우선으로 적용합니다.
2. **UI 컴포넌트**: `unplugin-vue-components` 및 `unplugin-auto-import`를 사용하여 Element Plus 컴포넌트는 수동 import 없이 사용합니다.
3. **스타일링**: TailwindCSS v4 표준 유틸리티를 활용하고, 고유 디자인 요소는 필요에 따라 SCSS를 병행 사용합니다.
4. **다국어**: `vue-i18n` 모듈이 통합되어 있으므로 다국어 지원 텍스트 템플릿 구조를 유지해야 합니다.
5. **테이블 내 아이콘 버튼 스타일**:
   * 테이블 컬럼(예: `관리`, `액션` 등) 내에 렌더링되는 아이콘 버튼은 테두리가 없는 깔끔한 **무테 디자인**(`link` 속성을 지정한 텍스트 형태)을 기본으로 적용합니다.
   * 브라우저의 기본 포커스 아웃라인(outline)이나 포커스 링으로 인해 검은색 네모 테두리가 표시되지 않도록, 클릭/포커스 시 `outline: none`, `box-shadow: none` 처리를 필수로 정의해야 합니다.
   * 인접한 아이콘들과 균형을 이룰 수 있도록 내부 `.el-icon` 혹은 SVG의 `font-size`(기본 15px 권장) 규격을 일관되게 고정하여 크기를 통일합니다.

---

## 4. 백엔드 아키텍처 및 개발 표준 (`GoldbApi`)

### 4.1 기술 스택
* **Framework**: .NET 8.0 (SDK Web)
* **ORM & Query**: Entity Framework Core 8.0, Dapper (조회 성능 극대화용 경량 ORM 병행)
* **Database**: PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`)
* **Security & Auth**: JWT (JSON Web Token) Bearer 인증, BCrypt.Net-Next (비밀번호 암호화)
* **Validation**: FluentValidation (Request DTO 유효성 강제)
* **Logging**: Serilog (Console & File Sinks 탑재)
* **API Documentation**: Swagger/Swashbuckle
* **Others**: SixLabors.ImageSharp (주얼리 이미지 리사이징), Spectre.Console (화려한 CLI 로깅 화면 제공)

### 4.2 아키텍처 구성 방식 (Minimal APIs)
백엔드는 전통적인 Controller 방식 대신, .NET 8의 성능 중심 **Minimal APIs** 구조를 채택하여 파일 단위로 엔드포인트를 매핑합니다.
1. **엔드포인트 등록**: `Endpoints` 디렉터리에 각 도메인별 매퍼 클래스(예: `AuthEndpoints.cs`, `ProductEndpoints.cs`)를 분리하고 `Program.cs`에서 `GoldbApi.Endpoints.XEndpoints.MapXEndpoints(app)` 형태로 등록합니다.
2. **비즈니스 서비스**: `Services` 디렉터리에 인터페이스(`IProductService`)와 구현체(`ProductService`)를 구분하고, `Program.cs`에서 **Scoped**로 의존성 주입(DI) 처리합니다.
3. **유효성 검사**: FluentValidation 검사기를 어셈블리 스캔 방식으로 `Program.cs`에 일괄 자동 등록합니다.

### 4.3 핵심 DB 및 모델 패턴
* **공통 스키마 및 마이그레이션**: 마이그레이션 히스토리 테이블은 PostgreSQL의 `goldb` 스키마 내에 생성됩니다. (`migrationsHistoryTable("__EFMigrationsHistory", "goldb")`)
* **BaseModel 및 자동 메타데이터 트래킹**:
  * 모든 주요 모델은 `BaseModel`을 상속받습니다.
  * EF Core `AppDbContext` 내부의 `OnBeforeSaving` 가로채기(Interceptor) 패턴을 통해, 데이터의 **소프트 딜리트(Soft Delete - `IsDeleted = true`)** 및 생성/수정 시간(`CreatedAt`, `UpdatedAt`), 생성/수정자(`CreatedBy`, `UpdatedBy`)가 JWT 클레임과 **`ICurrentUserService`** 기반으로 **자동 저장**됩니다. (과거의 `IHttpContextAccessor` 강결합을 해소하기 위해 도메인 계층에 `ICurrentUserService` 인터페이스를 도입하여 의존성을 역전시켰습니다.)
* **엔티티 매핑 (Entity Configuration) 분리 규칙**:
  * `AppDbContext.cs` 파일의 `OnModelCreating` 메서드가 비대해지는 것을 방지하기 위해 **모든 개별 엔티티의 매핑(HasKey, HasOne 등)은 `GoldbApi/Data/Configurations/` 폴더 하위의 `IEntityTypeConfiguration<T>` 구현체로 분리하여 작성해야 합니다.**
  * 작성된 Configuration은 `OnModelCreating` 내부에서 `modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);`를 통해 자동으로 일괄 등록됩니다.
* **주요 도메인 모델 관계**:
  * **주얼리 제품군**: `Product` (단품 정보, 중량, 14k/18k/24k 함량), `ProductSet` (세트 상품 구성), `Catalog` (카탈로그 매칭)
  * **실시간 가격 책정**: 원자재 시장 시세 실시간 반영을 위한 `GoldPrice`, `DiamondPrice` 모델 연계
  * **B2B 비즈니스**: `Company` (거래처 정보), `Receivable` (외상/미수금 거래 현황), `Order` / `OrderItem` / `OrderStatusHistory` (주문 및 주얼리 제작 공정 이력)
  * **주얼리 생산 특화**: 주얼리 왁스/석고 캐스팅 주문 관리를 위한 `PlasterOrder` 모델 적용

### 4.4 데이터 액세스 레이어 및 Repository 패턴
* **Repository 패턴 의무화**:
  * 백엔드 비즈니스 서비스 구현 시 `AppDbContext`를 직접 주입받아 DB 엑세스를 직접 처리하지 않고, 데이터 액세스 레이어를 추상화한 **Repository**를 경유하도록 설계합니다.
* **제네릭 리포지토리 (`IRepository<T>`)**:
  * 단순 CRUD 및 표준 데이터 엑세스는 Generic Repository인 `IRepository<T>`와 `RepositoryBase<T>`를 주입받아 수행합니다. (예: `IRepository<Article>`)
  * `IRepository<T>`는 기본 메서드(`GetByIdAsync`, `GetAllAsync`, `AddAsync`, `Update`, `Delete`, `SaveChangesAsync`, `GetQueryable`)를 공통 제공합니다.
* **커스텀 리포지토리 (Custom Repository)**:
  * 다중 Join, 동적 검색 조건 및 페이징, 복잡한 DTO 프로젝션(`.Select(...)` 쿼리 등)이 필요한 경우, `IRepository<T>`를 확장하는 전용 인터페이스(예: `IOrderRepository`)와 `RepositoryBase<T>`를 확장하는 구현 클래스(예: `OrderRepository`)를 정의하여 비즈니스 서비스 레이어가 얇고(Thin) 명확해지도록 개발합니다.
* **집계성 데이터 처리 표준 (Materialized View)**:
  * **(추천)** 대시보드 통계나 정산 요약 등 `Sum`, `Count`, `GroupBy`와 같은 무거운 집계 연산이 필요한 경우, 실시간 쿼리(On-demand Aggregation)로 인한 DB 병목 및 메모리 초과를 방지하기 위해 **PostgreSQL의 Materialized View(구체화된 뷰)**를 최우선적으로 활용해야 합니다.
  * `AppDbContext`에 등록된 `mv_...` 뷰 모델을 조회하는 방식으로 아키텍처를 구성하며, 데이터 동기화는 백그라운드 워커(`BackgroundService`)를 통한 주기적 리프레시(Eventual Consistency) 방식을 따릅니다.

### 4.5 객체 매핑 표준 (Mapster)
* **Mapster 도입 목적**:
  * 엔티티(Entity) 모델에서 DTO(Data Transfer Object)로 변환 시 장황한 LINQ `.Select()` 수동 매핑으로 인한 보일러플레이트 코드를 최소화하고 일관된 매핑 정책을 강제하기 위해 **Mapster**를 도입하였습니다.
* **매핑 및 프로젝션 규칙**:
  * **전체 목록/페이징 조회 (Projection)**: `IQueryable` 쿼리 체이닝의 마지막 단계에 `.ProjectToType<TDto>()`를 호출하여 필요한 컬럼만 SELECT 쿼리에 반영되도록 최적화합니다. (불필요한 `.Include()` 선언 방지 및 DB 성능 극대화)
  * **단일 객체 매핑**: 개별 엔티티를 DTO로 변환할 때는 `entity.Adapt<TDto>()` 확장 메서드를 사용합니다.
* **중앙 매핑 구성 등록 및 레지스트리 분리 (Assembly Scanning)**:
  * **(중요)** 하나의 `MappingConfig.cs` 파일에 모든 매핑 로직을 작성하지 마십시오.
  * 새로운 DTO 매핑이나 변환 규칙이 추가될 경우, 해당 도메인에 맞는 레지스트리 클래스(예: `GoldbApi/Mappings/Registers/UserMappingRegister.cs`)를 생성하고 **`IRegister`** 인터페이스를 상속받아 `config.NewConfig<TSource, TDest>()` 형식으로 작성합니다.
  * `MappingConfig.cs`는 오직 `TypeAdapterConfig.GlobalSettings.Scan(Assembly.GetExecutingAssembly());` 한 줄만 유지하여, 어셈블리 내의 모든 `IRegister` 구현체를 런타임에 자동 수집하도록 설계되었습니다.

### 4.6 유효성 검사 표준 (Validation Filter)
* **FluentValidation 자동화**: Minimal API의 엔드포인트 핸들러 내부에서 수동으로 유효성 검사를 수행하지 않고, **엔드포인트 필터(Endpoint Filter)**를 사용하여 자동화합니다.
* **필터 적용 방식**:
  * 엔드포인트 정의 시 `.WithValidation<T>()` 확장 메서드를 호출하여 해당 요청 DTO에 대한 검증을 강제합니다.
  * 예: `group.MapPost("/", ...).WithValidation<CreateRequestDto>();`
* **에러 응답**: 검증 실패 시 `ApiResponse<string>.Failure` 형식을 사용하여 표준화된 **400 Bad Request** 응답(에러 코드 포함)을 즉시 반환합니다.
* **파일 위치**:
  * 필터 구현: `GoldbApi/Filters/ValidationFilter.cs`
  * 도메인별 검사기: `GoldbApi/Validators/*.cs`

### 4.7 전역 예외 처리 표준 (Global Error Handling)
* **일관된 에러 봉투(Error Envelope)**: 백엔드에서 발생하는 모든 처리되지 않은 예외(Unhandled Exceptions)는 **전역 예외 처리 미들웨어**에 의해 포착됩니다.
* **표준 응답 포맷**: 예외 발생 시 HTTP 상태 코드 500과 함께 `ApiResponse<object>` 형식의 JSON을 반환하여 프론트엔드(`request.ts`)가 예외 상황에서도 일관된 데이터 구조를 수신하도록 보장합니다. (기본 에러 코드: 50000)
* **로깅 및 보안**: 모든 예외는 **Serilog**를 통해 서버 측에 상세 로그로 기록되지만, 클라이언트에게는 보안을 위해 상세 스택 트레이스 대신 표준 안내 메시지만 노출합니다.
* **파일 위치**: `GoldbApi/Middlewares/GlobalExceptionMiddleware.cs`

---

## 5. 프로젝트 특화 유틸리티
* **`clean_migration.py`**:
  * Entity Framework에서 마이그레이션 생성 시 시드 데이터가 너무 비대해져 마이그레이션 코드 파일 크기가 늘어나는 현상을 해소하는 Python 스크립트입니다.
  * 마이그레이션 코드 내의 `InsertData()` 및 `DeleteData()` 구문을 정규식을 이용해 제거해 주는 전처리 도구입니다.

---

## 6. 개발 실행 가이드 (터미널 명령어)
AI가 코드를 빌드, 실행 및 모니터링할 때 아래 표준 명령어를 사용합니다.

* **백엔드 실행**:
  ```bash
  cd /home/quri/projects/goldenbar/GoldbApi
  dotnet run
  ```

백엔드는 항상 수동실행한다. 만일 빌드나 기타 확인처리를 위해 동작시키게 된다면 작업이 끝났을때 백엔드를 종료시켜라.

  *(정적 파일 업로드 경로는 설정에 따라 `/home/lee/goldb_storage/uploads` 등으로 지정되어 자동 생성 및 서빙됩니다.)*

* **프론트엔드 테스트 환경 실행**:
  ```bash
  cd /home/quri/projects/goldenbar/goldenbar_front
  npm run dev:test
  ```

* **프론트엔드 빌드**:
  ```bash
  npm run build:prod
  ```

---

## 7. 대화의 연속성 메모리 (AI가 세션 종료 시 작성할 영역)

> [!TIP]
> AI 에이전트는 하나의 작업(피처 구현, 버그 수정 등)을 마치고 사용자에게 세션 완료를 브리핑하기 전, **반드시 아래 영역에 업데이트 이력과 다음에 이어서 진행할 작업을 추가하고 저장해야 합니다.**

### 7.1 최근 변경 이력 (History)
* **[2026-06-12]**:
  * **대규모 집계 성능 최적화 (CQRS / Materialized View 도입 준비)**: `DashboardService` 등에서 발생하는 실시간 통계 쿼리로 인한 DB 병목 및 N+1 문제를 방지하기 위해, PostgreSQL의 `MATERIALIZED VIEW` 기반 설계 문서(`docs/2026-06-12_MaterializedView_도입_및_아키텍처_문서.md`)를 작성하고 `AI.md` 가이드라인에 반영.
  * **프론트엔드/백엔드 전수 조사 및 문서화**: 전체 코드 리뷰 및 개선 필요 사항을 분석하여 `docs` 하위에 4종의 MD 파일 생성.
  * **프론트엔드 UI 컴포넌트화**: 
    * 장바구니 등 다수 화면에서 쓰이는 고객 검색 팝업을 `CustomerSelectDialog.vue`로 독립.
    * 주문, 시세, 발주, 권한 등 4개 화면의 복잡한 내장 팝업을 독립 컴포넌트로 분리하고 부모-자식 간 통신(Event/Props) 최적화 적용.
  * **프론트엔드 세트 상품 UI 개선**:
    * 세트 상품 구성품 목록 테이블에 항량, 컬러, 중량, 가격(공장도/수공비) 등 상세 정보 컬럼 추가 및 레이아웃(그룹화, 태그, 말줄임표) 최적화.
    * 세트 상품 자체의 수공비 관리 필드 추가 및 자동 합산 힌트 UI 구현.
  * **백엔드 아키텍처 리팩토링 (DI 및 의존성 역전)**:
    * 데이터 액세스 및 비즈니스 서비스 계층에서 `IHttpContextAccessor`에 강결합된 구조를 해소하기 위해 `ICurrentUserService` 도메인 인터페이스 도입 및 전면 교체(총 11개 서비스 및 `AppDbContext`).
  * **백엔드 EF Core 및 Mapster 구조 개선 (모듈화)**:
    * `AppDbContext.OnModelCreating`의 비대화 문제를 해결하기 위해 `GoldbApi/Data/Configurations` 폴더를 신설하고 모든 엔티티 매핑을 `IEntityTypeConfiguration<T>`로 분리 후 어셈블리 스캔 방식으로 일괄 등록 적용.
    * 단일 `MappingConfig.cs` 파일에 몰려있던 Mapster 룰을 도메인별 `IRegister` 클래스로 분리(`Mappings/Registers/` 폴더)하여 어셈블리 스캔 방식으로 재구성함으로써 확장성 확보 및 보일러플레이트 제거.
  * **백엔드 JSON 직렬화 최적화 (JinRestApi 동기화)**: `GoldbApi/Program.cs`의 JSON 옵션에 순환 참조 방지(`ReferenceHandler.IgnoreCycles`) 및 Enum 문자열 변환(`JsonStringEnumConverter`) 설정을 추가하여 API 응답의 안정성과 가독성 개선.
  * 제조사 대시보드에 '분류별 주문현황' 차트 섹션 추가 및 Y축 정수 표기 최적화.
  * 백엔드 유효성 검사 아키텍처 개선: Minimal API 엔드포인트 필터를 통한 FluentValidation 자동화 (`ValidationFilter` 도입).
  * 백엔드 전역 예외 처리 도입: 모든 미처리 오류를 `ApiResponse` 형식으로 반환하는 `GlobalExceptionMiddleware` 적용.
  * 전사적 DTO 매핑 자동화 완료: Mapster를 사용하여 8개 이상의 서비스 레이어 리팩토링 및 수동 할당 코드 제거.
  * AI 가이드문서 `AI.md`에 유효성 검사(4.6), 전역 예외 처리(4.7), Mapster(4.5), Entity Configuration(4.3) 표준 명시.
* **[2026-06-11]**:
  * 백엔드 아키텍처 개선을 위해 Generic Repository 패턴 도입 (`IRepository<T>`, `RepositoryBase<T>` 구현).
  * `OrderService`, `StockService`, `ReceivableService`에 대해 각각 커스텀 리포지토리(`IOrderRepository`, `IStockRepository`, `IReceivableRepository`)를 적용하여 DB Context 결합도 해소.
  * `ArticleService`, `NoticeService`, `FavoriteService` 등 단순 CRUD 서비스에 `IRepository<T>` 적용.
  * `DashboardService` 카테고리 매핑 로직의 중복 키 예외(`System.ArgumentException`) 수정.
  * 백엔드 DTO 매핑 최적화를 위해 Mapster 도입 및 `MappingConfig.cs` 구성.
  * 서비스 계층 및 엔드포인트의 DTO 변환 로직을 Mapster 프로젝션/매핑으로 전환.
  * AI 가이드문서 `AI.md`에 Repository 아키텍처 규칙(4.4) 및 Mapster 매핑 규칙(4.5) 명시.
* **[2026-05-22]**:
  * Antigravity IDE 2.0 및 새로운 Antigravity IDE 버전 업그레이드 연동 완료.
  * VueDevTools의 `launchEditor` 연동을 위한 시스템 심볼릭 링크`/usr/bin/antigravity`를 새 설치 경로(`/home/quri/Downloads/AntigravityIDE/antigravity-ide`)로 마이그레이션 성공.
  * 전체 프로젝트 분석을 거쳐 AI용 맥락 안내서 `AI.md` 파일 최초 수립 및 작성 완료.

### 7.2 진행 대기 중인 다음 작업 (TODO List)
- [ ] 1. 새로 적용한 Antigravity IDE에서 실제로 VueDevTools Component Inspector를 실행하여 클릭 시 코드 파일(`vite.config.ts` 및 일반 `.vue` 파일)이 정상적으로 포커싱 및 로드되는지 연동 테스트 진행.
- [ ] 2. 백엔드 및 프론트엔드 실행 환경에서 추가 피처 구현 지침 대기.
