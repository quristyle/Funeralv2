# 쪽지 — 사람에게서 사람에게로

포털 사용자끼리 짧은 글을 주고받는다. 보내면 상대의 **쪽지함에 남고** 앱 푸시로
두드린다. 푸시 아이콘은 **보낸 사람의 프로필 사진**이다.

## 두드림은 **받는 사람이 정한다** (2026-09-24)

한동안 쓰는 화면에 「앱 푸시」·「메일」 체크가 있었다. 그런데 메일을 한 통 더
받을지는 **받는 쪽의 사정**이지 보내는 쪽이 매번 고를 일이 아니다. 체크 둘을
걷어내고 규칙을 둘로 굳혔다.

| | 규칙 |
|---|---|
| 앱 푸시 | **늘 간다.** 그리고 푸시를 **꺼 둔 사람은 쪽지를 아예 받지 못한다** |
| 메일 | **받는 사람이 켜 두었을 때만.** 개인설정 › 「쪽지 메일받기」 (기본 꺼짐) |

**푸시를 끈 사람을 막는 까닭.** 쪽지함에만 넣어 두면 본인은 왔다는 것조차
모르는데 보낸 쪽은 보냈다고 믿는다. 그 조용한 어긋남이 「그런 아이디가 없다」
보다 나쁘다. 찾기 목록에서 그런 사람은 **흐리게 보이고 고를 수 없으며**
(지우면 「아이디가 틀렸나」를 한참 의심하게 된다), 아이디를 손으로 적어 보내면
서버가 이름을 짚어 돌려준다(결과의 `blocked`).

**메일은 한 사람에 한 통씩** 나가고 회사 메일 틀을 입힌 **HTML** 이다
(`NoticeEmailTemplate` — 평문 갈래도 함께 싣는다). 주소를 쉼표로 이어 한 통으로
보내면 받는 사람들이 서로의 메일 주소를 보게 된다.

## 제목은 비워도 된다 (2026-09-24)

쓰는 화면의 제목 칸은 **접혀 있다** — 「제목 넣기」로 편다. 쪽지 대부분은 한두
줄이라 제목이 내용과 같은 말이 되는데, 받아야만 보낼 수 있게 두면 같은 글자를
두 번 치게 만드는 셈이다.

안 적으면 서버가 **「누가 언제 보냈다」**로 지어 넣는다
(`NoteEndpoints.DefaultTitle` — 「홍길동 님이 2026-09-24 14:05 에 보낸 쪽지」,
시각은 한국 시간대다). 빈 채로 두지 않는 까닭은 쪽지함 목록과 앱 푸시의 본문이
그 글자를 쓰기 때문이다 — 비우면 목록이 통째로 빈 줄이 된다.

**대신 내용이 비면 막는다.** 지어 줄 것이 없고, 빈 쪽지는 받는 사람에게 알림
한 번일 뿐 아무 말도 아니다.

## 어디에 있나

| | 자리 |
|---|---|
| 상단 띠의 ✉ | 어느 화면에서나 창으로 열린다. 안 읽은 쪽지 수가 숫자로 붙는다 |
| 메뉴 「알림 관리 › 쪽지 쓰기」 | `/admin/note/write` (`admin.note.write`) |
| 메뉴 「알림 관리 › 쪽지함」 | `/admin/note/box` (`admin.note.box`) — 받은함·보낸함 |

받는 사람은 **로그인 아이디와 이메일 주소를 섞어** 쉼표로 이어 적는다. 두 글자부터
아이디·이름·이메일로 찾아 주지만, **보내기는 그 결과를 쓰지 않는다** — 서버가 적힌
글자에서 다시 푼다(화면이 푼 아이디를 믿으면 브라우저에서 갈아 끼울 수 있다).

## 무엇이 어디에 있나

| 조각 | 파일 |
|---|---|
| 표 | `scom.notes` — [deploy/sql/notification-notes-2026-09-24.sql](../deploy/sql/notification-notes-2026-09-24.sql) |
| 쪽지 메일 스위치 | `scom.notification_preferences.note_email_enabled` — [deploy/sql/notification-note-email-2026-09-24.sql](../deploy/sql/notification-note-email-2026-09-24.sql) |
| 메뉴 | [deploy/sql/portal-menu-note-2026-09-24.sql](../deploy/sql/portal-menu-note-2026-09-24.sql) |
| 서버 | `microservices/NotificationServer/Endpoints/NoteEndpoints.cs` · `Services/NoteRecipientResolver.cs` · `Entities/Note.cs` |
| 화면 | `web/src/Shared/JSini.Web.Components/Settings/NoteWritePanel.razor` (쓰는 판, 상단 띠와 화면이 나눠 쓴다) |
| | `web/src/Shared/JSini.Web.Components/Settings/NotificationPanel.razor` (개인설정 — 「쪽지 메일받기」) |
| | `web/src/Apps/JSini.Web.Admin/Components/Pages/NoteBoxPage.razor` (쪽지함) |
| 클라이언트 | `web/src/Shared/JSini.Web.Components/Settings/NoteClient.cs` |

엔드포인트는 전부 `/api/notification/notes/*` 다.

```
POST   /api/notification/notes                 보내기
GET    /api/notification/notes/recipients?q=   받는 사람 찾기
GET    /api/notification/notes/inbox           받은함
GET    /api/notification/notes/sent            보낸함
GET    /api/notification/notes/unread-count    안 읽은 수 (상단 띠의 숫자)
POST   /api/notification/notes/{id}/read       읽음
DELETE /api/notification/notes/{id}            치우기 (내 쪽만)
```

## 「쪽지가 안 왔다」를 가르는 법

**쪽지가 간 것과 두드림이 간 것은 다른 일이다.** 서버는 쪽지함에 들어갔으면
성공으로 답하고, 두드림이 어디까지 갔는지는 결과에 따로 담는다. 실패로 답하면
보낸 사람이 같은 쪽지를 한 번 더 보내고 상대의 쪽지함에 두 통이 쌓인다.

1. **쪽지함에 있나** — 상대에게 `/admin/note/box` 를 열어 보게 한다. 있으면 글은
   갔고 두드림만 막힌 것이다. 그 까닭은 보낸함의 「알림」 칸에 적혀 있다
   (`scom.notes.notify_note`).
2. **푸시가 안 갔다** — 「구독한 기기 없음」이다(상대가 브라우저에서 알림을
   허용한 적이 없다). 서버에 VAPID 가 없으면 전원에게 안 간다 — 기동 로그 한
   줄이 그것을 말한다. **「본인이 푸시를 끔」은 이제 여기 안 걸린다** — 그
   사람에게는 쪽지 자체가 안 들어가고 보낸 쪽이 그 자리에서 이름을 듣는다.
3. **메일이 안 왔다** — 셋 중 하나다. 그 사람이 개인설정에서 「쪽지 메일받기」를
   **안 켰거나**(기본 꺼짐 — 대부분 이것이다), 그 계정에 대표 이메일이 없거나
   (`scom.account_profile_details` 의 `Email`), SMTP 설정이 비어 있다
   ([email-smtp.md](email-smtp.md)). 뒤의 둘은 보낸함의 「알림」 칸에 적힌다.
   **첫 번째는 적지 않는다** — 사고가 아니라 그 사람의 뜻이라서다.
4. **아예 안 보냈다** — 받는 사람을 못 푼 것이다. 보내기 결과의 `unknown`(그런
   아이디·이메일이 없다)과 `blocked`(푸시를 꺼 두었다)에 적힌 글자가 그대로
   화면에 뜬다.

## 지우기가 양쪽으로 갈린다

보낸 사람이 자기 보낸함에서 치워도 **받은 사람의 쪽지는 남는다.** 편지를 부친
사람이 남의 우편함을 비울 수는 없어서다. 양쪽이 다 치운 줄만 `is_deleted` 가
참이 된다 — 실제로 지우는 규칙은 아직 없다.

## 발송 기록(`push_send_logs`)에 얹지 않은 까닭

그 표는 **보낸 흔적**이다. 기기마다 한 줄이고, 알림이 남긴 자국이라 알림을 못
보내면 글도 함께 사라진다. 쪽지는 **글 자체가 본체**라 두드림이 둘 다 막혀도
남아야 하고, 언젠가 그 표에 정리 규칙이 붙어도 남의 편지가 함께 지워지면 안 된다.
