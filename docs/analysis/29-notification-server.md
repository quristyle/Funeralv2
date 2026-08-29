# 알림 서비스 (NotificationServer) — 푸시·이메일을 셋이 공유한다

작성: 2026-08-27
대상: `microservices/NotificationServer/` · `ApiGateway/appsettings.json` ·
`docs/sql/push_subscriptions.sql` · `dev.bat` · `backend_run_ubuntu.sh`

> 지시: **D8 을 A 로 처리** — "NotificationServer 신설. 세 시스템이 공유.
> VAPID 키도 한 곳에서 관리."

---

## 1. 무엇이 문제였나

푸시·이메일이 **헬프데스크 안에만** 있었다.

- 포털도 장례식장도 알림을 보내려면 헬프데스크를 거쳐야 했다
- **VAPID 키가 두 서비스(파일로는 셋)에 중복으로 박혀 있었다** — D1 에서도 걸린 항목이다
- 대상 선택 로직이 발송 코드와 얽혀 헬프데스크 밖에서 쓸 수 없었다

마지막 항목이 핵심이다. 헬프데스크의 구독 표는 주인을 `(int UserId, string UserType)`
으로 잡고 `Admin`·`Customer` 테이블에 외래키로 묶여 있었고, 발송 API 는
`notify-team/{teamId}` · `notify-company/{companyId}` 처럼 **헬프데스크 도메인 개념**으로
되어 있었다. 포털 계정은 아이디가 문자열이고 장례식장은 또 다른 신원 체계를 쓰므로
그대로는 재사용이 불가능했다.

## 2. 어떻게 나눴나 — 보내는 일만 한다

```
헬프데스크 ──"이 팀의 관리자는 5·7·9번"──┐
포털      ──"quristyle 에게"────────────┼──▶ NotificationServer ──▶ 브라우저 / 메일 큐
장례식장   ──"이 담당자에게"─────────────┘        (VAPID 키는 여기 한 곳)
```

**누구에게 보낼지는 부르는 쪽이 정한다.** 이 서비스는 팀도 회사도 모른다.
헬프데스크는 자기 DB 에서 대상을 고른 뒤 주인 키 목록만 넘긴다.

주인은 문자열 한 쌍이다.

```
ownerType = "jsini"             ownerKey = "quristyle"   (포털 로그인 아이디)
ownerType = "helpdesk-admin"    ownerKey = "5"           (헬프데스크 Admin.Id)
ownerType = "helpdesk-customer" ownerKey = "12"
```

### 일부러 옮기지 않은 것

**알림 목록(읽음·전달 표시)은 헬프데스크에 남겼다.** `/api/push/notifications`,
`my-notifications`, `{id}/read`, `{id}/delivered` 는 화면 기능이고 헬프데스크
테이블을 읽는다. 옮기면 도메인 로직이 따라와야 해서 "전송" 과 "이력" 을 갈랐다.

## 3. API

| | 경로 | 설명 |
|---|---|---|
| GET | `/api/notification/notifications/vapid-public-key` | 화면이 구독을 만들 때 쓰는 공개 키. `enabled` 가 거짓이면 구독 버튼을 숨기면 된다 |
| POST | `/api/notification/notifications/subscriptions` | 구독 등록. 같은 `endpoint` 면 갱신한다 |
| DELETE | `/api/notification/notifications/subscriptions?endpoint=` | 해제 (자기 것만) |
| GET | `/api/notification/notifications/subscriptions/me` | 내 구독 목록 |
| POST | `/api/notification/notifications/push` | 주인 목록에게 발송 |
| POST | `/api/notification/notifications/email` | 메일 발송 요청 |

**익명으로 열지 않았다.** 구독 등록은 "누가" 가 반드시 필요하고, 발송은 더 그렇다 —
익명이면 아무나 우리 사용자에게 알림을 보낼 수 있다.
**남의 이름으로 구독을 만들 수 없다**(403). 다른 주인을 지정하는 서버 대 서버 경로는
아직 필요하지 않아 막아 두었다.

## 4. 이메일은 SMTP 가 아니다

**이 저장소 어디에도 SMTP 설정이 없다.** 헬프데스크가 하던 방식을 그대로 옮겼다.

1. 메일 내용을 JSON 파일로 떨어뜨린다 (`EmailQueue:SpoolPath`)
2. "이 스크립트를 이 파일로 돌려 달라" 를 큐에 넣는다 (`run_script`)
3. 배포 장비의 소비자가 스크립트를 실행해 실제로 보낸다

그래서 **결과는 "큐에 넣었다" 까지만 알 수 있다.** 배포 도구에서 다룬 것과 같은
한계다([28-release-tool.md](28-release-tool.md)). 진짜 발송 결과가 필요해지면
배포 도구처럼 되돌려 보고받는 길을 붙여야 한다.

JSON 의 키 이름(`title`·`body`·`mailto`)은 **바꾸면 안 된다.** 배포 장비의 스크립트가
그 이름으로 읽는다. 큐 넣기가 실패해도 **스풀 파일은 지우지 않는다** — 큐만 복구하면
사람이 다시 밀어 넣을 수 있다.

## 5. VAPID 키 — 옮기기만 했고 교체하지 않았다

`funeralv2Api`·`HelpDeskServer` 에 있던 값을 그대로 가져와
`NotificationServer/appsettings.Local.json`(git 제외)에 넣었다.

**교체하면 기존 구독이 전부 끊긴다.** 브라우저 구독은 만들 때의 공개 키에 묶여 있어서
키를 갈면 모든 사용자가 다시 구독해야 한다. 지시 없이 할 일이 아니다.

공개 키는 비밀이 아니다(브라우저가 구독할 때 쓰는 값이라 화면에 내려간다).
개인 키만 Local.json 에 둔다.

## 6. 확인한 것

테스트 인스턴스를 따로 띄워(알림 5460 · 게이트웨이 5266) 개발 DB 상대로 확인했다.

| 확인 | 결과 |
|---|---|
| 기동 · `/health` | 200 |
| 인증 없이 구독 | 401 |
| 구독 등록 · 같은 endpoint 재등록 | 200 · **중복 행이 생기지 않는다** |
| 남의 이름으로 구독 | 403 |
| 불완전한 구독(키 누락) | 400 |
| 기기 둘 등록 후 목록 | 2건 정확 |
| 해제 (내 것 / 없는 것) | 200 / 404 |
| 푸시 발송 (가짜 endpoint) | 실패를 세어 `failure_count` 에 남긴다. **터지지 않는다** |
| 푸시 (구독 없는 주인) | 202 + `ownersWithoutSubscription=1` + 사람 말 설명 |
| 푸시 (제목 없음) | 400 |
| 이메일 (RabbitMQ 없음) | 400 + 실제 사유. **스풀 파일은 남는다** (키 이름도 규약대로) |
| 게이트웨이 경유 (새 키 토큰) | 200 — `/api/notification/**` 이 5460 으로 정상 전달 |

`dotnet build -c Release` 오류 0.

### 작업 중 걸린 것 둘

**컬럼명 규칙을 빠뜨렸다.** 엔티티에 `[Column]` 을 달아 뒀지만 `BaseEntity` 에서
상속한 `Id`·`CreatedAt` 등에는 없어서, EF 가 PascalCase 로 찾아
`column p.Id does not exist` 로 깨졌다. AuthServer 와 같은 전역 snake_case 변환을
`OnModelCreating` 에 넣어 해결했다.

**`Npgsql 8.0.8` 이 `EF 8.0.8` 이상을 요구한다.** EF 를 8.0.0 으로 적었더니
`NU1605` 로 빌드가 막혔다.

## 7. 남은 것

### 아직 하지 않은 것 — 헬프데스크를 이쪽으로 돌리는 일

**헬프데스크의 푸시·이메일은 지금도 예전 방식으로 돌고 있다.** 일부러 그대로 두었다 —
돌리는 순간 기존 구독(다른 DB `jinrecept` 에 있다)과 알림 목록 기능이 함께 걸린다.
작동하는 것을 먼저 깨뜨리지 않는 편이 맞다고 보았다.

순서 제안:

1. 헬프데스크 구독을 `scom.push_subscriptions` 로 옮긴다
   (`ownerType = helpdesk-admin` / `helpdesk-customer`). D5 처럼 이전 도구가 필요하다.
2. 헬프데스크의 발송 호출(`WebPushService`·`EMailUtil`)을 NotificationServer 호출로 바꾼다.
   대상 선택(팀·회사)은 **헬프데스크에 남긴다** — 자기 DB 를 읽는 일이다.
3. 헬프데스크의 `Vapid` 설정과 `WebPush` 패키지 참조를 걷어낸다.
4. `funeralv2Api` 의 `Vapid` 설정도 걷어낸다(거기서 실제로 푸시를 보내는지 확인 후).

### D2 와의 관계

원래 문서의 판단은 "**D1·D2 이후에**" 였다. D1 은 끝났고 **D2(DB 통합)는 아직**이다.
그래서 이 서비스는 자기 DB 를 만들지 않고 포털 DB(`jsiniportal`/`scom`)에 구독 표
하나만 두었다. D2 가 정해지면 함께 옮기는 편이 낫다.

### 화면은 아직 없다

포털 프론트에 구독 버튼·서비스워커를 붙이는 일은 하지 않았다. 서버가 준비됐으니
화면은 `vapid-public-key` → `subscriptions` 두 API 만 부르면 된다.
서비스워커의 payload 키 이름(`title`·`body`·`url`·`icon`·`tag`)은 헬프데스크가 쓰던
것과 같게 맞춰 두었다 — 이미 배포된 서비스워커가 그 이름을 읽는다.
