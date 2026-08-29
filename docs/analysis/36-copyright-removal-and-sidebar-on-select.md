# 저작권 표시 걷어내기 · 메뉴 클릭 뒤 사이드바 동작

작업일: 2026-08-28

---

## 1. 저작권 정보를 모두 지웠다

> 지시: "preferences-panel.vue 에는 저작권 정보 표시 사용과 기타 정보를 입력하는
> 부분이 있다. 저작권 정보를 모두 제거하라."

### 무엇이 들어 있었나

기본값이 **vben 것 그대로**였다. 회사명 `Vben`, 사이트 `vben.pro`, 그리고
**중국 ICP 등록번호** `闽ICP备19024351号` + `beian.miit.gov.cn` 링크.
`enable` 이 `true` 라 이 제품 화면에 그대로 나갈 수 있는 상태였다.

| 어디 | 상태 |
|---|---|
| 로그인 화면 | `copyright.enable` 이 켜져 있어 **표시된다** |
| 본문 푸터 | `footer.enable` 이 꺼져 있어 지금은 안 보인다 |
| 환경설정 패널 | 표시 여부 · 회사명 · 사이트 · 날짜 · ICP · ICP 링크 입력칸 |

### 지운 것

```
basic/copyright/                       (표시 컴포넌트 · 폴더째)
widgets/preferences/blocks/layout/copyright.vue   (설정 블록)
preferences-panel.vue                  Block · defineModel 7개 · import
basic/layout.vue                       푸터 안의 <Copyright>
authentication/authentication.vue      로그인 화면 3곳 + copyright prop
authentication/form.vue                푸터 슬롯 자리
@core/preferences  config.ts · types.ts            기본값과 타입
packages/preferences/src/index.ts      appCopyrightPreferences (중국 ICP 기본값)
```

코드에 남은 `copyright` 참조는 **0건**이다.

**다국어 파일(`packages/locales/.../preferences.json`)의 `copyright` 항목은 남겼다.**
그쪽은 vben 상위와 맞춰 두는 영역이라 지우면 다음 동기화 때 충돌만 늘고,
남아도 쓰이지 않는 문구일 뿐이다.

푸터 자체는 남겼다 — 넣을 내용이 생기면 그 자리에 넣으면 된다.

---

## 2. 메뉴를 고른 뒤 사이드바를 어떻게 할지 (`sidebar.onMenuSelect`)

> 지시: "왼쪽 사이드바에서 메뉴 클릭 시 사이드바 자동숨김 스위치를 만들어 줘."
> 이어서: "사이드바는 축소 · 완전히숨기기 · 보이기 로 구분할 수 있다.
> 그대로유지 · 축소 · 완전히숨기기 로 제공되는 게 좋을 듯하다."

처음에는 켜고 끄는 스위치로 만들었는데, 지적대로 **사이드바 상태가 셋**이라
스위치로는 모자랐다. 고르는 칸으로 바꿨다.

| 고른 값 | 하는 일 | 되돌리는 법 |
|---|---|---|
| `none` 그대로 유지 (**기본**) | 아무 것도 하지 않는다 | — |
| `collapse` 축소 | 아이콘만 남긴다 (`sidebar.collapsed`) | 접기 버튼 · '마우스 올리면 펼치기' |
| `hide` 완전히 숨기기 | 사이드바가 사라진다 (`sidebar.hidden`) | **헤더 왼쪽 햄버거** |

**새 상태를 만들지 않았다.** 축소도 숨기기도 이미 있는 값을 그대로 쓴다 —
`hide` 가 쓰는 `sidebar.hidden` 은 헤더 햄버거(`toggleSidebar()`)가 바꾸는 바로 그 값이라,
다시 보이게 하는 방법도 원래 쓰던 것 그대로다.

기본값은 `none` 이라 **켜기 전과 똑같이 동작한다.**

### 어디에 걸었나

`layout.vue` 의 세로 메뉴(`mode="vertical"`)에만 건다. 상단 가로 메뉴에서 고른 것까지
접으면 사이드바를 쓰지도 않은 사람이 사이드바가 사라지는 것을 본다.

디렉터리를 펼치는 클릭은 `@open` 이고 `@select` 는 **실제로 이동하는 항목**에서만
오므로 따로 걸러 낼 것이 없다.

---

## 3. 확인한 것

개발 서버에서 실제로 눌러 확인했다. 사이드바 안쪽 폭으로 쟀다
(바깥 `aside` 는 224px 로 유지되고 안쪽이 줄어든다).

| | 결과 |
|---|---|
| 환경설정 패널 | '저작권' 블록 **없음**, 사이드바 블록에 '메뉴 클릭 시 사이드바' 추가 |
| 고를 수 있는 값 | 그대로 유지 · 축소 · 완전히 숨기기 |
| `축소` 로 두고 메뉴 클릭 | 안쪽 폭 **224 → 60px**, `collapsed: true` · `hidden: false` |
| `완전히 숨기기` 로 두고 메뉴 클릭 | 안쪽 폭 **224 → 1px**, `hidden: true` |
| 그 상태에서 헤더 햄버거 | **224px 로 복귀** (지시대로 같은 버튼으로 되돌아온다) |
| `그대로 유지` | 아무 변화 없음 |
| 콘솔 | 오류 없음 |

`pnpm vite build` 통과 · eslint 새 오류 없음.

시험하며 바꾼 설정(`onMenuSelect` · `collapsed` · `hidden`)은 **원래대로 되돌려 두었다.**

### 시험하며 알게 된 것

설정은 **서버에 사용자별로 저장**되므로(`account_preferences`) localStorage 를
직접 고쳐도 다음 조회 때 서버 값으로 덮인다. 그래서 시험도 화면에서 직접 골라서 했다.

---

## 4. 상위(vben)와 갈라지는 부분

둘 다 `fronts/packages` 를 건드린다. 저작권은 **기능을 통째로 걷어낸 것**이라
다음 상위 동기화 때 그쪽 파일이 되살아날 수 있다. 그때는 이 문서를 보고 다시 지운다.

`sidebar.onMenuSelect` 는 **더한 것**이라 충돌 위험이 작다.

---

## 5. 글꼴 목록에 나눔스퀘어라운드를 더했다

> 지시: "환경설정의 기본글꼴 선택 대상에 '나눔스퀘어라운드' 도 추가해 줘.
> CDN 을 쓰지 않고 폐쇄망에서도 되도록 내려받아 서비스하고 있다."

준수사항 5(글꼴은 저장소 안의 파일만 쓴다)를 그대로 따라 **파일을 받아 넣었다.**

- 받은 곳: `https://hangeul.pstatic.net/hangeul_static/webfont/NanumSquareRound/`
  (네이버 '한글한글 아름답게' 공식 웹폰트 배포처)
- 넣은 것: `L`(300) · `R`(400) · `B`(700) 세 무게, **woff2 만** — 합계 약 670KB

**woff2 만 넣은 이유** — 배포처에는 eot·woff·ttf 도 있지만 이 포털이 도는
브라우저는 전부 woff2 를 읽는다. woff 를 함께 넣으면 용량이 두 배가 된다.
ExtraBold 는 화면에서 쓸 자리가 없어 넣지 않았다.

**`@font-face` 이름을 셋 다 `NanumSquareRound` 하나로 뒀다.** 배포처 CSS 는
무게마다 다른 이름(`NanumSquareRoundB` 등)을 쓰는데, 그러면 `font-weight` 로
굵기가 바뀌지 않아 굵게 표시해야 할 곳이 굵어지지 않는다.

고친 곳은 셋이다 — `styles/fonts.css`(선언) · `styles/font.ts`(글꼴 묶음 표) ·
`blocks/theme/font-family.vue`(선택 목록). 이용 조건은 `public/fonts/README.md`
에 다른 글꼴과 같은 형식으로 적어 두었고, 다시 받는 명령도 함께 남겼다.

### 확인한 것

| | 결과 |
|---|---|
| 파일 | 세 개 모두 매직바이트 `wOF2`, 크기가 배포처 값과 일치 |
| 브라우저 | `/fonts/NanumSquareRoundR.woff2` **200**, `document.fonts` 에 300·400·700 세 벌 등록 |
| 선택 목록 | `S-CoreDream · 나눔스퀘어라운드 · Play · 시스템 설정 따름` |
| 고른 뒤 | `body` 와 `--font-family` 가 `NanumSquareRound` 우선으로 바뀜 |
| antd 컴포넌트 | 같이 따라감(테마 토큰 경로도 `font.ts` 를 쓴다) |
| 빌드 산출물 | `dist/fonts/` 에 세 파일 그대로 들어감 |

시험하며 바꾼 글꼴 설정은 **원래대로(S-CoreDream) 되돌려 두었다.**
