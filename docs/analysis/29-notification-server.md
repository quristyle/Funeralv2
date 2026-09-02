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
| GET | `/api/notification/notifications/preferences/me` | 내 알림 설정 + 공개 키 + 기기 목록 (8절) |
| PUT | `/api/notification/notifications/preferences/me` | 내 알림 설정 저장 (준 항목만) |
| POST | `/api/notification/notifications/push/test` | **나에게** 시험 발송 (대상을 서버가 정한다) |

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

### ~~화면은 아직 없다~~ → 2026-09-02 에 붙였다 (8절)

서비스워커의 payload 키 이름(`title`·`body`·`url`·`icon`·`tag`)은 헬프데스크가 쓰던
것과 같게 맞춰 두었다 — 이미 배포된 서비스워커가 그 이름을 읽는다.

---

## 8. 내 알림 설정 화면 — 관리 주체를 포털로 옮겼다 (2026-09-02)

대상: `fronts/apps/jsini-portal/src/views/portal/system/push/setting.vue` ·
`src/api/portal/notification/index.ts` · `docs/sql/notification_preferences.sql` ·
NotificationServer(엔드포인트 셋 · 표 하나 · 발송 판정)

> 지시: "`/system/push/setting` 을 **로그인 사용자의 알림설정** 화면으로 전면 수정.
> 관리 주체도 jsini-portal 이 되도록. PWA 구독·구독해지, 푸시/이메일/날씨 알림 여부.
> 백엔드도 jsini-portal 이 쓰는 곳에서 동작해야 한다."

### 8.1 무엇이 잘못돼 있었나

이 화면은 헬프데스크의 구독 시험 화면(`NotificationSettings.vue`)을 옮겨 온 것이라
**데이터도 헬프데스크(`/api/helpdesk/push/*`)를 보고 있었다.** 그래서 두 가지가 겹쳐
사실상 동작하지 않는 화면이었다.

1. **주인 체계가 맞지 않는다.** 헬프데스크 구독 표의 주인은 `(int Admin.Id, UserType)`
   이고 `Admin`·`Customer` 에 외래키로 묶여 있다(1절). 포털 계정으로 로그인한 사람에게는
   그 숫자가 없다.
2. **`applicationServerKey` 를 넘기지 않고 있었다.** VAPID 공개키 없이
   `pushManager.subscribe({ userVisibleOnly: true })` 를 부르면 크로미움계 브라우저는
   그냥 거절한다. 39번 문서가 "구독 화면이 살아났다" 고 적었지만 구독 자체는 이 이유로
   실패했을 것이다 — 자동화 환경은 알림 권한이 `denied` 라 거기까지 가 보지 못했다.

화면 머리말에 남아 있던 "이 프론트에는 서비스 워커가 없다" 는 안내도 이미 사실이
아니었다(39번 문서, 2026-08-30 PWA).

### 8.2 스위치는 기기가 아니라 **사람의 뜻**이다

`scom.notification_preferences` 를 새로 만들었다(`docs/sql/notification_preferences.sql`).

| | `push_subscriptions` | `notification_preferences` |
|---|---|---|
| 무엇 | **기기** — 브라우저마다 한 행 | **사람** — 주인마다 한 행 |
| 사라질 때 | 브라우저를 지우면 없어진다 | 남는다 |
| 주인 키 | `(owner_type, owner_key)` | 같다 |

주인 키가 같아서 둘을 짝지을 수 있다. 포털이면 `('jsini', 로그인 아이디)` 다 —
게이트웨이가 주는 `X-User-Id` 가 `scom.accounts.user_id` 이기 때문이다
(`accounts.id` 가 아니다. `MenuService.cs` 가 같은 함정을 주석으로 적어 두었다).

**행이 없으면 켜짐으로 본다.** 꺼짐을 기본으로 두면 화면을 한 번도 열지 않은 사람이
알림을 못 받게 되어 이 표가 생기기 전과 동작이 달라진다. 날씨만 꺼짐이 기본이다.

**끄면 구독을 지우지 않는다.** 지워 버리면 다시 켤 때 브라우저 권한부터 다시 받아야
한다. 스위치는 발송만 멈춘다.

### 8.3 D8-A("보내는 일만 한다")를 어긴 것이 아니다

발송 직전에 `PushSender` 가 "이 사람이 껐나" 를 본다. 대상 선택을 이 서비스가
가져온 것처럼 보이지만 그렇지 않다.

- **누구에게 보낼지는 여전히 부르는 쪽이 정한다** — 헬프데스크는 팀을 알고,
  장례식장은 담당자를 안다.
- **받는 사람 본인이 껐다는 것은 대상 선택이 아니라 수신자의 속성이다.** 기기 목록이
  이미 여기 있으므로 판정도 여기서 해야 맞는다. 부르는 쪽마다 기억하게 하면
  **한 곳만 잊어도 새는 설정**이 된다.

발송 결과에 `optedOut`(본인이 꺼서 제외된 대상 수)을 더했다. `ownersWithoutSubscription`
과 같은 목적이다 — "왜 안 왔나" 에 답할 수 있어야 한다.

이메일은 **역할로 보내는 것(`toRole`)에만** 건다. 주소를 직접 적어 보내는 메일
(문의 회신 등)은 업무 메일이고 주소만으로는 어느 계정인지도 확실치 않다 —
알림 설정으로 업무 메일을 막으면 조용히 일이 끊긴다.

### 8.4 시험 발송에 길을 따로 뒀다

`POST /notifications/push/test` 는 **대상을 서버가 정한다**(부른 사람 자신).
`/push` 로도 할 수 있지만 그러려면 화면이 자기 주인 키를 알아야 하고, 남의 키를
적어 보낼 여지가 생긴다.

**푸시를 껐으면 시험도 안 간다.** 켜짐 판정을 건너뛰면 "시험은 오는데 실제 알림은
안 오는" 상태를 진단할 수 없다. 대신 202 와 그 이유가 돌아온다.

### 8.5 날씨 알림은 뜻만 받아 둔 것이다

판정(기상 임계치·특보)은 LifeEnvServer 가 이미 돌리고 있지만 **발송 경로가 없다**
(D-G1, [38-ghub-migration.md](38-ghub-migration.md)). 그 결정에서 열려 있던 질문이
"누가 받나(역할? 구독?)" 였는데, 이 스위치가 그 답이 된다 — 발송을 붙일 때
`weather_enabled = true` 인 주인을 대상으로 삼으면 된다.
화면도 "아직 발송이 켜지지 않았다" 고 그대로 말한다.

### 8.6 확인한 것

개발 게이트웨이(:5265) → NotificationServer(:5460) → 개발 DB 상대로 확인했다.

| 확인 | 결과 |
|---|---|
| 표 생성 (`notification_preferences.sql`) | 컬럼 11 · PK · 주인 유일 인덱스 |
| `GET /preferences/me` (행 없음) | 기본값 + `saved:false` + 공개키 + `pushAvailable:true` |
| `PUT` 한 항목만 | 나머지 값이 유지된다 (날씨만 켠 뒤 푸시를 꺼도 날씨는 켜짐) |
| `PUT {}` | 400 |
| 인증 없이 | 401 |
| 남의 이름으로 구독 | 403 (기존 방어 그대로) |
| 구독 등록 → 기기 목록 | 1건, `source` · `failureCount` 함께 |
| 해제 / 없는 것 해제 | 200 / 404 |
| 시험 발송 (푸시 꺼짐) | **202 + `optedOut:1` + "대상이 모두 푸시 알림을 끄고 있습니다."** |
| 시험 발송 (가짜 endpoint) | 202 + `failed:1`. 터지지 않고 `failure_count` 에 남는다 |
| 화면 (개발 서버) | 상태 조회 200 · 스위치 클릭 → `PUT` 200 → DB 행 반영 · 서비스 워커 **활성** |
| 준수사항 4 (세로 스크롤) | 1280×720 · 1000×800 모두 넘침 0 |

`dotnet build -c Release` 오류 0 · `vite build` 성공 · 손댄 파일 `vue-tsc` 오류 0.

브라우저 알림 권한은 자동화 환경이라 `denied` 다. 화면은 그 상태를 알아보고
"자물쇠 › 알림 › 허용" 안내를 띄운다 — 실기기에서 허용한 뒤 구독하면 된다.

### 8.7 함께 정리한 것

`api/helpdesk/admin.ts` 에서 구독 등록·해제·확인·시험발송 넷을 걷어냈다.
쓰는 곳이 이 화면 하나였고, 남겨 두면 같은 일을 하는 길이 두 벌이 된다.
**알림함(`/push/notifications`, 읽음 표시)은 남겼다** — 헬프데스크 테이블을 읽는
화면 기능이라 옮기면 도메인 로직이 따라온다(2절 "일부러 옮기지 않은 것").

### 8.8 남은 것

- **메뉴 권한은 건드리지 않았다.** `HD_PUSH_SETTING` 은 `ADMINISTRATOR` ·
  `SYSTEM_ADMINISTRATOR` · `PARTNER` · `PARTNER_ADMINISTRATOR` 넷에 열려 있고,
  이는 같은 `SETTING` 묶음의 다른 개인 화면(환경설정 · 프로필)과 **똑같다.**
  개인 설정 화면 전체를 어느 역할까지 열지는 이 화면 하나의 문제가 아니라
  운영 결정이라 손대지 않았다 — 넓히려면 역할 권한 화면에서 함께 넓히는 것이 맞다.
- 헬프데스크 자신의 구독·발송은 여전히 예전 방식이다(7절 순서 제안 그대로).
- 날씨·생일 알림의 **발송**은 D-G1 대기.
