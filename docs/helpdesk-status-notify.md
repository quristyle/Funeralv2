# 요청글이 접수·완료·종료 되면 알린다 (앱푸시 · 이메일)

헬프데스크 요청글을 올린 사람은 **「완료」가 눌린 것을 그 글을 다시 열어야만**
알았다. 알림 코드는 분명히 있었는데 **한 통도 안 나갔다.** 이 문서는 그 까닭과,
고친 길과, 받는 사람이 끄는 자리를 적는다.

> 2026-10-12. 신고된 자리는 `/helpdesk/request/detail/17` 의 「완료」다.

---

## 한눈에

| | |
|---|---|
| 보내는 자리 | `HelpDeskServer/Services/RequestStatusNotifier.cs` |
| 거는 자리 | `RequestEndpoints` 의 `PUT /api/requests/accept/{id}` |
| 주인 풀이 | `HelpDeskServer/Services/RequestOwnerResolver.cs` (댓글 알림과 **같은 것**) |
| 통로 | `HelpDeskServer/Services/NotifyGateway.cs` |
| 이벤트·구분 | `HELPDESK_STATUS` (`JSini.Shared.DTOs.NotificationEvents.HelpDeskStatus`) |
| 설정 | 포털관리 › 시스템관리 › **알림관리** · 내 정보 › **환경설정** |
| SQL | `deploy/sql/helpdesk-status-notify-2026-10-12.sql` (2026-10-12 운영 반영) |

---

## 무엇이 잘못돼 있었나 — **보낼 곳이 0 이었다**

```
  「완료」 누름
      │
      ▼
 RequestEndpoints ──▶ PushUtil.SendPushMsg(…, customerSubscriptions, …)
                                   │
                                   ▼
                      helpdesk.pushsubscriptions   ← 0 줄 (옛 Vue 포털이 채우던 표)
                                   ✗

                      scom.push_subscriptions      ← 기기 6 대 (지금 포털이 채운다)
```

구독을 담은 표가 **둘**이고, 보내는 쪽이 **안 쓰는 쪽**을 보고 있었다.
옛 Vue 포털은 2026-09-05 에 걷어냈고, 지금 포털(`:5557`)에서 알림을 켜면
구독은 알림 서비스의 `scom.push_subscriptions` 에 들어간다.

그래서 완료 알림은 **받을 기기가 한 대도 없는 채로 조용히 성공**했다 —
오류도 없고, 발송 이력에도 줄이 안 남는다. 「안 온다」는 신고 말고는 알 길이 없다.

**메일도 같은 꼴이었다.** 접수·완료 메일이 받는 사람을 고르는 조건은
`helpdesk.userproperty` 의 `receiveEmail = true` 인데 **그 표가 0 줄**이라
언제나 빈 목록이었다(`AdminService.GetCustomerEmailsForNotificationAsync`).

운영에서 센 숫자 (2026-10-12) —

| 표 | 줄 |
|---|---|
| `helpdesk.pushsubscriptions` | **0** |
| `helpdesk.userproperty` | **0** |
| `scom.push_subscriptions` | 6 (글쓴이 본인 기기만 4) |

> **「요청 등록」만 멀쩡했던 까닭이 이것이다.** 그 하나는 이미 알림 서비스를
> 부르고 있었다(`roles: ["SYSTEM_ADMINISTRATOR"]`). 신고에 「작성시 알림은
> 온다」가 적혀 있던 것은 두 길이 다르다는 표시였다 —
> `scom.push_send_logs` 에도 `HELPDESK` 구분으로 **등록 알림만** 쌓여 있었다.

댓글 알림이 먼저 같은 까닭으로 몇 달을 못 닿았다
([docs/helpdesk-comment-notify.md](helpdesk-comment-notify.md)). 고친 길도 같다.

---

## 고친 것

`RequestStatusNotifier` 하나가 이 일을 한다. 상태를 저장한 뒤
`PUT /api/requests/accept/{id}` 가 부른다.

### 1. 받는 사람은 상태마다 다르다

| 상태 | 받는 사람 | 앱푸시 | 이메일 |
|---|---|---|---|
| **접수**(`InProgress`) | 글을 쓴 **작성자** | ○ | ○ |
| **완료**(`Completed`) | 글을 쓴 **작성자** | ○ | ○ |
| **종료**(`UserCompleted`) | 그 글의 **접수자** | ○ | ※ 옛 길 |

반려·협의·논의·삭제는 알리지 않는다 — 옛 길에서도 안 보냈고, 늘리는 것은 이
고침의 몫이 아니다.

### 2. 사람을 포털 계정으로 푼다

요청글은 사람을 **헬프데스크 내부 번호**로 가리키는데(`customerid`·`adminid`)
알림 서비스가 아는 사람은 **포털 로그인 아이디**뿐이다. 그 둘을 잇는 정본이
`helpdesk.auth_user_links` 다.

| | 먼저 | 받침 |
|---|---|---|
| 작성자 | 계정 연결표(`customer`) | `improvementrequest.createdby` |
| 접수자 | 계정 연결표(`admin`) | `admin.loginid` |

`system` 은 사람이 아니므로 보낼 곳이 아니다.

**이 풀이는 댓글 알림과 한 곳을 쓴다**(`IRequestOwnerResolver`). 두 벌로 두면
「댓글은 오는데 완료는 안 온다」가 생기고, 그 증상으로는 어느 쪽이 틀렸는지
알 수가 없다.

### 3. 누른 사람 자신은 받지 않는다

담당자가 완료를 누를 때마다 제 휴대폰이 울리면 받는 사람은 알림을 통째로
꺼 버린다. 작성자가 스스로 종료하는 일도 실제로 있다.

### 4. 못 보내도 상태는 남는다

알림 서비스가 죽어 있어도 **예외를 위로 던지지 않는다**(`NotifyGateway`).
상태는 이미 저장됐는데 화면에 「실패했습니다」가 뜨면 사람은 같은 단추를
한 번 더 누른다. 사유는 로그로만 남는다.

---

## 끄는 자리 — **코드가 아니라 표가 정한다**

부르는 쪽은 **이벤트 코드만** 실어 보낸다(`HELPDESK_STATUS`). 「이 사람이
받을 사람인가」는 알림 서비스가 푼다 — 순서는 다른 알림과 같다.

| | 누가 정하나 | 어디서 고치나 |
|---|---|---|
| ① 정책 | 회사 | 포털관리 › 시스템관리 › **알림관리** |
| ② 본인 설정 | 그 사람 | 내 정보 › **환경설정** |
| ③ 구독·주소 | 기기와 계정 | 브라우저 알림 허용 · 계정 관리 |

**정책 줄은 하나도 넣지 않았다.** 줄이 없는 이벤트는 「제한 없음」이라 지금까지와
똑같이 나간다 — 기본을 「아무도 못 받음」으로 두면 이 고침을 넣은 날 알림이
통째로 멎는데, 그 증상은 「알림이 안 온다」 하나라서 원인이 표로 보이지 않는다
([docs/notify-policy.md](notify-policy.md) 의 「조용히 막지 않는다」).

**대상이 당사자(`USER`)라 정책의 역할은 거름막이다.** 역할을 한 줄이라도 켜면
그 역할을 가진 사람만 받는다.

---

## 갈라 둔 자리 둘

### 왜 `HELPDESK` 와 구분을 나눴나

| | `HELPDESK` | `HELPDESK_STATUS` |
|---|---|---|
| 무슨 일 | 요청이 **올라왔다** | 내 요청이 **처리됐다** |
| 받는 사람 | 처리할 **역할** | 기다리던 **당사자** |
| 대상 성격 | `ROLE` | `USER` |

받는 사람도 끄는 뜻도 다르다. 한 갈래로 묶으면 「내 글 처리 알림만 끄고 싶다」를
적을 자리가 없어진다.

### 왜 종료 메일만 옛 길에 남겼나

접수자 메일은 `admin.email` 로 **바로** 가므로 그 길은 실제로 살아 있다.
알림 서비스로 옮기면 포털 계정이 아닌 담당자에게 메일이 끊긴다 — 고치려다
되려 끊는 쪽이다. 앱 푸시는 새 길로 함께 보내므로 **여기서 또 내지 않는다**
(두 통이 된다).

같은 까닭으로 이벤트의 `email_from_push` 는 **거짓**이다. 메일은 헬프데스크가
제 틀로 따로 내므로, 곁가지를 켜면 한 번 완료할 때 메일이 두 통 간다.

---

## DB 에 넣은 것

`deploy/sql/helpdesk-status-notify-2026-10-12.sql` — 2026-10-12 운영(`jsiniportal`) 반영 확인.

| | 무엇 |
|---|---|
| `scom.notification_events` | `HELPDESK_STATUS` 한 줄 (`USER` · 푸시·메일 · `governed`) |
| `scom.common_codes` | `NOTI_CATEGORY` 에 `HELPDESK_STATUS`(「헬프데스크 처리」, 차례 36) |
| `scom.notification_events` | `HELPDESK_LEGACY` 의 이름·설명을 **서비스 점검만**으로 좁힘 |
| `scom.notification_policies` | **아무것도 안 넣음** (= 제한 없음) |

`HELPDESK_LEGACY` 를 지우지 않은 까닭은 **아직 옛 길로 가는 것이 남아서**다 —
`HealthCheckWorker` · `AutoCheckWorker` 의 서비스 점검 알림이다. 설명을 그대로
두면 관리자가 「완료 알림을 여기서 끈다」고 읽는다.

---

## 확인한 것 (2026-10-12)

헬프데스크(`:55400`)를 운영 DB 상대로 따로 띄우고, 알림 서비스 자리에는 **대역**을
세워 무엇이 나가는지 받아 적었다. 실제로 나간 알림·메일은 **없다**.

| 몬 것 | 결과 |
|---|---|
| 완료(작성자 ≠ 누른 사람) | 푸시 1 · 메일 1 — `owners: [{jsini, quristyle}]` · `eventCode HELPDESK_STATUS` |
| 접수 | 푸시 1 · 메일 1 — 제목 「접수 - 이순열」 |
| 종료 | 푸시 1 — 접수자에게. **메일은 안 냄**(옛 길이 낸다) |
| 작성자 본인이 완료 | **0 통** |

알림 주소는 `/helpdesk/request/detail/17`, 메일의 고리는
`https://portal.jsini.co.kr/helpdesk/request/detail/17` 이다.

몰기 위해 바꾼 요청글 17 의 칸(`status`·`acceptedat`·`completededat`·
`usercompletededat`·`modifiedby`·`modifiedat`)은 **원래 값으로 되돌려 놓았다.**
