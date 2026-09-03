# 도움말 — F.A.Q · Q&A

작성: 2026-08-25
대상 화면: `/help/faq` · `/help/qna`

> 지시
> - `/help/faq` 는 FAQ 화면이다. 관리자는 FAQ 를 작성·수정할 수 있으며,
>   다른 사용자들은 FAQ 를 읽을 수 있는 화면으로 진행하라. 필요한 백엔드와 화면을 구현하라.
> - `/help/qna` 는 Q&A 화면이다. 사용자는 질문을 하고 관리자는 답변을 할 수 있도록 구성하라.
>   답글에 추가 질문을 할 수 있도록 하고, 답글에 답글에 답글을 달 수 있도록 구성하라.
>   Q&A 에 올라온 글은 관리자는 모두 볼 수 있고 답을 달 수 있다.
>   다른 사용자는 관리자가 공개한 Q&A 만 볼 수 있도록 한다.
>   답글도 관리자가 공개한 답글만 일반 사용자가 볼 수 있도록 관리된다.

두 화면 모두 **임시 화면(자리만 있던 것)** 이었다. 백엔드가 아예 없었고
프론트에는 존재하지 않는 경로(`/help/faq/list` 등)를 가리키는 껍데기 API 만 있었다.

---

## 1. 어디에 붙였나 — AuthServer

공지(`scom.notices`)와 같은 방침을 따랐다.

> 도움말은 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
> 각 MSA 가 자기 FAQ·Q&A 를 따로 두지 않는다.

권한이 이미 AuthServer 한 곳(`scom.role_menus`)에 있어서, 여기에 두면
'관리자냐 아니냐' 를 서비스 사이로 물어보지 않고 바로 판정할 수 있다.
장례식장 API 나 헬프데스크에 두면 같은 판정을 다시 만들어야 한다.

```
scom.faqs        F.A.Q
scom.qna_posts   Q&A — 질문과 답글을 한 테이블에
```

## 2. '관리자' 를 무엇으로 판정했나

새 플래그를 만들지 않고 **메뉴 권한을 그대로 썼다.** 권한 관리 화면에서
이미 다루는 값이라, 나중에 역할을 조정할 때 다른 데를 찾아갈 일이 없다.

| 화면 | 읽기 | 쓰기(= 관리자) |
|---|---|---|
| `/help/faq` | `can_view` · `can_search` | `can_create` · `can_update` · `can_delete` |
| `/help/qna` | `can_view` · `can_search` | `can_cust1` — 이름 `답변·공개 관리` |

실제로 배정한 결과(실행 후 확인한 값):

| 역할 | 계정 | `/help/faq` 쓰기 | `/help/qna` 관리 |
|---|---|---|---|
| `ADMINISTRATOR` (관리자) | 1 | ✅ | ✅ |
| `SYSTEM_ADMINISTRATOR` (시스템관리자) | 1 | ✅ | ✅ |
| `PARTNER_ADMINISTRATOR` (파트너 관리자) | 0 | ✗ | ✗ |
| `PARTNER` (파트너) | 39 | ✗ | ✗ |

읽기(`can_view`·`can_search`)와 Q&A 질문 등록(`can_create`·`can_update`·`can_delete`)은 네 역할 모두 갖는다.

Q&A 만 다르게 잡은 이유가 있다. **Q&A 는 일반 사용자도 글을 쓴다.**
그래서 `can_create` 를 관리자 표시로 쓸 수 없다. 사용자 정의 권한 1번에
이름을 붙여(`cust1_name`) 관리자 표시로 썼다 — 메뉴 관리 화면에도 그 이름이 그대로 나온다.

Q&A 의 `can_update` · `can_delete` 는 **자기 글**에만 쓴다. 남의 글을 고치는 것은
`can_cust1` 이 있는 사람만이다.

### 판정은 서버가 한다

화면의 `v-perm` 은 버튼을 숨기는 장치일 뿐이다. 같은 API 에 직접 요청을 보내면 통과한다.
그래서 쓰기 권한은 `FaqService` · `QnaService` 가 다시 확인한다.

그러면 화면과 서버의 판단이 어긋날 수 있다 — 권한 정보가 늦게 도착하면
화면은 버튼을 보여 주는데 저장이 막힌다. 그래서 **화면이 권한 스토어를 보지 않게** 했다.
목록 응답에 `canManage` · `canWrite` 를 함께 담아 내려주고, 화면은 그것만 본다.
글마다 `canEdit` · `isMine` 도 서버가 계산해 준다.

새로 만든 공용 도구:

```
IMenuService.GetEffectivePermissionAsync(userId, path)
```

경로 하나의 실효 권한을 돌려준다. 기존 `GetMenuPermissionsAsync` 를 그대로 쓰므로
"여러 역할을 OR 로 합치고, 메뉴가 안 쓴다고 한 항목은 끈다" 는 규칙이 자동으로 따라온다.

## 3. Q&A — 무엇이 누구에게 보이나

```
관리자   전부
그 외    공개된 글(is_public) + 자기가 쓴 글(author_id)
```

답글도 같은 규칙이다. 여기에 규칙 하나를 더 뒀다.

> **부모가 안 보이면 그 아래 답글도 함께 감춘다.**

무엇에 대한 답인지 알 수 없는 답글만 떠 있으면 오히려 혼란스럽기 때문이다.
비공개 질문에 공개 답변이 달려 있어도, 질문을 못 보는 사람에게는 답변도 안 보인다.

### 공개 기본값

| 누가 쓰나 | `is_public` |
|---|---|
| 일반 사용자 | **꺼짐** — 관리자가 공개해야 남에게 보인다 |
| 관리자 | **켜짐** (창에서 끌 수 있다) |

관리자 답변을 기본 공개로 둔 이유: 답변을 공개하지 않으면 질문한 사람조차 볼 수 없다.
관리자가 매번 두 번 눌러야 하는 흐름이 되어 실수하기 쉽다.
비공개로 남겨 둘 필요가 있으면 등록 창에서 끄면 된다.

`author_id` 가 있어서 **작성자 본인은 자기 글이 비공개여도 본다.**
질문을 올리고 나서 "내 글이 사라졌다" 로 보이지 않는다.

### 답글의 답글의 답글

한 테이블에 `parent_id` 로 자기 자신을 가리키게 두었다. 깊이 제한이 없다.

```
parent_id IS NULL   질문(스레드 뿌리)
root_id             스레드 뿌리 아이디 (뿌리 글은 자기 자신)
depth               뿌리 = 0, 답글 = 부모 + 1
```

`root_id` 를 따로 둔 것은 조회 때문이다. 스레드 하나를 통째로 가져올 때
부모를 재귀로 따라 올라가지 않고 한 번의 조회로 끝난다.

화면에서는 `views/funeral/help/qna/modules/qna-post.vue` 가 **자기 자신을 다시 부른다.**
답글이든 답글의 답글이든 같은 부품 하나로 그려진다.
들여쓰기는 6단까지만 준다 — 그보다 깊어지면 본문이 좁아져 읽기가 더 어려워진다.

글을 지우면 그 아래 답글까지 함께 지운다(soft delete). 남겨 두면
무엇에 대한 답인지 알 수 없는 글이 된다.

## 4. 본문 HTML 을 세탁한다 — `RichTextSanitizer`

여기가 이번 작업에서 **새로 생긴 위험**이었다.

공지·F.A.Q 는 관리자만 쓴다. 그런데 **Q&A 는 일반 사용자가 본문을 쓴다.**
그 본문은 조회 화면에서 `v-html` 로 그려진다(공지·헬프데스크 댓글과 같은 방식).

화면 편집기(tiptap)는 자기 스키마에 없는 태그를 알아서 버리지만, 그것은 화면 쪽 정리다.
같은 API 에 직접 요청을 보내면 무엇이든 들어오고, 그 글은 남의 화면에서 그려진다.

그래서 저장 전에 허용 목록으로 걸러 낸다(`Services/RichTextSanitizer.cs`).
금지 목록이 아니라 허용 목록이다 — 금지 목록은 새 태그·속성이 생길 때마다 구멍이 난다.

- 남기는 태그: 문단·서식·목록·표·`a`·`img`
- 통째로 버리는 태그: `script` · `style` · `iframe` · `object` · `svg` · `form` …
- `on*` 속성은 전부 제거
- `href` · `src` 는 `http(s)` · `mailto:` · `tel:` · 상대경로만 (`javascript:` 차단)
- 새 창 링크에는 `rel="noopener noreferrer"` 를 강제

F.A.Q 답변도 같이 지나가게 했다. 관리자만 쓰지만 걸러서 손해 볼 것이 없다.

HTML 파싱은 `HtmlAgilityPack` 을 썼다. **HelpDeskServer 가 이미 쓰는 것과 같은 버전**이라
새 의존성을 들이지 않았다.

> 공지(`scom.notices`)는 이 세탁을 지나지 않는다. 관리자만 쓰는 글이라 이번 범위에서 뺐다.
> 함께 걸고 싶으면 `NoticeService` 의 `Content` 대입 두 곳에 같은 한 줄을 넣으면 된다.

## 4-1. 영상 넣기 (YouTube 등)

> 지시: "FAQ 등록시 답변입력시 html 코드를 입력할 수는 없는가? youtube 영상같은 내용을
> 삽입하려고 한다." (삽입 코드 샘플 함께 제시)

막힌 곳이 **두 군데**였다. 서버 세탁기가 `<iframe>` 을 버리고, 화면 편집기(tiptap)도
자기 스키마에 없는 태그라 지웠다. 둘 다 열어야 한다.

### 서버 — 주소를 아는 영상만 허용한다

`RichTextSanitizer.Sanitize(html, allowEmbeds)` 로 바꿨다. 켜면 `<iframe>` 을 남기되
**호스트 허용 목록**(`EmbedHosts`)을 지나야 한다 — YouTube · YouTube(nocookie) · Vimeo ·
네이버TV · 카카오TV. 임의 주소를 허용하면 관리 화면 안에 남의 페이지를 띄워
버튼을 가리는 길(클릭재킹)이 열린다.

켠 자리는 둘뿐이다.

| 자리 | `allowEmbeds` |
|---|---|
| F.A.Q 답변 | 항상 (관리자만 쓴다 — 바로 앞에서 확인한다) |
| Q&A 본문 | `canManage` — **관리자가 쓴 글만** |

일반 사용자가 Q&A 에 iframe 을 넣으면 그대로 사라진다. 그래서 화면도 같은 조건으로
버튼을 감춘다 — 넣었는데 저장하면 없어지는 화면이 되지 않게.

같이 좁혀 둔 것들:

- `srcdoc` · `name` · `sandbox` 는 속성 허용 목록에 넣지 않았다.
  `srcdoc` 은 **주소 검사를 건너뛰고** 임의 HTML 을 실행하는 길이라 특히 위험하다.
- `style` 은 크기·여백 속성만 남긴다(`StyleProperties`). 그대로 두면
  `position:fixed` 로 화면 전체를 덮을 수 있다. 샘플의 `height:480px !important` 는 통과한다.
- `style` 안의 `url(...)` 은 버린다 — 크기 지정에 쓸 일이 없고 바깥으로 요청이 나가는 길이다.
- iframe **안쪽 내용은 비운다.** 대체 문구 자리인데 코드가 숨을 수 있다.
- `referrerpolicy` 는 `strict-origin-when-cross-origin` 으로 덮어쓴다.
  영상 서비스에 우리 주소 전체를 실어 보내지 않는다.
- `http` 로 들어온 주소는 `https` 로 바꿔 둔다. 그대로 두면 브라우저가
  혼합 콘텐츠로 막아 아무것도 보이지 않는다.

### 화면 — 편집기가 iframe 을 알아본다

`components/rich-editor/video-embed.ts` 에 tiptap 노드를 하나 더했다.
`atom` 이라 통째로 선택되고 안쪽에 커서가 들어가지 않는다 — 영상 위에 글을 쓰다
태그가 깨지는 일이 없다.

**이 노드는 버튼을 감추더라도 늘 등록한다.** 그러지 않으면 이미 저장된 영상이
수정 창을 한 번 여는 것만으로 사라진다(tiptap 이 스키마에 없는 태그를 지운다).

편집기에 붙은 것 둘:

- **영상 버튼**(`allowVideo`) — 영상 넣기 창을 연다(아래 4-2 절).
  영상 뒤에 빈 문단을 함께 넣는다(연달아 넣을 때 앞 영상을 덮어쓰지 않게).
- **HTML 버튼**(`htmlSource`) — 서식 편집기와 HTML 직접 입력을 오간다.
  지시가 요청한 "html 코드를 직접 입력" 이다. 편집기로 돌아갈 때 tiptap 이 한 번 정리하므로
  스키마에 없는 태그는 그 자리에서 사라진다(영상은 위 노드가 지켜 준다).

켠 자리: F.A.Q 답변(둘 다) · Q&A 등록 창(관리자만) · Q&A 답글 편집기(영상만, 관리자만).

조회 화면(`v-html`)에서는 `iframe { display:block; max-width:100%; border:0 }` 만 준다.
삽입 코드가 보통 `width="816"` 으로 오는데 목록이 그보다 좁으면 가로 스크롤이 생겨
준수사항 4 를 어긴다. 넣은 사람이 적은 크기는 그대로 두고 폭만 눌러 둔다 —
좁아지면 영상 서비스가 자기 안에서 맞춘다.

## 4-2. 영상 넣기 창

> 지시: "영상아이콘 클릭시에 브라우저가 지원하는 입력창이 나타나는데 매우 불만이다.
> 깔끔한 입력UI 형태로 바꿔라. 최근 웹시스템들이 유투브 삽입을 하는 UI 가 어떻게 생겼는지
> 조사하고 참고를 하길 바란다."

처음에는 `window.prompt` 를 썼다. 링크 넣기가 이미 그렇게 되어 있어 맞춘 것인데,
브라우저 기본 창은 붙여 넣은 것이 맞는지 확인할 방법이 없다. 창을 만들었다.

### 조사한 것

| 참고한 곳 | 가져온 것 |
|---|---|
| Notion · CKEditor 5 | **붙여넣기 우선.** 입력 한 칸이고 버튼을 누르지 않아도 알아본다. 편집기에 주소를 붙여 넣으면 바로 영상이 된다 |
| WordPress Gutenberg | 알아보지 못하면 **그 자리에 오류를 적고** 실행 버튼을 잠근다 |
| TinyMCE | **크기 칸 + 비율 유지 자물쇠**(constrain proportions), 붙여넣는 즉시 **미리보기** |
| YouTube 퍼가기 패널 | 미리보기 옆에 **재생 옵션 체크** (컨트롤 표시 · 시작 시간 …) |

### 만든 것 — `components/rich-editor/video-embed-modal.vue`

1. **입력 한 칸.** 주소든 삽입 코드든 받는다(삽입 코드는 한 줄에 안 담기므로 여러 줄 칸이다).
   **버튼을 누르지 않아도** 알아본다 — 붙여 넣는 것이 곧 실행이다.
2. **알아본 즉시 실제 재생기로 미리보기.** 저장하고 나서 확인할 일이 없다.
   서비스 이름과 영상 아이디도 함께 보여 준다.
3. **알아보지 못하면 그 자리에 이유를 적고** [넣기] 를 잠근다.
4. **크기** — `화면 폭에 맞추기`(기본) / `직접 지정`. 직접 지정에는 가로·세로 칸과
   16:9 비율 유지 자물쇠를 둔다.
5. **재생 옵션**(YouTube 만) — 자동 재생 · 소리 끔 · 반복 재생 · 컨트롤 숨기기 · 시작 시간.
   기본은 접어 두고, 이미 옵션이 붙은 주소를 넣으면 펼쳐서 무엇이 켜져 있는지 보여 준다.

손이 덜 가게 맞춰 둔 것들:

- **삽입 코드에 적힌 뜻을 이어받는다.** `width="816" height="480"` 이 있으면
  `직접 지정` 으로 바뀌어 그 값이 채워지고, 주소에 붙은 `autoplay=1&loop=1` 등도
  체크 상태로 읽힌다.
- **자동 재생을 켜면 '소리 끔' 이 자동으로 켜지고 잠긴다.** 브라우저가 소리 있는
  자동 재생을 막기 때문이다. 왜 잠겼는지 마우스를 올리면 알려 준다.
- **반복 재생을 켜면 `playlist` 에 같은 영상 아이디를 넣는다.** YouTube 는 한 편만
  반복할 때 이것이 없으면 동작하지 않는다.
- **미리보기에서는 자동 재생·반복을 뺀다.** 옵션을 만지는 동안 영상이 계속 다시
  시작하면 무엇을 보고 있는지 알 수 없다. 크기와 컨트롤은 그대로 반영한다.
- **편집기에 주소를 바로 붙여 넣어도 영상이 된다**(Notion·CKEditor 방식).
  붙여 넣은 것이 **주소 하나뿐일 때만** 그렇게 한다 — 글 속에 섞여 있으면 건드리지 않는다.
  링크로 쓰려고 붙여 넣은 것을 영상으로 바꿔 버리면 되돌리기가 번거롭다.

주소 해석은 `video-embed.ts` 로 모았다(`parseVideoInput` · `buildYoutubeSrc`).
`watch?v=ID` · `youtu.be/ID` · `/shorts/ID` · `/live/ID` · `/embed/ID` 를 모두 받는다.

## 4-3. Q&A 작성자 아바타

> 지시: "url /help/qna 에 질문글,답글 에 사용자 아바타가 보이지도록 개선 해줘."

글마다 왼쪽에 아바타를 둔다. 답글이 깊어질수록 이름만으로는 누가 썼는지 눈에 잘
들어오지 않기 때문이다. 목록의 질문 줄에도 질문한 사람의 아바타를 둔다.

- 사진은 `account_profile_details` 의 `Avatar` 항목에서 읽는다(계정 하나에 여러 항목이 있다).
- **사진은 글에 새겨 두지 않는다.** 조회할 때마다 계정에서 읽는다 —
  사진은 바뀌는 것이 정상이라, 글마다 그때의 사진을 붙여 두면 지난 글에 옛 사진이 남는다.
  이름(`author_name`)은 표시용 기록이라 새겨 두는 편이 맞다(작성 당시의 이름).
- **한 번에 읽는다**(`LoadAvatarsAsync`). 글마다 계정을 조회하면 답글 20개짜리 스레드에
  20번을 묻게 된다.
- 사진이 없으면 **이름 첫 글자**를 색 있는 동그라미에 그린다. 색은 이름에서 정하므로
  같은 사람은 늘 같은 색이다 — 이름을 읽지 않고도 구분된다.
- 원본이 아니라 **썸네일 경로**로 바꿔 쓴다(`/api/file/download/` → `/api/file/thumbnail/`).
  실제로 재 보니 원본 255KB(JPEG) → 썸네일 2.4KB(WebP) 로 100배 차이다.
  레이아웃 헤더(`layouts/basic.vue`)가 쓰는 것과 같은 규칙이다.

부품은 `views/funeral/help/qna/modules/author-avatar.vue` 하나로 두고
목록과 글에서 함께 쓴다.

## 5. 만든 것

### 백엔드 (AuthServer)

```
Entities/Faq.cs                  scom.faqs
Entities/QnaPost.cs              scom.qna_posts (자기 참조)
DTOs/FaqDto.cs                   FaqDto · SaveFaqDto · FaqListDto
DTOs/QnaDto.cs                   QnaPostDto(children 재귀) · Create/Update · Visibility · List
Services/IFaqService.cs          + FaqService.cs
Services/IQnaService.cs          + QnaService.cs
Services/RichTextSanitizer.cs    본문 HTML 허용 목록 세탁 (+ 영상 임베드)
Endpoints/FaqEndpoints.cs        /faqs
Endpoints/QnaEndpoints.cs        /qna
```

고친 것: `Data/AppDbContext.cs`(DbSet 둘) · `Program.cs`(등록) ·
`Services/IMenuService.cs` · `Services/MenuService.cs`(`GetEffectivePermissionAsync`) ·
`AuthServer.csproj`(HtmlAgilityPack)

게이트웨이는 `/api/auth/{**remainder}` 로 이미 통째로 넘기므로 **손댈 것이 없다.**

| 메서드 | 경로 | 누가 |
|---|---|---|
| GET | `/api/auth/faqs?keyword=&category=` | 로그인한 모두 (비활성은 관리자만) |
| GET | `/api/auth/faqs/{id}` | 로그인한 모두 |
| POST · PUT · DELETE | `/api/auth/faqs[/{id}]` | 관리자 |
| GET | `/api/auth/qna?keyword=&filter=&page=&pageSize=` | 로그인한 모두 (보이는 것만) |
| GET | `/api/auth/qna/{id}` | 스레드 하나 |
| POST | `/api/auth/qna` | `can_create` — `parentId` 있으면 답글 |
| PUT · DELETE | `/api/auth/qna/{id}` | 본인 또는 관리자 |
| PUT | `/api/auth/qna/{id}/visibility` | 관리자 |

### 프론트

```
src/api/portal/faq/index.ts                          신규
src/api/portal/qna/index.ts                          신규
src/views/funeral/help/faq/index.vue                 임시 화면 → 실제 화면
src/views/funeral/help/qna/index.vue                 임시 화면 → 실제 화면
src/views/funeral/help/qna/modules/qna-post.vue      신규 (자기를 다시 부르는 재귀 부품)
src/components/rich-editor/video-embed.ts            신규 (영상 tiptap 노드 + 주소 해석)
src/components/rich-editor/video-embed-modal.vue     신규 (영상 넣기 창)
src/components/rich-editor/rich-editor.vue           영상 버튼 · HTML 직접 입력 · 주소 붙여넣기
src/views/funeral/help/qna/modules/author-avatar.vue 신규 (사진 또는 이름 첫 글자)
```

`rich-editor.vue` 는 공지 등 다른 화면도 쓰는 공용 부품이다.
새 기능은 **둘 다 기본 꺼짐**(`allowVideo` · `htmlSource`)이라
켜지 않은 화면은 전과 똑같이 동작한다.

`src/api/funeral/help/index.ts` 의 FAQ·Q&A 함수는 여전히 붙지 않은 껍데기다.
`*-custom` 화면들이 아직 참조하고 있어 지우지 않고, 파일 머리에 어디가 실제인지 적어 두었다.

### SQL

`docs/sql/help_faq_qna.sql` — **2026-08-25 실행했다.** 반복 실행해도 안전하다.
테이블 둘, 메뉴가 쓰는 권한 항목, 역할 권한을 만든다.

### 실행하면서 설계를 한 번 고쳤다 — 기록해 둔다

처음 쓴 스크립트는 역할 이름을 코드에 박지 않으려고 관리자 역할을 이렇게 잡았다.

> "관리자 화면(`/system%` · `/auth%`)을 **수정**할 수 있는 역할"

실행 전에 그 조건을 조회해 보니 **네 역할이 전부 잡혔다.** `PARTNER`(계정 39개)까지 들었다.
`role_partner_tighten.sql` 을 아직 실행하지 않아 PARTNER 가 그 권한을 그대로 갖고 있었기 때문이다.
그대로 돌렸으면 파트너 39명이 F.A.Q 를 쓰고 Q&A 를 공개할 수 있게 되어 **지시와 반대**가 된다.

그래서 추측을 버리고 명시 목록(`ADMINISTRATOR` · `SYSTEM_ADMINISTRATOR`)으로 바꿨다.
바꿀 곳은 스크립트 안의 임시 표 한 곳이다.

또 하나. `role_menu_backfill.sql` 이 "메뉴가 쓴다고 지정한 항목은 모두 허용" 으로 채워 둔 탓에
**네 역할 모두 이미 F.A.Q 를 등록·수정·삭제할 수 있었다.** 지시대로 만들려면 켜는 것만으로는
안 되고 관리자가 아닌 역할에서 **꺼야** 했다. 스크립트가 그 일까지 한다.
되돌리려면 역할 권한 화면에서 다시 켜거나 `role_menu_backfill.sql` 을 실행한다.

## 6. 준수사항

- **3번 (팝업 이동)** — ant 모달은 `plugins/draggable-modal.ts` 가 앱 시작 시
  전역으로 걸어 준다. 두 화면의 모달 모두 헤더가 있으므로 그대로 잡아 옮길 수 있다.
- **4번 (세로 스크롤)** — 조회 줄과 쪽 넘기기는 위·아래에 고정하고 목록만 안에서 스크롤한다
  (`flex h-full flex-col` + `min-h-0 flex-1` + 안쪽 `overflow-auto`).
  16번 문서가 정리한 `h-full` 함정을 피하려고 `Page` 부품을 건드리지 않고 이 화면 안에서만 잡았다.
- **5번 (글꼴)** — 바깥 CDN 을 쓰지 않았다.

## 7. 확인한 것 · 확인하지 못한 것

```
dotnet build AuthServer      오류 0
pnpm vite build (포털)        성공 — faq · qna 청크 생성 확인
vue-tsc                      새 파일 오류 0 (기존 49건은 다른 화면들)
API 실동작 (게이트웨이 경유)   43개 검사 전부 통과
화면 (localhost:5555)         두 화면 렌더링 · 등록 · 목록 갱신 확인
```

### API 실동작

게이트웨이(`:5265`)를 지나는 실제 요청으로 세 계정을 써서 확인했다 —
`vben`(ADMINISTRATOR) · `bmkim`(PARTNER) · `choisunghyun`(PARTNER, 제3자).
확인한 것 중 중요한 것들:

- 일반 사용자 F.A.Q 등록·삭제 → 403. 비활성 F.A.Q 는 일반 사용자에게 안 보이고 관리자에게만 보인다
- 일반 사용자가 `isPublic: true` 를 보내도 **무시되고 비공개로 들어간다**
- 비공개 질문: 작성자 본인 ✅ · 관리자 ✅ · 제3자 ✗
- 관리자 답변은 `isAnswer=true` · 기본 공개
- 답글 → 답글 → 답글 → 답글 (depth 1·2·3·4) 모두 정상. 깊이 제한 없음
- **뿌리가 비공개면 공개 답변이 달려 있어도 제3자에게 스레드가 안 보인다**
- 답변 하나만 비공개로 바꾸면 **그 아래 답글까지 함께 감춰진다** (`replyCount` 4 → 0)
- 남의 글 수정·공개 전환 → 403
- 본문 세탁: `<script>` 제거 · `javascript:` href 제거 · `on*` 속성 제거 · `<iframe>` 제거,
  정상 이미지(`/api/file/...`)는 남음
- 빈 본문(`<p></p>`) → 400
- 스레드 삭제 시 답글까지 함께 지워지고 이후 조회는 404

시험 데이터는 모두 지웠다(soft delete). 살아 있는 행은 0개다.

### 화면

`/help/faq` · `/help/qna` 를 실제로 띄워 확인했다.

- F.A.Q — 분류별 묶음(`계정 2건` · `사용법 1건` …) · 분류 고르개 · 펼치면 답변 HTML 이
  서식(`<strong>` · `<ul>`)까지 그려진다 · 비활성 항목에 `중지` 표시(관리자만)
- F.A.Q 등록 창 — 분류·순서·질문·답변(편집기)·활성 모두 표시, 저장 후 "F.A.Q 를 등록했습니다"
  토스트와 목록 갱신 확인
- Q&A — 관리자에게만 `공개 대기` 고르개가 붙는다 · `답변 완료`/`답변 대기`·`비공개` 표시 ·
  답글 수 표시 · `스레드 전체 공개`/`전체 비공개` 전환 버튼
- Q&A 5단 스레드가 들여쓰기 `0 · 20 · 40 · 60 · 80px` 로 그려지고
  관리자 글에만 `답변` 표시가 붙는다
- **준수사항 4** — 두 화면 모두 바깥·목록 안·문서 모두 세로 스크롤 0

### 영상 넣기 (2026-08-25 추가분)

세탁기를 직접 부르는 시험으로 **28개 검사 전부 통과**했다.
서버를 다시 띄우지 않아도 되도록 `RichTextSanitizer` 를 그대로 참조해 확인했다.

- 지시에 함께 온 삽입 코드가 그대로 살아남는다 —
  `src`(재생 옵션 `autoplay=1`·`loop=1` 포함) · `width`·`height` ·
  `style` 의 `height:480px` · `title` · `allow` · `allowfullscreen`
- 막힌 것: 허용 목록 밖 호스트 · 상대경로 · `javascript:` · `data:` ·
  호스트 흉내(`youtube.com.evil.com`) · `srcdoc` · `name` · `sandbox` ·
  `position:fixed` 덮기 · `style` 의 `url()` · `onload` · iframe 안에 숨긴 `<script>` ·
  `<object>`·`<embed>`
- `http` → `https` 로 바뀐다
- 영상만 있는 본문도 '내용 있음' 으로 본다

화면(vite dev)에서 확인한 것:

- HTML 버튼 — 원문이 그대로 보이고, 손으로 쓴 HTML(문단 + iframe)이
  편집기로 돌아와도 **둘 다 살아남는다**

### 영상 넣기 창 · 아바타 (4-2 · 4-3)

화면에서 확인한 것:

- 영상 버튼을 누르면 **브라우저 기본 창이 아니라 우리 창**이 뜬다.
  빈 상태에서는 [넣기] 가 잠겨 있다
- 허용 목록 밖 주소(`evil.example.com`) → 그 자리에 오류 문구, [넣기] 잠김 유지
- `watch?v=CaEg-7UEEEI` → `YouTube` · 영상 아이디 표시 · 미리보기가 `/embed/CaEg-7UEEEI` 로 뜸
- 삽입 코드를 통째로 넣으면 **가로 816 · 세로 480 이 채워지고 `직접 지정` 으로 바뀌며**,
  주소에 있던 `autoplay·mute·loop·controls=0` 이 체크로 읽힌다.
  '소리 끔' 은 자동 재생 때문에 잠긴다. 미리보기 주소에서는 자동 재생·반복이 빠져 있다
  (`?mute=1&controls=0`)
- [넣기] → 편집기에 `src`(옵션 전부 + `playlist=`) · `width` · `height` ·
  `max-width:100%` · `title` 이 들어가고 뒤에 빈 문단이 생긴다
- **편집기에 `https://youtu.be/…` 를 붙여넣기** → 창을 열지 않고 바로 영상이 됐다
- Q&A 아바타 — 목록과 글 양쪽에 그려진다. 사진이 없는 계정은 이름 첫 글자에
  이름에서 정한 색(김 초록 · 이 파랑)이 들어간다. 세로 스크롤은 여전히 0
- 썸네일 경로 확인 — `/api/file/download/<id>` 255KB(JPEG) ·
  `/api/file/thumbnail/<id>` 2.4KB(WebP), 둘 다 200

### 확인하지 못한 것

**서버(`.cs`) 수정분은 실제 요청으로 확인하지 못했다.**
6개 서비스가 `dotnet run --no-build` 로 떠 있어 자동 재빌드가 없고,
실행 중이던 AuthServer 는 이 수정들 이전 빌드였다. 해당하는 것 둘:

| 무엇 | 어떻게 확인했나 |
|---|---|
| 영상 임베드 허용 (`allowEmbeds`) | 세탁기를 직접 부르는 28개 검사로 확인 |
| Q&A 작성자 아바타 (`AuthorAvatar`) | **확인하지 못했다.** 화면은 사진이 없을 때(이름 첫 글자)만 확인했다 |

아바타는 사진을 가진 계정이 하나(`quristyle`)뿐이고, 그 사진이 실제로 내려오는지는
AuthServer 를 다시 띄운 뒤에 봐야 한다. 썸네일 경로 자체는 200 으로 확인했다.

> 이 저장소는 `dotnet watch` 를 쓴다고 적혀 있지만 실제로 떠 있던 것은 `--no-build` 였다.
> `.cs` 를 고친 뒤에는 해당 서비스를 다시 띄워야 한다.

**Popconfirm(삭제 확인)을 화면에서 눌러 보지 못했다.** 자동화 브라우저 창이 표시되지 않아
합성(compositing)이 멈춘 상태라 합성 마우스 이벤트가 Vue 핸들러에 닿지 않는다.
같은 조건에서 **기존 공지 관리 화면(`/portal/notice`)의 Popconfirm 도 똑같이 반응하지 않았다** —
이 저장소가 이미 쓰는 패턴(`Popconfirm > Tooltip > Button`)을 그대로 따랐고 코드 차이는 없다.
삭제 자체는 API 로 확인했다(본인 글·관리자 삭제·403·404 전부).

`PARTNER` 계정으로 화면에 로그인해 보지는 않았다 — 사용 중인 브라우저 세션을 건드리게 된다.
일반 사용자 관점은 API 로 대신 확인했다(위 목록).

---

## 판단이 필요한 것

### D-H1. `PARTNER_ADMINISTRATOR` 를 관리자로 볼지

관리자로 넣은 것은 `ADMINISTRATOR` · `SYSTEM_ADMINISTRATOR` 둘이다.
`PARTNER_ADMINISTRATOR`(파트너 관리자)는 **넣지 않았다.**

- 이름은 관리자지만 파트너 쪽 관리자다.
- 지금 만든 Q&A 는 회사별로 갈라져 있지 않다 — 포털 전체가 하나의 Q&A 다.
  그 역할에 `can_cust1` 을 주면 **남의 회사 질문까지** 공개하고 답할 수 있게 된다.
- 배정된 계정이 0개라 지금 당장 달라지는 것은 없다.

넣기로 하면 `help_faq_qna.sql` 의 임시 표에 한 줄 더하고 다시 실행하거나,
역할 권한 화면에서 `/help/faq` 의 등록·수정·삭제와 `/help/qna` 의 `답변·공개 관리` 를 켜면 된다.

회사별로 Q&A 를 가르는 것이 맞다면 그건 더 큰 변경이다(`qna_posts` 에 회사 구분을 두고
조회에 회사 조건을 걸어야 한다). 지금은 공지와 같은 '포털 공통' 방침을 따랐다.

### D-H2. 역할이 하나도 없는 계정을 막지 않는다

`GetEffectivePermissionAsync` 는 권한 정보가 **아예 없는** 계정(역할 미배정)을
'전부 허용' 으로 본다. 화면 쪽 규칙(`useMenuPermission` · `v-perm` · `can()`)과 같게 맞춘 것이다.
한쪽만 엄격하면 버튼은 보이는데 저장이 안 되는 상태가 된다.

**그 계정은 F.A.Q 를 쓰고 Q&A 를 공개할 수 있다.** 새로 만든 이 두 화면만의 문제가 아니라
포털 전체가 지금 그렇게 동작한다(계정 관리도 마찬가지다). 이 방침을 바꾸려면
`store/menu-permission.ts` 의 `hasAnyData` 주석에 적힌 판단부터 다시 정해야 한다 —
여기서 혼자 다르게 하면 두 곳이 어긋난다.

### D-H3. Q&A 에 첨부파일을 둘지

지금은 본문에 이미지를 붙여넣는 것만 된다(FileServer 로 올라가고 본문에는 경로만 남는다).
공지처럼 파일 목록을 따로 두지는 않았다. 로그 파일·문서를 올릴 일이 있으면 필요하다.
공지의 `notice_files` 구조를 그대로 가져오면 된다.

### D-H4. 남아 있는 `*-custom` 화면

`views/funeral/help/faq-custom` · `archive-custom` · `inquiry-custom` 은
붙지 않은 API(`api/funeral/help`)를 그대로 참조하는 생성 흔적이다.
메뉴에 걸려 있는지 확인한 뒤 정리하는 것이 좋다. 이번에는 손대지 않았다.

### D-H5. `/help/archive` (자료실) ✅ **계속 사용 (2026-09-04)**

> 지시: "/help/archive 는 계속 사용할 화면이다."

'임시 화면' 이라는 표현이 지나쳤다 — 확인해 보니 **온전한 기능**이다:
전용 API 모듈(`api/portal/help-archive`) · AuthServer 백엔드
(`HelpArchiveEndpoints` + 엔티티 + DbContext) · 메뉴 등록(status 1) ·
파일 업로드(`bizType=help-archive`)까지 갖춰져 있고, 화면을 열어 정상 동작
(빈 목록 표시 · 콘솔 오류 0)을 확인했다. 정식 화면으로 유지한다.

`/help/inquiry`(문의)는 **없앴다** — Q&A 가 같은 일을 하기 때문이다(아래 8절).

---

## 8. `/help/inquiry` 제거 (2026-08-25)

> 지시: "`/help/inquiry` 는 문의를 위해 준비한 화면이다. QnA 화면이 그 역활을 하니
> `/help/inquiry` 의 화면은 삭제하라. 메뉴에서도 제거하라."

Q&A 가 같은 일을 한다 — 사용자가 묻고 관리자가 답한다. 둘을 함께 두면 사용자가
어디에 물어야 할지 고르게 되고 관리자는 두 곳을 봐야 한다.

지운 것:

```
views/funeral/help/inquiry/index.vue          임시 화면
views/funeral/help/inquiry-custom/index.vue   붙지 않은 생성 흔적
api/funeral/help/index.ts                     Inquiry · Qna 함수와 타입
docs/sql/help_inquiry_drop.sql                메뉴 · 역할 권한 (실행 완료)
```

메뉴 행까지 지웠다(soft 가 아니라 DELETE). 화면 파일이 없어졌으므로 행을 남기면
**없는 파일을 가리키는 경로**가 된다. 공지 때와 같은 방식이다(`notice_menu.sql`).
되돌리는 SQL 은 스크립트 아래에 그대로 적어 두었다.

실행 결과 5행(역할 권한 4 + 메뉴 1). 즐겨찾기에 담아 둔 사람은 없었다.
도움말 아래에는 Q&A · F.A.Q · 자료실 셋이 남는다.

`api/funeral/help/index.ts` 의 Q&A 함수도 함께 지웠다 — 실제 Q&A 는
`#/api/portal/qna` 로 옮겼고 이 파일의 것은 아무도 쓰지 않았다.
`faq-custom` · `archive-custom` 이 아직 참조하는 F.A.Q·자료실 함수만 남겼다.

> 헬프데스크의 '문의하기'(`/helpdesk/contact-us`)는 다른 화면이다. 건드리지 않았다.
