# jsini-site — 회사 소개 사이트

`www.jsini.co.kr`. 로그인하지 않은 사람이 보는 유일한 프론트다.

```bash
pnpm -C fronts/apps/jsini-site dev        # http://localhost:5556/ko
pnpm -C fronts/apps/jsini-site build      # dist/ 에 정적 HTML 10장
pnpm -C fronts/apps/jsini-site typecheck
```

`dev` 는 `/api` 를 게이트웨이(:5265)로 넘긴다. 자료는 SiteServer(:5480)에서 온다.

---

## 왜 포털과 따로인가

`apps/jsini-portal` 은 로그인한 사람이 오래 머무는 업무 화면이고, 이 앱은 처음 온 사람이
20초 안에 판단하는 공개 화면이다. 요구되는 것(SEO · OG 태그 · 첫 화면 속도 · 익명 쓰기 방어)이
반대라서 한 앱에 넣으면 양쪽이 서로를 망친다.

**`@vben/*` 를 하나도 쓰지 않는다.** 같은 pnpm 워크스페이스에 있는 것은 node · pnpm ·
tailwind 도구 체인을 재사용하기 위한 것뿐이다. `fronts/packages` 는 상위 vben 과 계속
맞춰 나가는데, 그때마다 회사 대표 사이트가 흔들려서는 안 된다.

## 빌드가 정적 HTML 인 이유

`vite-ssg` 가 라우트를 돌며 HTML 을 미리 만든다 (결정 D-S1). 결과는 파일 묶음이고
서버 런타임이 없다 — nginx·CDN 어디든 올라간다.

프리렌더할 주소는 `src/router/routes.ts` 의 `prerenderRoutes` 가 정한다.
**뉴스 상세(`/news/:slug`)는 뺐다.** 넣으려면 빌드 때 API 를 불러 slug 를 받아야 하고,
그러면 배포가 API 상태에 묶인다(서버가 잠깐 죽으면 빌드 실패). 검색 노출이 중요해지면
그 대가를 받아들이고 넣는다.

`<html lang>` · `<title>` · `description` · `og:title` 은 `vite.config.ts` 의 `onPageRendered`
가 언어별로 채운다. 자바스크립트가 돌기 전에 맞아 있어야 하는 값이라 화면 코드에서
`document.title` 을 바꾸는 것으로는 늦다 (SNS 미리보기는 JS 를 아예 돌리지 않는다).

## 언어

주소가 언어를 정한다 (`/ko/...` · `/en/...`). 정적 프리렌더라 빌드 시점에는 스토어도
브라우저도 없고 주소만 있다. 쿠키나 `Accept-Language` 로 고르면 정적 파일 하나로
두 언어를 담을 수 없다.

화면 고정 문구는 `src/i18n/messages.ts` 표 하나다. vue-i18n 을 얹지 않았다 —
문구 대부분은 DB(`site.sections`)에서 오고, 코드에 남는 것은 메뉴 이름 정도다.
언어를 늘릴 때는 `LOCALES` 에 코드를 더하면 라우트와 프리렌더 목록이 함께 늘어난다.
DB 쪽 언어 값도 같은 코드를 쓴다(SiteServer 의 `NormalizeLocale`).

## 브랜드

색·글꼴·로고는 [`docs/brand/README.md`](../../../docs/brand/README.md) 가 정한다.
여기서 새로 만들지 않는다.

- `public/brand/` — `docs/brand/` 에서 복사한 것이다. 원본을 고쳤으면 다시 복사한다.
- 헤더·푸터 로고는 `components/brand-logo.vue` 에 좌표를 인라인해 두었다.
  밝은 배경과 어두운 배경에서 색이 달라야 하고(2톤 · 녹아웃), 첫 화면에 무조건 필요해서다.
  **좌표를 여기서 고치지 않는다** — `docs/brand/generate.py` 를 고치고 다시 뽑아 옮긴다.
- 각진 스타일이라 `styles/index.css` 가 `border-radius` 를 전역에서 0 으로 눌러 둔다.

## 글꼴 — 준수사항 5

바깥 CDN 을 쓰지 않는다. `public/fonts/` 의 파일만 쓴다.
포털의 같은 폴더에서 복사했고, 넷 중 **둘만** 가져왔다(300 Light · 700 Bold) —
하나에 350KB 라 전부 넣으면 첫 화면이 느려지고, 이 사이트가 쓰는 무게는 그 둘뿐이다.
무게가 더 필요하면 포털 폴더에서 파일을 더 복사하고 `styles/index.css` 에 `@font-face` 를 더한다.

확인: 개발자 도구 네트워크에 `location.origin` 밖으로 나가는 요청이 하나도 없어야 한다.

## 준수사항 4 는 이 앱에 해당되지 않는다

'세로 스크롤 없이 한 화면' 은 업무 화면 규칙이다. 소개 사이트는 스크롤이 서술 수단이다.
`docs/준수사항.md` 규칙 4 의 예외 절에 적혀 있다.

## 아직 안 한 것

- **문의 양식** — 접수 API(`POST /api/site/inquiries`)는 준비됐지만 화면은 열지 않았다.
  개인정보 수집·이용 동의 문구가 확정돼야 한다(결정 D-S7). `views/contact.vue` 주석에
  그때 함께 넣을 것을 적어 두었다.
- **문구** — `site.sections` 가 비어 있어 홈·회사소개의 블록이 나오지 않는다.
  정보구조와 문구는 D-S8 · D-S9 다.
- **본문 마크다운 렌더링** — 지금은 그대로 보여 준다. 렌더러를 붙일 때는 정제(sanitize)를
  함께 넣어야 한다. 관리 화면에서 쓴 글이라도 HTML 로 넣는 순간 XSS 경로가 된다.
