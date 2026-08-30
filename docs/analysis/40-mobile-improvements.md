# 40. 모바일 대응 — jsini-portal

2026-08-30. 포털은 PWA(39번 문서)로 모바일에서도 업무를 본다. 실기기 뷰포트(375×812)
조작 + 코드 전수 점검으로 찾은 문제들을 4단계로 고쳤다. 점검 상세(무엇이 왜 문제였나)는
이 문서 끝의 점검 요약을 본다.

## 방침 하나 — 준수사항 4("한 화면")는 데스크톱 한정

세로가 짧은 모바일에서 "한 화면에 담기"를 우기면 그리드가 수십 px 로 압착되거나
컨테이너를 뚫었다(실측: 날씨 기록 화면 그리드 84px). 그래서 **모바일(<768px,
vben isMobile 기준)에서는 이 규칙을 풀고 페이지 세로 스크롤을 허용한다.**
`.page-fill-last` 를 `@media (min-width:768px)` 안으로 옮긴 것이 전부라(37개 화면 일괄),
데스크톱은 그대로다.

## 1단계 — 전역 수정 묶음 (화면 개별 수정 없이 일괄 적용)

| 수정 | 자리 | 내용 |
|---|---|---|
| 핀치 확대 허용 | index.html | `user-scalable=0` 제거 — 14px 글자에 확대 수단이 없었다 |
| 규칙 4 데스크톱 한정 | src/styles/index.css | 위 방침 |
| vxe 모바일 보정 | src/adapter/vxe-table.ts `adjustGridForMobile` | `height:'auto'` → 500(고정), 고정열(fixed) 해제. 셋업 시점 판정 — 회전은 화면 재진입 시 반영 |
| 모달 전체 화면 | src/bootstrap.ts | 모바일이면 `setDefaultModalProps({ fullscreen:true })`. 상자는 프레임워크가 clamp 하지만 내부 고정폭까지는 못 구해서다. 부팅 시점 판정 |
| DatePicker 보정 | src/adapter/component/index.ts `withMobileReadonlyInput` | 모바일에서 `inputReadOnly` — 소프트 키보드와 달력 패널이 동시에 뜨던 것 차단 |
| 드래그 모달 터치 | src/plugins/draggable-modal.ts | `touch-action:none` + `setPointerCapture` + pointercancel 정리 |
| 100dvh | packages/effects/layouts/basic/layout.vue | 주소창 접힘으로 하단이 잘리던 것. CSS.supports 폴백 |
| 탭바 데스크톱 전용 | 같은 파일 | 모바일에서 38px 탭바 숨김 + 높이 계산에서도 제외. 화면 전환은 드로어가 맡는다 |

검증(375×812 실측): 날씨 기록 그리드 84→638px + 페이지 스크롤 동작, 탭바 사라짐,
문의내역 고정열 제거, 상세 모달 375×812 전체 화면.

## 2단계 — 터치에서 사라졌던 기능 복구

드래그·hover·더블클릭은 전부 **데스크톱용으로 유지**하고, 터치로 닿는 경로를 병행 추가했다.
새 API 는 만들지 않았다 — 전부 각 화면이 이미 쓰던 저장 경로 재사용이다.

| 화면 | 터치 경로 |
|---|---|
| helpdesk 일정 | 완료/삭제 버튼 모바일 상시 노출(`max-md:opacity-100`) · 칸 한 번 탭 = 날짜 선택(하이라이트) 후 [일정 등록] · 날짜 변경은 편집 팝업의 시작/종료일(이미 있었음) |
| projmng 일정표 | 같은 탭-선택 패턴 + [일정 등록] 버튼 신설 · 날짜 변경은 AppointmentForm(이미 있었음) |
| 조직도(org-chart) | 노드마다 [이동] 버튼 → 대상 선택 팝업(자기·하위 제외, 회사 직속 포함) · **터치 팬/핀치 줌 추가**(기존 캔버스가 마우스 전용이라 모바일에서 노드에 닿을 수 없었다) |
| 회사-사용자 배정 | 행마다 [배정]/[해제] 버튼 (드롭 핸들러를 함수로 추출해 공유) |
| 사용자-역할 | 역할 태그 × 로 해제 · 역할 서랍 [+] 로 지정 |
| 고정 탭 순서 | 항목마다 ↑/↓ 버튼 (favoriteStore.reorder 재사용) |
| 장식 배치(장례) | 썸네일 [+] 버튼 → 화면 중앙 배치, 위치는 속성 패널로 미세조정 |
| 업로드 순서 | sortablejs 가 터치 지원(길게 눌러 드래그, delayOnTouchOnly 기존 설정) — `touchStartThreshold: 5` 만 보강 |
| 메뉴 관리 셀 편집 | 변경 없음 — 작업 열 [수정] 버튼이 모든 인라인 필드를 포함하는 폼을 연다(근거 주석만 추가) |
| projmng DynamicGrid | 변경 없음 — 좌측 연필 버튼이 dblclick 과 동일 조건으로 항상 함께 그려진다 |
| AI 챗 삭제 버튼 2곳 | 모바일 상시 노출 |

## 3단계 — 모바일 회선 성능

- **모나코 배럴 누수 차단**: views/projmng/shared/index.ts 가 CodeEditor 를 재수출해
  배럴을 스치는 projmng 화면 27개 전부가 모나코(수 MB)를 받았다. 재수출을 걷어내고
  실제 사용 화면 9개만 `#/components/code-editor` 직수입으로 바꿨다.
- **부팅 차단 제거**: bootstrap 의 BizSelect 설정 프리로드를 fire-and-forget 으로.
  스토어 getConfig 가 미적재 시 스스로 기다리므로 예열일 뿐이었다.

## 4단계 — 고정폭 정리

데스크톱(≥768px) 렌더링이 변하지 않는 수단(flex-wrap · max-w · 미디어쿼리)만 썼다.

- **기상 도구줄 4화면**(dashboard·forecast·warning·events): `flex-wrap` — 375px 에서 버튼이 줄바꿈된다 (전에는 overflow-hidden 에 잘려 접근 불가)
- **클램프 없던 팝업**: notice-popup · video-embed-modal 의 ant `:width="620"` → `min(620px, calc(100vw-32px))`, funeral notice-custom Card 팝업에 max-w/max-h, preview-custom DID 미리보기는 실물 비율 유지가 필요해 **가로 스크롤 감싸개** 채택
- **공용 셀렉트**: BizSelect·DictSelect 의 `min-width !important` 를 모바일에서 해제 + `max-width:100%`
- **조회조건 대표 3화면**(room-usage · request/edit · util/diagram): 고정폭에 `max-width:100%` 병기

에이전트가 발견했으나 목록 밖이라 남긴 것(다음 순번): preview-custom 상단 도구줄,
request/edit 유형 Select(160px), util/diagram 프로젝트 Select(190px),
life history·standards 내부 Select 의 max-w 병기.

## 5단계 — 화면 크기별 메뉴목록 노출 (2026-08-30 추가)

작은 화면에서 메뉴목록이 길어지는 문제는 위 1~4단계가 손대지 않았다.
데스크톱에서만 쓸모 있는 화면(넓은 그리드·조직도·배치 편집)까지 휴대폰에 그대로 나온다.
메뉴마다 **어느 크기의 메뉴목록에 보일지**를 정할 수 있게 했다.

| 값 | 뜻 | 기본 |
|---|---|---|
| `use_mobile` | 휴대폰 크기(<768px) 메뉴목록에 보일지 | true |
| `use_tablet` | 태블릿 크기(768~1023px) 메뉴목록에 보일지 | true |

크기 경계는 이 문서가 이미 쓰던 기준을 따른다 — 휴대폰은 vben `isMobile`(md 미만),
태블릿은 tailwind md 이상 lg 미만. **데스크톱(≥1024px)은 이 값과 무관하게 늘 보인다.**

### 거르는 것은 목록뿐이다

**라우트는 그대로 만든다.** 휴대폰 메뉴목록에서 빠진 화면도 주소로 직접 들어가거나
즐겨찾기·고정 탭으로 열면 열린다. 목적이 "목록을 짧게" 이지 "못 들어가게" 가 아니어서다 —
못 들어가게 하려면 `status = 0`(비활성)이나 역할 권한을 쓴다. 뜻이 다른 장치다.

그래서 백엔드가 걸러 내려주지 않고 화면이 거른다. `/auth/menu/all` 은 두 값을
`meta` 에 실어 **전부** 내려주고, 라우트를 다 만든 뒤 사이드바 목록만 걸러 낸다.

### 자리

| 무엇 | 자리 |
|---|---|
| 컬럼 추가 | `docs/sql/menu_mobile_tablet.sql` (jsiniportal · scom, 반복 실행 안전) |
| 엔티티·DTO | AuthServer `Entities/SystemMenu.cs` · `DTOs/SystemMenuDto.cs`(관리) · `DTOs/MenuDto.cs`(사이드바) |
| 거르기 | `apps/jsini-portal/src/router/menu-size-visibility.ts` (+ 같은 이름의 테스트) |
| 연결 | 같은 폴더 `access.ts`(규칙 수집·갱신) · `guard.ts`(최초 로그인) |
| 관리 화면 | `views/portal/system/menu/` — 그리드 두 칸(눌러서 켜고 끔) · 등록/수정 폼 체크박스 둘 |

**규칙을 API 응답에서 미리 걷어 두는 이유**: 사이드바가 쓰는 `MenuRecordRaw` 는
`generateMenus`(packages/utils)가 만드는데 이때 우리가 붙인 meta 값이 떨어져 나간다.
프레임워크(packages)를 고치지 않으려고, 응답에서 규칙을 걷어 두고 경로·링크·번역키·이름
넷을 열쇠로 나중에 맞춰 본다.

**빈 묶음은 함께 뺀다.** 자식이 모두 빠진 디렉토리는 눌러도 아무것도 없으므로 같이 뺀다.

**크기가 바뀌면 다시 거른다.** 거르기 전 목록을 들고 있다가 `matchMedia` 변경에
다시 건다 — 기기 회전·창 크기 조절·개발자 도구 기기 모드에서 다시 로그인할 필요가 없다.

### 검증

- `docs/sql/menu_mobile_tablet.sql` 실행 — 메뉴 177건 전부 기본값 true.
- `/menu/all` · `/system/menu/list` 두 응답 모두 177건에 값이 실린다(누락 0).
- 저장 왕복(끔 → 폰만 켬 → 원복)을 DB 로 확인. 같은 저장에서 권한 항목(`use_excel`)도 안 바뀐다.
- `menu-size-visibility.test.ts` 4건 통과 — 데스크톱 전부 노출 / 휴대폰·태블릿이 서로 다른
  값을 본다 / 값 없는 옛 메뉴는 어디서나 보인다 / 빈 묶음 제거.
- `pnpm vite build` 통과.

### 함께 고친 것 — 인라인 편집이 권한 항목을 지우던 문제

`views/portal/system/menu/list.vue` 의 `toUpdatePayload` 가 `permissions` 를 빼고 보냈다.
서버는 안 실려 온 요청을 "기본값으로 저장" 으로 다루므로, **셀 하나만 고치거나 상태 배지를
한 번 눌러도 그 메뉴의 권한 항목 설정(`use_cust1~8` 과 이름)이 기본값으로 되돌아갔다.**
새로 넣는 두 값이 같은 경로로 저장되므로 함께 고쳤다 — 한 줄이다.

## 프레임워크 수정 — 상위 동기화 주의 (D-M1)

layouts/basic/layout.vue 둘(100dvh · 탭바 모바일 숨김)은 **fronts/packages 수정**이다.
17번 문서 규칙대로 상위(vbenjs/vue-vben-admin)에 같은 수정이 있는지 확인하려 했으나
GitHub 코드 검색이 미인증이라 못 봤다. **다음 상위 동기화 때 이 둘을 충돌 목록에
넣어야 한다** (17번 문서에서 이 문서를 참조).

## 검증

- `pnpm vite build` 통과. 모나코 분리 확인: 일정표(scheduler) 청크의 editor.api 참조 0건.
- 375×812 실측: 대시보드 도구줄 버튼 전부 뷰포트 안(줄바꿈), 일정 화면 탭-선택 →
  등록 팝업에 날짜 반영(에이전트가 브라우저로 확인), 조직도 터치 팬 요소 확인.
- 데스크톱(≥768) 회귀 없음: page-fill-last 채움(그리드 734px·페이지 스크롤 0),
  탭바 복원, vxe 고정열 복원, 모달 720px(전체 화면 아님).

## 결정 대기

- **D-M1**: 프레임워크 수정 둘(100dvh · 탭바 모바일 숨김)의 상위(vbenjs) 반영 여부 확인 —
  GitHub 코드 검색이 미인증이라 못 봤다. 다음 상위 동기화(17번 문서) 때 충돌 목록에 포함.
- **D-M2**: 조직도 [이동] 실행에 확인창을 둘지 — 지금은 드래그와 동일하게 즉시 실행
  (대상 선택 팝업이 사실상 확인 단계). 부서 이동은 파급이 커서 confirm 을 얹을 수 있다.
- **D-M3**: 사용자-역할 [+] 지정도 확인 없이 즉시 실행(드래그와 동일). 권한 부여라
  오탭 방지 confirm 여부는 운영 판단.
- **D-M4**: 조직도 핀치 줌은 화면 중심 보정 없는 단순 배율(기존 휠 줌과 동일 수준).
  더 다듬을지는 사용해 보고 판단.
