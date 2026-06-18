
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
