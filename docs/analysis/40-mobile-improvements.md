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
