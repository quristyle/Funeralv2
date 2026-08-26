# 회사 소개 사이트 · JSINI 브랜드

작성: 2026-08-26
관련: [docs/brand/](../brand/) · [12-decisions-pending.md](12-decisions-pending.md)

> 지시
> - 회사에 사용할 로고 · 그래픽 · 모션영상을 만들고, 그것을 기준하여 회사를 소개하는
>   웹페이지도 만들려고 한다.
> - 지금 프론트는 인증을 해야만 쓰는 업무 화면이다. 소개 사이트는 누구나 볼 수 있고,
>   공개된 자료를 내려받을 수 있고, 연락을 위한 글을 남길 수 있어야 한다.
> - 매우 깔끔하면서 각진 스타일. 검정 · 회색 · 흰색의 조합으로 고급진 느낌.

---

## 1. 포털에 붙이지 않는다

`jsini-portal` 은 로그인한 사람이 오래 머무는 업무 화면이다. 소개 사이트는 처음 온 사람이
20초 안에 판단하는 공개 화면이다. 요구되는 것(SEO · OG 태그 · 첫 화면 속도 · 익명 쓰기 방어)이
반대라서 한 앱에 넣으면 양쪽이 서로를 망친다.

또 `fronts/packages` 는 [17-vben-upstream-sync.md](17-vben-upstream-sync.md) 대로 상위와
계속 맞춰 나갈 텐데, 그때마다 회사 대표 사이트가 흔들려서는 안 된다.

```
fronts/apps/jsini-site/     신설. 같은 pnpm 워크스페이스, @vben/* 의존 0
microservices/SiteServer/   신설. :5480
docs/brand/                 생성 완료 — 로고 · 모티프 · 사용 규칙
```

## 2. 프론트 — `fronts/apps/jsini-site` — 세웠다

Vue 3 + Vite + `vite-ssg`(정적 프리렌더) + Tailwind 4. 산출물은 정적 파일이고 서버 런타임이 없다.
쓰는 법과 결정 근거는 [앱 README](../../fronts/apps/jsini-site/README.md) 에 있다.

워크스페이스 안에 두는 이유는 node · pnpm · turbo · eslint · tailwind 도구 체인을 그대로
쓰기 위한 것뿐이다. `@vben/*` 는 하나도 가져오지 않는다. 배포 산출물도 완전히 분리한다.

- 도메인: `www.jsini.co.kr`(정적) / `admin.jsini.co.kr`(포털). 오리진을 나눠야 토큰·쿠키 표면이 섞이지 않는다.
- 다국어: `/ko` · `/en` 둘 다 처음부터 (D-S5). 경로에 언어를 넣고 DB 에 언어 열을 둔다.
- 폰트: 준수사항 5 를 지킨다. Pretendard 로컬 서브셋만. Google Fonts CDN 금지.
- 준수사항 4(세로 스크롤 없이 한 화면)는 **업무 화면 규칙이다.** 소개 사이트는 스크롤이
  서술 수단이므로 예외다 — `docs/준수사항.md` 규칙 4 의 예외 절에 적었다.

### 만든 것

```
src/main.ts                  ViteSSG 진입 (createApp 아님)
src/router/routes.ts         /:locale(ko|en) 아래 화면 다섯 + prerenderRoutes
src/layouts/site-layout.vue  헤더 · 본문 · 푸터
src/components/
  brand-logo.vue             가로 조합. 좌표를 인라인 (2톤/녹아웃 전환 · 첫 화면 요청 절약)
  shard-motion.vue           상승 조각 코드 모션 (canvas)
  site-header.vue            언어 전환 포함
  site-footer.vue
src/views/                   home · about · news · news-detail · downloads · contact
src/api/site.ts              fetch 만. 실패하면 빈 값 (프리렌더가 API 상태에 묶이지 않게)
src/i18n/messages.ts         화면 고정 문구 표 (vue-i18n 없음)
src/styles/index.css         @font-face · 브랜드 토큰 · 각진 스타일
public/brand · public/fonts  docs/brand 와 포털 글꼴에서 복사
```

프리렌더 결과는 **HTML 10장**(화면 5 × 언어 2)이다. 뉴스 상세는 뺐다 —
넣으려면 빌드 때 API 로 slug 를 받아야 하고, 그러면 배포가 API 상태에 묶인다.

`<html lang>` · `<title>` · `description` · `og:title` 은 `vite.config.ts` 의 `onPageRendered`
가 언어별로 채운다. 자바스크립트가 돌기 전에 맞아 있어야 하는 값이라 화면 코드에서
`document.title` 을 바꾸는 것으로는 늦다 — **SNS 미리보기는 JS 를 아예 돌리지 않는다.**

### 히어로 모션 — 영상을 쓰지 않은 이유

3MB 비디오 대 6KB 스크립트이고, 해상도가 무한하고, 무엇보다 **흑백 그라디언트는 비디오
압축에서 밴딩이 가장 잘 생기는 소재다.** 우리 팔레트가 정확히 그것이라 영상으로 만들면
계단이 보인다. 그래서 상승 조각을 캔버스에 아주 느리게 흘린다.

`prefers-reduced-motion: reduce` 면 한 프레임만 그리고 멈춘다. 탭이 보이지 않으면 멈춘다.
문구는 모션 위가 아니라 왼쪽에 둔다 — 움직이는 배경 위의 글자는 읽기 어렵다.

### 확인한 것

```
pnpm build      HTML 10장 (ko.html · ko/about · ko/news · ko/downloads · ko/contact, en 5장)
pnpm typecheck  통과

브라우저(:5556)
  lang               ko / en 각각 맞음
  글꼴               S-CoreDream → Play → 시스템   (준수사항 5 순서)
  외부 요청          0 건                          ← 준수사항 5 (CDN 금지) 지켜짐
  글꼴 파일          /fonts/ 에서만 받음
  히어로 배경        rgb(10,10,10) = Ink, canvas 있음
  제목               font-weight 700 · letter-spacing -1.08px
  언어 전환          /en/downloads ↔ /ko/downloads (경로 유지)
  API                /api/site/downloads?locale=en · /api/site/visits 호출됨
  빈 상태            "Nothing here yet." 로 그려짐
  DB → 화면          site.sections 에 행을 넣으면 홈에 블록이 나온다 (넣어 보고 지웠다)
```

### 아직 열지 않은 것

- **문의 양식.** 접수 API 는 준비됐지만 화면은 안내 문구만 둔다. 개인정보 수집·이용
  동의 문구(D-S7)가 없으면 이름·연락처를 받는 것이 동의 없이 받는 것이 된다.
  `views/contact.vue` 주석에 문구가 나온 뒤 함께 넣을 것을 적어 두었다.
- **문구 전체.** `site.sections` 가 비어 있어 홈·회사소개 블록이 나오지 않는다.
  **회사 소개 문구를 내가 지어 넣지 않았다** — 대표 사이트에 실릴 글이라 회사가 정해야 한다 (D-S8 · D-S9).
- **본문 마크다운 렌더링.** 지금은 그대로 보여 준다. 렌더러를 붙일 때 정제(sanitize)를
  함께 넣어야 한다. 관리 화면에서 쓴 글이라도 HTML 로 넣는 순간 XSS 경로가 된다.

## 3. 백엔드 — `microservices/SiteServer` (:5480) — 세웠다

소유 범위: 소개 콘텐츠 · 공개 자료실 메타 · 문의 접수 · 조회 통계.

AuthServer 에 넣지 않는다. AuthServer 는 토큰 발급기이자 권한의 중심이다.
익명 쓰기 엔드포인트(문의 등록)를 그 프로세스 안에 두면 공개 입력과 신뢰의 중심이
한 주소 공간을 공유하게 된다.

DB 는 별도 인스턴스까지 가지 않는다. `funeralv2` 안에 `site` 스키마를 새로 판다.

```
site.sections     소개 문구 블록 (언어별)
site.posts        뉴스 · 보도자료 (언어별)
site.downloads    공개 자료 메타 — 파일 자체는 FileServer
site.inquiries    문의 접수
site.visits       조회 집계 — 날짜 · 경로 · 언어별 횟수만
```

스키마는 [docs/sql/site_schema.sql](../sql/site_schema.sql) 이 만든다. **돌렸다** — 표 5개와
인덱스 8개가 붙었고 반복 실행도 확인했다. `Database.Migrate()` 는 쓰지 않는다
(`.gitignore` 가 `Migrations/` 를 제외하므로 그 방식은 다른 장비로 가지 않는다).

언어는 컬럼이 아니라 **행**으로 나눈다. `title_ko` · `title_en` 처럼 컬럼을 늘리면 언어가
셋이 되는 순간 컬럼이 배로 늘어난다. 그래서 `(열쇠, locale)` 을 유일하게 두었다.

### 엔드포인트

| 경로 | 정책 | 하는 일 |
|---|---|---|
| `GET /api/site/sections` | 익명 | 문구 블록. `locale` · `keyPrefix` |
| `GET /api/site/posts` | 익명 | 뉴스 목록. 공개 시각이 지난 것만 |
| `GET /api/site/posts/{slug}` | 익명 | 뉴스 상세 |
| `GET /api/site/downloads` | 익명 | 자료실 목록 |
| `GET /api/site/downloads/{id}/file` | 익명 | 횟수를 세고 FileServer 로 302 |
| `POST /api/site/inquiries` | 익명 + 분당 3회 | 문의 접수 |
| `POST /api/site/visits` | 익명 | 조회 집계 |
| `GET /api/site/admin/inquiries` | 인증 | 접수 목록 (포털 화면용) |
| `PUT /api/site/admin/inquiries/{id}/status` | 인증 | 상태 변경 |

`downloads/{id}/file` 이 한 번 거치는 이유는 브라우저가 FileServer 를 직접 열면 내려받기 수를
셀 수 없기 때문이다. 공지 첨부와 같은 방식이다. **넘겨받는 파일은 `is_public` 을 켜 두어야 한다.**

### 확인한 것

```
GET  /api/site/sections?locale=ko      200
GET  /api/site/admin/inquiries          401   (인증 요구)
POST /api/site/inquiries  동의 없음     400   "개인정보 수집·이용에 동의해야 접수됩니다."
POST /api/site/inquiries  허니팟 채움   200   ← 성공처럼 보이지만 DB 에 저장되지 않았다
POST /api/site/inquiries  정상          200   저장됨 (동의시각 · 아이피 기록 확인)
POST /api/site/inquiries  4회째         429   분당 3회 제한
```

허니팟에 걸린 요청에 성공 응답을 주는 것은 일부러다. 400 을 주면 봇이 무엇에 걸렸는지 알게 된다.
넣어 본 시험 행은 지웠다(`site.inquiries` 0건).

## 4. 게이트웨이 라우트

[ApiGateway/appsettings.json](../../ApiGateway/appsettings.json) 에 셋을 넣는다.

| 경로 | 정책 | Order |
|---|---|---|
| `/api/site/admin/{**}` | 지정 안 함 (= `FallbackPolicy`, 인증 필요) | 0 |
| `POST /api/site/inquiries` | `Anonymous` + 신규 RateLimiter `public-write` | 1 |
| `GET /api/site/{**}` | `Anonymous` | 2 |

관리 화면은 새로 만들지 않고 기존 포털에 메뉴를 추가한다 (D-S2). `scom.system_menus` 주도
구조를 그대로 쓴다. 비개발자가 고칠 수 있어야 하고, 권한·이력이 이미 그쪽에 있다.
정적 빌드이므로 저장 후 재빌드 훅이 필요하다.

`public-write` 는 `auth-attempts` 옆에 IP 당 분당 3회로 만들었다. 로그인(분당 10회)보다 조인다 —
사람이 문의를 그보다 자주 보낼 일이 없고, 익명 쓰기는 한 번 열리면 곧 스팸의 통로가 된다.

라우트는 넷으로 나눴다. 조회는 **GET · HEAD 만** 익명이다 — 위에 없는 쓰기가 새로 생겨도
익명으로 새어 나가지 않게 메서드를 못박았다. 어디에도 걸리지 않은 `/api/site` 요청은
`site-route`(Order 3, 정책 미지정 = 인증 필요)가 받는다.

`dev.bat` · `backend_run_ubuntu.sh` · `backend_run_mac.sh` 에 `site` 로 등록했다
(`dev.bat site` 로 이것만 재기동된다). 스모크 테스트에도 넣었다.

## 5. 익명 다운로드 구멍 (D-S3) — 닫았다

게이트웨이에서 `/api/file/download/{**}` 가 이미 `Anonymous` 다. id 만 알면 누구나 내려받는다.
헬프데스크 첨부까지 포함된다. 지금은 "아무도 id 를 모른다" 에 의존하는 상태인데,
공개 자료실이 이 위에 올라가면 그 구멍이 정식 기능으로 굳는다.

**결정: FileServer 에 `is_public` 플래그를 두고, 익명 읽기 라우트는 그 플래그가 켜진 파일만
통과시킨다.** 기본값은 `false` 라 기존 파일 전부가 보호된다.

작업하면서 **더 큰 구멍을 먼저 찾았다.** 포괄 라우트 `/api/file/{**remainder}` 도 `Anonymous`
였다. 읽기 경로가 익명이어야 해서 같이 열어 둔 것인데, 그 바람에 쓰기까지 열려 있었다.

```
DELETE /api/file/{id}   토큰 없이 아무 파일이나 삭제        (FileServer 에 인증 검사가 없다 — 직접 호출 시 404 = 그냥 실행됨)
POST   /api/file/upload 토큰 없이 업로드
GET    /api/file/metadata/{id}  토큰 없이 원본 파일명·크기 열람
```

### 무엇을 했나

0. **SQL 을 실제로 돌렸다** (2026-08-26). `scom.filemetadatas.ispublic` 이 붙었고 살아 있는 174건이
   전부 `false` 다. 반복 실행도 확인했다.
1. `scom.filemetadatas.ispublic` 컬럼 추가 — [docs/sql/file_is_public.sql](../sql/file_is_public.sql).
   기본값 `false` 라 기존 파일 전부가 보호 대상으로 남는다.

   처음에는 EF 마이그레이션으로 만들었다가 걷어냈다. **`.gitignore` 8행이 `Migrations/` 를
   제외한다.** FileServer 는 유일하게 `Database.Migrate()` 를 부르는 서비스인데 마이그레이션이
   추적되지 않으니, 만들어도 다른 장비에는 가지 않는다. 그쪽에서는 컬럼이 없는 채로 뜨고
   `filemetadatas` 를 읽는 모든 쿼리가 깨진다. 그래서 다른 여섯 서비스와 같은 방식으로 돌렸다.
2. 게이트웨이 `file-route` 에서 `AuthorizationPolicy: Anonymous` 제거 → 쓰기 전부 401.
3. `PUT /api/file/public/{id}?value=` — 공개 여부를 바꾸는 엔드포인트 (인증 필요).
4. `PublicFileAccessFilter` — 읽기 엔드포인트 7개에 붙였다.
   `Files:RequirePublicFlagForAnonymous` 는 **켜 두었다**(다음 항목 참고).
   막을 때는 403 이 아니라 404 다 — 403 은 "그 아이디의 파일은 있다" 를 알려 준다.
5. 로그인이 `jsini_file_at` 쿠키를 심고, 게이트웨이가 파일 읽기 경로에서만 그것을 신원으로 받는다.
6. 스모크 테스트 5·6절 추가 — 쿠키가 읽기만 인증하는지, 쓰기 3개가 401 인지.

`dev.bat gateway auth file` 로 세 서비스를 다시 띄워 반영했다.

### 읽기 판정도 켰다 — 브라우저가 스스로 보내는 인증 수단을 붙였다

읽기 판정은 처음에 껐다. `is_public` 만으로는 구멍이 닫히지 않기 때문이었다(아래).
그 전제를 없애고 켰다.

로그인할 때 같은 토큰을 **`jsini_file_at` 쿠키로도 심는다.**

```
AuthServer/Endpoints/AuthEndpoints.cs   로그인이 심고, 로그아웃이 지운다
  HttpOnly · SameSite=Lax · Path=/api/file · Secure(개발 제외) · 만료는 토큰과 같음(7일)

ApiGateway/Program.cs  OnMessageReceived
  Authorization 헤더가 없을 때만, 그리고 **파일 읽기 경로일 때만** 쿠키를 신원으로 받는다
  /api/file/download · thumbnail · medium · large · resize
```

안전장치를 셋 걸었다.

| 장치 | 막는 것 |
|---|---|
| `Path=/api/file` | 쿠키가 다른 API 로 실려 나가지 않는다 |
| `SameSite=Lax` | 남의 사이트가 우리 주소로 `<img>` 를 걸어도 쿠키가 실리지 않는다 |
| 게이트웨이의 경로 제한 | 업로드·삭제·공개여부 변경에는 쿠키를 받지 않는다 (CSRF) |

세 번째가 중요하다. 쿠키를 전 구간 인증으로 받아 주면 남의 사이트가 우리 주소로 요청을 걸어
파일을 지울 수 있다. 그래서 쿠키는 **읽기 전용 신원**이다.

### 확인한 것

브라우저(포털 오리진, Vite 프록시 경유)에서 직접 확인했다.

```
로그아웃           HTTP 200 → 쿠키가 지워진다
익명   thumbnail   404      → 막힌다
로그인 후 thumbnail 302      → 통한다 (브라우저가 스스로 쿠키를 실었다)
document.cookie    ""       → httpOnly 라 스크립트는 못 읽는다
```

게이트웨이 쪽도 함께 확인했다.

```
쿠키로 DELETE   401   CSRF 차단
쿠키로 metadata 401   읽기 경로에서만 받는다
익명 + is_public=true  → 302 (허용)   ← PUT /api/file/public/{id}?value=true 로 켜고 되돌렸다
익명 + is_public=false → 404 (차단)
```

스모크 테스트 21개 통과(5절·6절 8개가 새 항목). 1절의 포트 확인 6개는 Windows 에 `ss` 가
없어서 실패로 나온다 — 3절이 서비스가 살아 있음을 증명한다.

### 알아 둘 것 — 이미 로그인해 있던 사람은 다시 로그인해야 한다

토큰은 localStorage 에 있고 쿠키는 새로 심는 것이라, 스위치를 켠 시점에 이미 로그인해 있던
브라우저는 **쿠키가 없어서 사진이 404 로 깨진다.** 실제로 그 상태를 확인했다(아바타 404).
다시 로그인하면 정상으로 돌아온다. 운영에 올릴 때는 배포와 함께 세션을 만료시키는 편이 낫다.

### 공개 공지의 첨부도 공개로 본다 (D-S10) — 결정하고 구현했다

`/api/auth/notices/popup/public` 은 익명으로 열려 있고 그 팝업이 로그인 화면에서도 뜬다.
팝업 안의 첨부 링크는 익명이라 404 가 됐다. **공지를 공개로 두면 첨부도 공개로 본다.**

```
AuthServer/Services/PublicFileSyncService.cs
  공지를 저장할 때마다(생성 · 수정 · 삭제) 첨부의 ispublic 을 다시 계산한다.
  기준: is_public AND status = 1 AND NOT is_deleted 인 공지에 붙어 있는가.
  한 파일이 여러 공지에 붙어 있으면 어느 하나라도 공개면 공개다(bool_or).

docs/sql/notice_public_files.sql
  이미 있던 공지들을 소급 반영. 돌렸다 — 공개 공지 1건의 첨부 2개가 켜졌다.
```

**게시 기간은 보지 않는다.** 팝업 조회는 `start_at`·`end_at` 까지 보지만, 기간은 아무도
저장을 누르지 않아도 지나간다. 반영하려면 주기 작업이 하나 더 필요한데, 얻는 것은
'기간이 끝난 공지의 첨부를 주소를 아는 사람이 더 받을 수 있다' 를 막는 것뿐이다.

**남이 켜 둔 것은 끄지 않는다.** 끄는 것은 `updatedby = 'NoticeSync'` 인 행뿐이다.
소개 사이트 자료실처럼 다른 이유로 공개된 파일을, 공지에서 뗐다는 이유로 닫아 버리면 안 된다.

**왜 AuthServer 가 FileServer 의 표를 건드리나.** 서비스 간 인증이 없다.
`PUT /api/file/public/{id}` 는 게이트웨이가 인증을 요구하고, 서비스가 직접 부르면
`X-User-Id` 가 없어 401 이다. 그 통로를 새로 여는 것은 인증 설계를 건드리는 일이라,
**같은 DB · 같은 `scom` 스키마** 라는 사실을 쓰는 한 문장짜리 UPDATE 로 두었다.
서비스를 다른 DB 로 갈라야 할 때가 오면 이 클래스 하나만 HTTP 호출로 바꾸면 된다.

확인 — 공지의 공개 여부를 API 로 토글하고 익명 다운로드를 같이 봤다.

```
공지 → 비공개   ispublic=false (updatedby=NoticeSync)   익명 download 404
공지 → 공개     ispublic=true                          익명 download 302
```

### 왜 `is_public` 만으로는 안 됐는가 — 배경

`is_public` 만으로는 구멍이 닫히지 않았다. **포털 안에서 사진을 보는 요청도 FileServer 쪽에서는
익명과 구별되지 않았기 때문이다.** 포털은 토큰을 `Authorization` 헤더로 붙이는데
(`fronts/apps/jsini-portal/src/api/request.ts`), 브라우저는 `<img src>` · `<a href>` 에
그 헤더를 붙여 주지 않는다. 실제로 이렇게 쓰고 있다.

```
components/notice/notice-popup.vue          공지 첨부 다운로드 링크
components/rich-editor/rich-editor.vue      본문 이미지
layouts/basic.vue                           사용자 아바타
views/funeral/building/status/room-card.vue 빈소 현황의 고인 사진
```

그래서 **브라우저가 자동으로 보내는 인증 수단**이 먼저 필요했다. 선택지는 셋이었다.

| 안 | 방법 | 프론트 수정 | 위험 |
|---|---|---|---|
| **A** | 로그인 때 `/api/file` 범위 httpOnly 쿠키를 함께 심고, 게이트웨이가 그것도 `X-User-Id` 의 근거로 받는다 | 없음 | 로그인 경로를 건드린다 |
| B | 파일 URL 을 쓸 때마다 단기 서명 URL 을 받아 온다 | 위 4곳 + 앞으로 생기는 곳 전부 | 낮음. 대신 계속 번진다 |
| C | 이미지를 JS 로 받아 blob URL 로 바꾼다 | 위 4곳 전부 | 캐시·메모리 관리가 새로 생긴다 |

**A 로 했다.** 한 곳만 고치면 끝나고 프론트를 건드리지 않는다.
B 는 파일 URL 을 만드는 자리가 앞으로 계속 늘어날 텐데 그때마다 같은 일을 해야 한다.

## 6. 문의 접수 (D-S4) — 열었다

방어는 셋이다. 외부 스크립트를 부르지 않아 준수사항 5 의 취지와 부딪히지 않는다.

| 겹 | 무엇 |
|---|---|
| 게이트웨이 | IP 당 분당 3회 (`public-write`) |
| 화면 + 서버 | 허니팟 칸 `website` — 채워져 있으면 조용히 버리고 **성공 응답을 준다** |
| 서버 | 동의 없이는 접수하지 않는다. `consentedat` 이 빈 행은 만들어지지 않는다 |

허니팟에 성공 응답을 주는 것은 일부러다. 400 을 주면 봇이 무엇에 걸렸는지 알게 된다.

`display:none` 을 쓰지 않고 화면 밖(`-left-[9999px]`)으로 밀고 `aria-hidden`·`tabindex="-1"`
을 건다. 요즘 봇은 `display:none` 을 보고 건너뛴다.

부족해지면 Turnstile 을 올린다. 그때가 오기 전에는 외부 의존을 두지 않는다.

### 개인정보 동의 문구 (D-S7) — 임시로 채웠다

⚠ **법률 검토를 받지 않은 문구다.** 사이트를 실제로 공개하기 전에 반드시 확정해야 한다.
보관 기간을 3년으로 적었는데 그것도 정해진 값이 아니다.

문구는 코드가 아니라 DB 에 있다 — `site.sections` 의 `contact.consent` (ko · en).
법률 문구는 코드 배포 없이 고칠 수 있어야 하기 때문이다. 블록이 없으면 화면에 박아 둔
같은 뜻의 문구가 쓰인다(동의 문구가 아예 안 보이는 것이 더 나쁘다).

찾아 바꿀 때: `SELECT * FROM site.sections WHERE createdby = 'PlaceholderSeed'`

2단계로 접수 건을 헬프데스크 티켓으로 자동 생성하는 연결을 검토할 수 있다. 지금은 하지 않는다.

### 확인한 것

브라우저에서 폼을 채워 보냈다.

```
접수         200 → site.inquiries 에 행 1건 (동의시각 · 아이피 · locale 기록)
완료 화면    폼이 사라지고 안내만 남는다 (두 번 보내는 것을 막는다)
동의 없음    400 "개인정보 수집·이용에 동의해야 접수됩니다."
허니팟 채움  200 이지만 DB 에 저장되지 않는다
4회째        429
```

동의 문구의 `**강조**` 가 그대로 보이던 것을 고쳤다 — `components/rich-text.vue` 가
문단과 굵은 글씨만 살려 그린다. **`v-html` 을 쓰지 않는다.** 문자열을 조각으로 잘라
템플릿으로 그리므로 어떤 값이 들어와도 태그로 해석되지 않는다.

넣어 본 시험 행은 지웠다.

## 7. 브랜드 — 무엇을 만들었나

[docs/brand/](../brand/) 에 생성했다. 사용 규칙은 [docs/brand/README.md](../brand/README.md).

- 심볼: **js 인터록** — 각진 대문자 J 와 S 가 4 유닛 겹친다. J 가 Ink, S 가 Steel.
- 워드마크: **JSINI 대문자**, 트래킹 10 유닛(캡 높이의 33%).
- 조합 기본형: **가로** (심볼 왼쪽, 글자 오른쪽). 세로는 보조.
- 보조 모티프: **상승 조각** — 사이트 전반의 리듬. 심볼 자리에는 쓰지 않는다.
- 색: Ink `#0A0A0A` · Graphite `#1C1C1E` · Steel `#6E6E73` · Mist `#D2D2D7` · Paper `#FFFFFF`.
  액센트 색은 두지 않는다.

폰트를 쓰지 않고 레터폼을 좌표로 직접 그렸다. [generate.py](../brand/generate.py) 를 돌리면
전부 다시 만들어진다. SVG 를 손으로 고치지 않는다.

### 알아 둬야 하는 제약 하나

심볼은 **2톤이 기본이다.** J 의 스템과 S 의 좌측 스템이 같은 자리를 지나므로,
한 색으로 눌러 쓰면 두 글자가 한 덩어리로 붙는다. 1색이 필요한 자리에는
`symbol-mono.svg`(J 실루엣에 흰 키라인을 두른 것)를 쓰는데, 이것은 **흰 배경 전용**이고
최소 폭이 64px 이다. 더 작거나 배경이 흰색이 아니면 `favicon.svg`(블레이드 J 축약)로 간다.

## 8. 모션 배경

비디오보다 코드 모션이 낫다. 3MB 비디오 대 6KB 스크립트이고, 해상도가 무한이고,
무엇보다 흑백 그라디언트는 비디오 압축에서 밴딩이 가장 잘 생기는 소재다.

상승 조각의 기울기를 그대로 써서 캔버스에 사선 격자가 아주 느리게 흐르게 하고,
스크롤에 따라 각도만 바꾼다. `prefers-reduced-motion: reduce` 를 존중하고,
히어로 문구는 모션 위가 아니라 옆이나 아래에 둔다. 첫 프레임은 정적 poster 로 깐다.

굳이 영상을 쓴다면 8~12초 심리스 루프 · 1920×1080 · H.264 와 VP9 둘 다 · 3MB 이하 ·
`muted autoplay playsinline` · 영상 안에 글자를 넣지 않는다.

## 9. 결정된 것

| 번호 | 결정 | 날짜 |
|---|---|---|
| D-S1 | 프레임워크는 `vite-ssg`. 정적 산출물, 서버 런타임 없음 | 2026-08-26 |
| D-S2 | 콘텐츠는 `site` 스키마에 두고 포털 화면에서 편집. 저장 시 재빌드 훅 | 2026-08-26 |
| D-S3 | FileServer 에 `is_public` 플래그. 익명 라우트는 그것만 통과 | 2026-08-26 |
| D-S4 | 허니팟 + IP 레이트리밋으로 시작. Turnstile 은 필요해지면 | 2026-08-26 |
| D-S5 | `ko` · `en` 둘 다 처음부터 | 2026-08-26 |
| D-S6 | 심볼은 js 인터록 · 워드마크는 JSINI 대문자 · 가로 조합 기본 · 상승 조각 보조 | 2026-08-26 |
| D-S7 | 개인정보 동의 문구 — **임시로 채웠다.** 공개 전 법률 검토 필요 (6절) | 2026-08-26 |
| D-S8 | 정보구조와 소개 문구 — **임시로 채웠다** (`docs/sql/site_seed_placeholder.sql`) | 2026-08-26 |
| D-S9 | 영문 문구 — 임시 문구를 `ko`·`en` 두 벌로 넣었다 | 2026-08-26 |
| D-S10 | 공지를 공개로 두면 첨부도 공개로 본다 (5절) | 2026-08-26 |

## 10. 남은 것

### 회사가 확정할 것

| 항목 | 내용 |
|---|---|
| **D-S7 문구** | ⚠ 개인정보 동의 문구를 **법률 검토 없이** 채웠다. 보관 기간 3년도 임의 값이다. **공개 전에 반드시 확정한다.** |
| **D-S8 문구** | 소개 문구가 임시다. 설립연도 · 임직원 수 · 고객사 · 매출 같은 **확인할 수 없는 사실은 일부러 넣지 않았다** — 임시 문구가 사실처럼 굳는 것이 더 나쁘다. 그래서 '연혁' 블록도 두지 않고 '일하는 방식' 으로 대신했다 |
| **D-S9 문구** | 영문도 같은 상태다 |

임시 문구는 모두 `createdby = 'PlaceholderSeed'` 로 표시해 두었다.

```sql
SELECT * FROM site.sections WHERE createdby = 'PlaceholderSeed';
SELECT * FROM site.posts    WHERE createdby = 'PlaceholderSeed';
```

### 코드 쪽 (결정 필요 없음)

| 항목 | 내용 |
|---|---|
| R-S3 | 파일 공개 여부를 켜고 끄는 포털 화면. 지금은 API(`PUT /api/file/public/{id}`) 만 있다 |
| R-S4 | 운영 배포 때 기존 세션 만료시키기. 안 하면 로그인해 있던 사람의 사진이 깨진다 (5절) |
| R-S5 | 소개 콘텐츠 관리 화면 (포털에 메뉴 추가 — D-S2 결정). 관리 API 는 문의만 있다 |
| R-S6 | 저장 후 정적 사이트 재빌드 훅 (D-S2 의 대가) |
| R-S7 | 배포 — `www.jsini.co.kr` 정적 호스팅, `admin.jsini.co.kr` 로 포털 오리진 분리 |
| R-S8 | 자료실(`site.downloads`)에 실제 자료 올리기. 파일을 FileServer 에 올리고 `is_public` 을 켠 뒤 행을 만든다 — 임의 파일을 지어 넣지 않아 지금은 비어 있다 |
| ✔ R-S1 | 준수사항 4 예외 — 넣었다 |
| ✔ R-S2 | 읽기 판정 켜기 — 켰다 (5절) |

### 이 문서 밖의 것 하나

`.gitignore` 8행이 `Migrations/` 를 제외하는데 FileServer 만 `Database.Migrate()` 를 쓴다.
**새 장비에서는 FileServer 테이블이 아예 생기지 않는다.** `ispublic` 을 넣을 때 발견해
`docs/sql` 방식으로 우회했지만(5절), 기존 테이블 4개는 그대로 문제다.
별도 작업으로 다루기로 했으나 그 세션이 지워져 **아직 손대지 않았다.**

## 11. 개발 서버 띄우기

`dev.bat`(윈도우) · `backend_run_ubuntu.sh` · `backend_run_mac.sh` 가 같은 표를 읽는다.
서비스가 열이다.

```
gateway auth funeral ai file helpdesk projmng site portal web
                                      ^^^^      ^^^^^^ ^^^
                                      신설      이름바뀜 신설
```

- `site` — SiteServer (:5480)
- `portal` — 업무 포털 프론트 (:5555). **예전 이름 `front` 다.** `front` 로 쳐도 받아 준다
- `web` — 회사 소개 사이트 프론트 (:5556)

`dev.bat site web` 으로 소개 사이트만 재기동한다.

### 표에 '기동 명령' 칸이 생겼다

프론트가 둘이 되면서 "front 면 pnpm, 아니면 dotnet" 이라는 분기로는 표현이 안 됐다.
형식이 `표시이름|상대경로|포트|SERVER_NAME|기동명령` 으로 늘었고, `SERVER_NAME` 이 `-` 면
프론트다(빌드 대신 `pnpm install`, 여러 개여도 한 번만). 세 번째 프론트가 붙어도 한 줄만 더한다.

### 겪은 것 — 프론트 하나를 내리면 다른 하나도 죽었다

`dev-stop.ps1` 이 **작업 디렉터리로도** 프로세스를 골랐다. 프론트 둘이 `fronts` 를 공유하고
node 실행 파일도 `fronts
ode_modules` 하나를 쓰기 때문에, `dev.bat stop web` 이
업무 포털까지 내렸다.

`-CmdMatch` 를 추가해 프론트는 **명령줄로** 고르게 했다(`pnpm --filter @jsini/site dev`).
양방향으로 확인했다 — 한쪽을 내려도 다른 쪽은 살아 있다.

## 12. 지나가며 고친 것 — 게이트웨이가 로컬 설정을 안 읽고 있었다

작업 중 **모든 인증 호출이 401** 이 되는 상태를 만났다. 원인은 내 변경이 아니었다.

다른 세션이 JWT 서명 키를 `appsettings.Local.json`(git 제외)으로만 두는 작업(D1-B)을 하면서
`ApiGateway/Program.cs` 에 `JwtKeyGuard.Require(config, "Jwt:Key", ...)` 를 넣었는데,
**게이트웨이는 `appsettings.Local.json` 을 읽는 줄이 없었다.** AuthServer · FileServer ·
SiteServer 는 모두 그 줄이 있다.

그래서 AuthServer 는 Local 의 키로 서명하고 게이트웨이는 저장소에 남아 있는 예전 값으로
검증했다 — 토큰이 전부 401. 게다가 그 작업의 목적("잘 알려진 키를 못 쓰게 한다")도
조용히 깨져 있었다. 게이트웨이는 바로 그 예전 키로 돌고 있었기 때문이다.

한 줄을 넣어 다른 서비스와 같은 방식으로 맞췄다.

```csharp
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
```

**남은 것은 그 세션의 판단이다** — `ApiGateway/appsettings.json` 에 예전 키가 아직 남아 있다.
D1-B 대로라면 그것을 지워야 검사가 실제로 동작한다. 지우면 Local 설정이 없는 환경은
기동에 실패하므로, 순서를 정하는 것은 그쪽 일이다. 나는 건드리지 않았다.
