# CargoTrust — JSini 운송관리 (화물 거래처 신뢰정보)

거래 전에 거래처의 **실제 결제 이력**을 보고, 거래 후 결제 결과를 남겨 다음 사람이 참고하게 한다.
블랙리스트가 아니다 — 판정어 없이 숫자와 기간만 보여 준다.

| 문서 | |
|---|---|
| [01-service-overview.md](01-service-overview.md) | 상위 설계안(원본) |
| [05-api-design.md](05-api-design.md) | **API 계약** — 백엔드와 두 MFE 의 약속 |

## 구성

| | 자리 | 주소 |
|---|---|---|
| MSA | `microservices/CargoTrustServer` | :5500 (루프백), 게이트웨이 `/api/cargotrust/*` |
| 사용자 MFE | `web/src/Apps/JSini.Web.CargoTrust` | 포털 `/cargotrust` · 열쇠 `cargotrust.*` |
| 관리자 MFE | `web/src/Apps/JSini.Web.CargoTrust.Admin` | 포털 `/cargoadmin` · 열쇠 `cargoadmin.*` |
| DB | `cargotrust` DB · `cargotrust` 스키마 · `cargotrust` 역할 | jin114.co.kr:31015 |

`dev.bat cargo` 로 띄운다. 스키마는 EF 가 아니라 SQL 이 만든다(마이그레이션 없음).

## SQL — 순서대로

| 파일 | DB | 상태 (2026-09-24) |
|---|---|---|
| `deploy/sql/cargotrust-database-2026-09-24.sql` | superuser | **적용함** — 역할·DB·스키마 |
| `deploy/sql/cargotrust-schema-2026-09-24.sql` | cargotrust | **적용함** — 테이블 9 + pg_trgm |
| `deploy/sql/portal-menu-cargotrust-2026-09-24.sql` | jsiniportal/scom | **적용함** — 메뉴 21줄 + 역할 권한(관리자 두 역할만 켬) |

## 운영 배치 (2026-09-24 끝남, 커밋 fd623895)

1. `/srv/jsini/config/CargoTrustServer/appsettings.Local.json` — `ConnectionStrings:cargotrust`
   (Host `host.docker.internal`, Port 31015). 없으면 컨테이너가 DB 에 못 붙는다.
2. `/srv/jsini/config/ApiGateway/appsettings.Local.json` 에 `cargotrust-cluster` →
   `http://cargo:8080` 을 더하고 **gateway 를 재시작**했다. 빠뜨리면 전 경로가 502 다
   (compose 머리 주석 참고 — 환경변수로는 먹지 않았다). 고치기 전 사본은 같은 폴더의
   `appsettings.Local.json.bak-20260924-cargo`.
3. 메뉴 SQL 적용. 다른 역할에 열려면 권한 화면(`/admin/auth`)에서 켠다.
4. 남은 확인: 로그인해서 `/cargotrust` → 거래처 등록 → 거래 등록 → 결제 등록 → 상세 통계를 한 번 지나 본다.
