# 요청글이 접수·완료·종료 되면 알린다 (앱푸시 · 이메일)

헬프데스크 요청글을 올린 사람은 **「완료」가 눌린 것을 그 글을 다시 열어야만**
알았다. 알림 코드는 분명히 있었는데 **한 통도 안 나갔다.** 이 문서는 그 까닭과,
고친 길과, 받는 사람이 끄는 자리를 적는다.

> 2026-10-12. 신고된 자리는 `/helpdesk/request/detail/17` 의 「완료」다.
>
> **같은 날 두 번 고쳤다.** 첫 번째는 보낼 곳이 빈 구독표였고(아래),
> 두 번째는 **우리가 그때 넣은 걸러냄**이 작성자와 누른 사람이 같은 가장 흔한
> 길을 막은 것이었다(`/helpdesk/request/detail/19`) —
> [누른 사람을 뺐더니 가장 흔한 길이 막혔다](#누른-사람을-뺐더니-가장-흔한-길이-막혔다-2026-10-12-같은-날-두-번째).

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

### 3. 누른 사람이 곧 받을 사람이어도 보낸다

> 처음에는 반대로 했다가 **그것이 두 번째 고장이 되었다.** 아래 「누른 사람을
> 뺐더니…」 참고.

「제가 누른 것이 제 휴대폰을 울리면 시끄럽다」는 까닭으로 받는 사람과 누른
사람이 같으면 조용히 돌아섰는데, 이 회사에서는 **한 사람이 두 모자를 쓴다** —
글을 올린 사람이 곧 시스템 관리자라 접수도 완료도 제가 누른다. 그래서 그
걸러냄이 **가장 흔한 길을 통째로 막았다.**

시끄러운 사람이 끄는 자리는 **환경설정**이다. 부르는 쪽에 걸러냄을 하나 더
두면 그것은 **아무도 끌 수 없고 기록에도 안 남는 설정**이 된다.

> **댓글 알림은 지금도 자기 것을 거른다**([docs/helpdesk-comment-notify.md](helpdesk-comment-notify.md)).
> 일부러 다르게 둔다 — 내가 방금 타이핑한 글이 나에게 다시 오는 것과, 내 글이
> **어떤 상태가 되었는지**가 알림함에 영수증으로 남는 것은 다른 일이다.

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
| 작성자 본인이 완료 | **0 통** ← **이 줄이 다음 고장이었다.** 아래를 본다 |

알림 주소는 `/helpdesk/request/detail/17`, 메일의 고리는
`https://portal.jsini.co.kr/helpdesk/request/detail/17` 이다.

몰기 위해 바꾼 요청글 17 의 칸(`status`·`acceptedat`·`completededat`·
`usercompletededat`·`modifiedby`·`modifiedat`)은 **원래 값으로 되돌려 놓았다.**

---

## 누른 사람을 뺐더니 **가장 흔한 길이 막혔다** (2026-10-12, 같은 날 두 번째)

위의 고침을 운영에 올린 바로 그날, 같은 신고가 다시 들어왔다 —
`/helpdesk/request/detail/19` 에서 「완료」를 눌렀는데 **앱 알림이 안 온다.**

이번에는 구독표가 아니라 **우리가 넣은 걸러냄**이 원인이었다.

```
  「완료」 누름 (quristyle)
      │
      ▼
 RequestStatusNotifier
      │  작성자 풀이 → quristyle
      │  누른 사람  → quristyle
      ▼
  targetLoginId == actorLoginId  →  조용히 return
      ✗ 알림 서비스를 부르지도 않는다
```

### 왜 못 알아챘나 — **실패 줄조차 안 남는다**

돌아서는 자리가 `_gateway.PostAsync` **앞**이라 `scom.push_send_logs` 에
아무것도 안 쌓인다. 「구독한 기기 없음」 같은 실패 줄이라도 남으면 까닭이
보이는데, 그것조차 없으니 **「보낼 곳이 없었다」와 「보내지 않기로 했다」가
기록에서 똑같아 보인다.** 앞 고침의 증상과 구분이 안 되는 것이다.

실제로 요청 19 의 기록은 이랬다 — 같은 1초 안에 **등록 알림만** 나가 있다.

| 시각(UTC) | 무엇 | 받는 이 | 결과 |
|---|---|---|---|
| 04:41:11~12 | 새 헬프데스크 요청 (`HELPDESK`) | quristyle | 성공 ×4 (기기 4대) |
| 04:41:20 | **완료** (`HELPDESK_STATUS`) | — | **줄 자체가 없음** |

### 전제가 틀렸다 — 여기서는 한 사람이 두 모자를 쓴다

「담당자가 완료를 누를 때마다 제 휴대폰이 울리면 시끄럽다」는 **작성자와
담당자가 다른 사람일 때** 맞는 말이다. 그런데 이 회사에서 글을 올리는 사람이
곧 시스템 관리자다(`quristyle` — `customer.id = 2` · `admin.id = 3` 둘 다).
그래서 **접수도 완료도 제가 누른다** — 걸러냄이 예외가 아니라 **기본 경로**를
막았다.

**등록 알림은 이미 자기 것을 자기에게 보내고 있었다**(역할 기반 `HELPDESK`,
위 표의 4통). 등록은 울리는데 완료는 안 울리는 것은 받는 사람에게 규칙이 아니라
고장으로 보인다.

### 고친 것

`RequestStatusNotifier` 의 `targetLoginId == actorLoginId` 걸러냄을 **걷어냈다.**
이 문서가 처음부터 적어 둔 원칙으로 되돌린 것이다 —
**「누가 받나」는 부르는 쪽이 아니라 정책 → 본인 설정 → 구독이 정한다.**
부르는 쪽에 둔 걸러냄은 알림관리에도 환경설정에도 안 보이고 기록도 안 남아서,
**아무도 끌 수 없고 아무도 켤 수 없는 설정**이 된다.

`actorLoginId` 는 그대로 받는다 — 알림에 얹을 **얼굴**(`iconOwnerKey`)을
정하는 데만 쓴다.

### 다시 확인한 것

같은 자리(헬프데스크 `:55400` + 알림 대역 `:55460`)에 **버리는 요청글 하나**를
새로 넣어 몰았다(`customer` · `auth_user_links` · `improvementrequest` 세 줄 —
끝나고 지우고 개수를 다시 셌다. 사람이 쓴 요청 8건은 손대지 않았다).

| 상태 | 누른 사람 | 고치기 전 | 고친 뒤 |
|---|---|---|---|
| 접수 | **작성자 본인** | — | 푸시 1 · 메일 1 |
| 접수 | 남 | — | 푸시 1 · 메일 1 |
| 완료 | **작성자 본인** | **0 통** | **푸시 1 · 메일 1** |
| 완료 | 남 | 푸시 1 · 메일 1 | 푸시 1 · 메일 1 |
| 종료 | **접수자 본인** | 0 통 | 푸시 1 (메일은 옛 길) |
| 종료 | 작성자 | 푸시 1 | 푸시 1 |
| 반려 | 남 | 0 통 | 0 통 (알리지 않는 상태) |

고친 뒤 실제로 나간 푸시의 속은 이랬다 — 실제 발송은 **없다**(대역이 받았다).

```json
{"owners":[{"ownerType":"jsini","ownerKey":"probe542"}],
 "message":{"title":"완료 - 이순열","body":"탐침 542 — 완료 알림 재현",
            "url":"/helpdesk/request/detail/9901","iconOwnerKey":"probe542",
            "tag":"helpdesk-request-9901",
            "category":"HELPDESK_STATUS","eventCode":"HELPDESK_STATUS"}}
```

### 알림 서비스 쪽은 멀쩡했다

막힌 곳이 하나뿐임을 먼저 셌다(2026-10-12 운영) —

| 본 것 | 값 |
|---|---|
| `scom.notification_events` 의 `HELPDESK_STATUS` | 있음 (`USER` · 푸시·메일 · 활성) |
| `scom.notification_policies` 의 `HELPDESK_STATUS` | **0 줄** (= 제한 없음) |
| `scom.notification_preferences` 의 `quristyle` | `push_enabled = t` · `email_enabled = t` |
| `scom.push_subscriptions` 의 `quristyle` | **기기 4 대** (모두 살아 있음) |

**DB 에 더 넣을 것은 없다.** 이 고침은 코드만 바뀐다.
