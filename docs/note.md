# 쪽지 — 사람에게서 사람에게로

포털 사용자끼리 짧은 글을 주고받는다. 보내면 상대의 **쪽지함에 남고**, 앱 푸시와
메일로 한 번 두드린다. 푸시 아이콘은 **보낸 사람의 프로필 사진**이다.

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
| 메뉴 | [deploy/sql/portal-menu-note-2026-09-24.sql](../deploy/sql/portal-menu-note-2026-09-24.sql) |
| 서버 | `microservices/NotificationServer/Endpoints/NoteEndpoints.cs` · `Services/NoteRecipientResolver.cs` · `Entities/Note.cs` |
| 화면 | `web/src/Shared/JSini.Web.Components/Settings/NoteWritePanel.razor` (쓰는 판, 상단 띠와 화면이 나눠 쓴다) |
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
2. **푸시가 안 갔다** — 흔한 것 둘이다. 「구독한 기기 없음」(상대가 브라우저에서
   알림을 허용한 적이 없다)과 「본인이 푸시를 끔」(`/admin/push/setting`).
   서버에 VAPID 가 없으면 전원에게 안 간다 — 기동 로그 한 줄이 그것을 말한다.
3. **메일이 안 갔다** — 그 계정에 대표 이메일이 없으면 보낼 데가 없다
   (`scom.account_profile_details` 의 `Email`). SMTP 설정이 비어 있으면 한 통도
   안 나간다 — [email-smtp.md](email-smtp.md).
4. **아예 안 보냈다** — 받는 사람을 못 푼 것이다. 보내기 결과의 `unknown` 에
   적힌 글자가 그대로 화면에 뜬다.

## 지우기가 양쪽으로 갈린다

보낸 사람이 자기 보낸함에서 치워도 **받은 사람의 쪽지는 남는다.** 편지를 부친
사람이 남의 우편함을 비울 수는 없어서다. 양쪽이 다 치운 줄만 `is_deleted` 가
참이 된다 — 실제로 지우는 규칙은 아직 없다.

## 발송 기록(`push_send_logs`)에 얹지 않은 까닭

그 표는 **보낸 흔적**이다. 기기마다 한 줄이고, 알림이 남긴 자국이라 알림을 못
보내면 글도 함께 사라진다. 쪽지는 **글 자체가 본체**라 두드림이 둘 다 막혀도
남아야 하고, 언젠가 그 표에 정리 규칙이 붙어도 남의 편지가 함께 지워지면 안 된다.
