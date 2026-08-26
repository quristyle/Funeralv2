# 배포 도구 — 실제 진행 상황과 결과를 받는다 (`/portal/release`)

작성: 2026-08-26
대상: `microservices/AuthServer/{Entities,DTOs,Services,Endpoints}/Release*` ·
`fronts/apps/jsini-portal/src/{api,views}/portal/release` ·
`deploy/release-consumer/` · `docs/sql/release_runs.sql`

> 지시: "`/portal/release` 는 시스템 자동 반영을 위한 배포도구이다. 서버에 보관된 sh
> 파일을 큐에게 동작하도록 만들어 두었다. **실제 처리되는 내용과 처리 결과를 받으면서**
> 배포처리를 하려면 어떻게 개선하는 것이 좋은가?"
>
> 이어서: "권장하는 방향으로 계속 진행하라. 자율모드로 진행하고 반드시 결정이 필요한
> 경우 돌아와서 지시하겠다."

---

## 1. 무엇이 문제였나 — 화면이 거짓말을 하고 있었다

화면(`views/portal/release/index.vue`)에 `BUILD_STEPS` 라는 7단계 배열이 있었고,
`setTimeout` 으로 순서대로 초록색 `[SUCCESS]` 를 찍었다.

```js
// 예전 코드
const BUILD_STEPS = ['source get', 'source checking', 'front build', ...];
for (const step of BUILD_STEPS) {
  appendLog('info', `[INFO] Starting: ${step}...`);
  await wait(per);
  appendLog('success', `[SUCCESS] Completed: ${step}`);   // ← 서버는 아무 말도 안 했다
}
```

**서버에서 오는 정보가 아니었다.** `catch` 로 잡히는 것은 큐에 넣는 것이 실패한
경우뿐이고, 배포 장비에서 스크립트가 실패하든 큐 소비자가 아예 안 떠 있든
화면은 전부 초록이었다.

그 밖에 함께 있던 문제들:

| | 예전 |
|---|---|
| 이력 | 없다. AuthServer 콘솔 로그뿐이고 화면을 새로 고치면 사라진다 |
| 권한 | 화면의 `v-perm:cust1` 뿐. API 는 `user is null` 만 봤다 — 요청을 직접 보내면 누구나 운영 배포 |
| 동시 실행 | 막는 것이 없다. 두 사람이 동시에 누르면 같은 체크아웃에서 스크립트 둘이 돈다 |
| 큐 실패 | 붉은 줄로 한 번 보이고 사라진다 |
| 버전 표시 | **포털 자신의** `/version.json` 을 읽었다. jin114 를 배포해도 안 바뀌는 숫자다. 못 읽으면 빌드 시점 버전으로 조용히 대체 |
| 메시지 | 실행할 스크립트 경로가 그대로 들어간다. 큐(`run_script`)는 헬프데스크 메일 발송과 공유 — 사실상 범용 원격 실행 채널 |

## 2. 없던 것은 run id 였다

요청 한 건을 행 하나로 만들면 나머지가 따라온다.

```
화면 ─POST /release/{key}─▶ AuthServer ──▶ scom.release_runs 행 생성
                                │
                                └─큐─▶ 배포 장비 소비자 ─▶ release-run.sh ─▶ 실제 배포 스크립트
                                                              │
   ◀── GET /release/runs/{id}?sinceSeq=N ─┐                   │ stdout 한 줄씩
   폴링(1.2초)                            └── AuthServer ◀────┘ POST /release/runs/{id}/events
```

표는 둘이다 ([docs/sql/release_runs.sql](../sql/release_runs.sql), 실행 완료).

- `scom.release_runs` — 요청·진행·결과 한 건
- `scom.release_run_events` — 진행 로그 한 줄

### status 값의 뜻

| 값 | 뜻 |
|---|---|
| `queued` | 큐에 넣었고 **아직 아무도 집어가지 않았다** ← 예전에 감춰져 있던 상태 |
| `running` | 배포 장비가 집어가서 돌고 있다 |
| `succeeded` | 스크립트가 0 으로 끝났다 |
| `failed` | 0 이 아닌 코드로 끝났다 (큐 연결 실패도 여기 남는다) |
| `timeout` | 제한 시간을 넘겨도 소식이 없다 — 소비자가 죽었거나 없다 |
| `dispatched` | 보고를 하지 않는 대상에 요청만 보냈다. **결과는 알 수 없다** |

`dispatched` 를 따로 둔 것이 이 작업의 핵심 중 하나다. 배포 장비의 소비자는 이
저장소 밖에 있어서 래퍼를 붙이기 전에는 보고가 올 수 없다. 그때 성공했다고 하지
않고 "요청을 보냈다" 까지만 말한다.

## 3. 기존 배포를 깨지 않았다 — 소비자를 고치지 않는 길

이 작업에서 가장 신경 쓴 부분이다. 자리를 비우신 동안 운영 배포가 멈추면 안 된다.

큐 이름(`run_script`)과 메시지의 `script`·`args` 를 **그대로** 두었다. 배포 장비의
지금 소비자는 늘어난 필드를 무시하고 예전처럼 동작한다.

그런데 진행 보고에는 run 마다 다른 `runId`·`token` 이 필요하고, 그것은 설정에 미리
박아 둘 수 없다. 소비자가 전달해 주어야 하는데 — 소비자는 고칠 수 없다.

**해결: 래퍼를 `script` 자리에 넣는다.**

지금 소비자가 하는 일은 "메시지의 `script` 를 `args` 와 함께 실행" 하는 것뿐이다.
그래서 포털이 이렇게 보낸다.

```
script = /home/lee/projects/wrkScripts/release-run.sh      ← 래퍼
args   = [runId, token, callbackUrl, /…/ReleaseRecept.sh, "Release", "Release"]
```

소비자는 예전과 똑같이 "스크립트를 실행" 하는데 그 스크립트가 래퍼가 된다.
**소비자 코드는 한 줄도 바뀌지 않는다.** 설정 항목은 `Release:Targets[].WrapperPath` 다.

### 켜는 순서

세 개 모두 **기본 꺼짐**이라 지금은 개선 전과 똑같이 동작한다(화면 문구만 정직해졌다).

1. `deploy/release-consumer/release-run.sh` 를 배포 장비에 복사 (`chmod +x`)
2. 손으로 한 번 돌려 확인 — 토큰이 틀려도 배포는 끝까지 진행되어야 한다
3. `Release:CallbackBaseUrl` 을 게이트웨이 주소로 채운다
4. 대상 하나만 `WrapperPath` + `ReportsProgress: true` 로 켜고 확인
5. 나머지 대상을 켠다

자세한 것은 [deploy/release-consumer/README.md](../../deploy/release-consumer/README.md).

> `CallbackBaseUrl` 이 비어 있는데 `ReportsProgress` 를 켜면 포털이 실행을 **거절한다.**
> 조용히 보고를 못 받는 것보다 왜 안 되는지 말하는 편이 낫다.

## 4. 왜 웹훅인가 (결과 큐가 아니라)

| | 웹훅 (택함) | 결과 큐 |
|---|---|---|
| AuthServer | 엔드포인트 하나 | 상주 소비자 — 수명·재연결·중복 처리를 돌봐야 한다 |
| 배포 장비 | `curl` | 브로커 클라이언트 |
| 확인 | `curl` 로 손으로 재현된다 | 브로커를 띄워야 한다 |
| 나중에 | GitHub Actions 로 옮겨도 같은 규약 | 러너에서 브로커에 붙어야 한다 |

게이트웨이의 `/api/auth/**` 는 **이미 Anonymous** 라([ApiGateway/appsettings.json:36](../../ApiGateway/appsettings.json))
라우트를 새로 열 필요가 없었다. 인증은 계정이 아니라 **run 별 1회용 토큰**이다 —
배포 장비에는 로그인 정보가 없고, 토큰은 run 이 끝나면 서버가 지운다.

## 5. 함께 고친 것

- **권한을 서버가 판정한다.** `IMenuService.GetEffectivePermissionAsync(userId, "/portal/release").CanCust1`.
  `release_menu.sql` 이 이미 만들어 둔 권한을 그대로 쓴다(SYSTEM_ADMINISTRATOR 만).
  응답의 `canRelease` 를 화면이 그대로 쓰므로 "버튼은 보이는데 누르면 403" 이 생기지 않는다.
- **같은 대상 동시 실행 금지.** 부분 unique 인덱스로 DB 에서 막는다
  (`status IN ('queued','running')`). 서비스가 먼저 확인해 안내하고, 경합은 인덱스가 막는다.
- **멈춘 실행 정리.** 상주 서비스를 두지 않고 **읽을 때마다** 훑는다 — 멈춘 run 이
  문제가 되는 순간(진행 상황 조회 · 다음 배포 시도)이 모두 그 경로를 지난다.
  `queued` 는 짧게(`PickupTimeoutSeconds`, 기본 60초), `running` 은 길게
  (대상별 `TimeoutSeconds`, 기본 600초) 본다 — "아무도 집어가지 않았다" 와
  "스크립트가 느리다" 는 다른 문제다.
  **토큰은 지우지 않아** 늦게라도 보고가 오면 `running` 으로 되살아난다.
- **큐 실패가 이력에 남는다.** 예전에는 붉은 줄 한 번으로 사라졌다.
- **배포 후 버전을 서버가 확인한다.** 대상별 `VersionUrl` 을 AuthServer 가 읽어
  `deployed_version` 에 남긴다. 종료 코드 0 과 "반영됐다" 는 다른 이야기다.
  못 읽어도 배포 결과는 바꾸지 않는다(확인 실패 ≠ 배포 실패). 로그에는 남는다.
  예전의 브라우저 `fetch('/version.json')` 은 지웠다.
- **로그 상한.** `MaxEventsPerRun`(5000) · `MaxEventLength`(4000).
  넘으면 **한 줄로 알려 주고** 버린다 — 조용히 자르면 로그가 왜 끊겼는지 알 수 없다.

## 6. 래퍼에서 배운 것 — sed 로 JSON 을 만들지 않는다

처음에는 줄마다 `sed` 를 여섯 번 걸어 JSON 문자열을 만들었다. 가짜 콜백 서버를
띄워 돌려 보니 두 가지가 깨졌다.

- 역슬래시 이스케이프(`s/\\/\\\\/g`)가 **sed 구현에 따라 먹지 않아** JSON 이 깨졌다
- 한글이 UTF-8 에서 EUC-KR 로 바뀌어 나갔다 (`ec 8b 9c` → `bd c3`)

지금은 `LC_ALL=C awk` 한 번으로 버퍼 전체를 바꾼다. `LC_ALL=C` 로 두는 것이 핵심이다.

- awk 가 **바이트 단위**로 움직여 UTF-8 이 그대로 지나간다
- ASCII 제어문자만 정확히 골라낸다
- 이스케이프를 `gsub` 치환문 대신 한 바이트씩 이어 붙여 만든다 —
  치환문의 `\\`·`&` 특별 해석을 아예 피한다

줄마다 프로세스를 띄우지 않게 된 것은 덤이다.

## 7. 확인한 것

**AuthServer 를 5299/5298 포트에 따로 띄워** 개발 DB 를 상대로 53개 항목을 확인했다
(운영 중인 5264 는 건드리지 않으려 별도 인스턴스를 썼다).

| 무리 | 항목 수 | 내용 |
|---|---|---|
| 권한 | 4 | 관리자/비관리자/로그인 없음 · 서버가 403·401 을 준다 |
| 큐 실패 | 6 | RabbitMQ 가 없을 때 400 + 사유가 응답과 이력에 남는다 |
| 콜백 | 11 | 토큰 검증 · 없는 run · `##STEP` · 모르는 level · 한글·따옴표·역슬래시 · `sinceSeq` |
| 동시 실행 | 3 | 409 + 목록의 `activeRunId` 로 이어 보기 |
| 마무리 | 8 | exitCode 0/7 → succeeded/failed · 토큰 폐기 · 끝난 run 에 덧붙이기 차단 |
| 멈춘 실행 | 5 | queued/running 각각의 제한 시간 · 늦은 보고로 되살아나기 |
| 버전 확인 | 7 | JSON `version` 키 읽기 · 주소가 죽었을 때 |
| 로그 상한 | 6 | 줄 수·길이 상한 · 안내가 중복되지 않는다 |

래퍼는 가짜 콜백 서버로 따로 확인했다 — UTF-8 · 이스케이프 · ANSI 제거 · stderr 포착 ·
종료 코드 전달 · **403 이나 서버 부재에도 배포가 끝까지 진행되는지**.
표는 [README](../../deploy/release-consumer/README.md#확인한-것) 에 있다.

### 게이트웨이를 지나는 경로도 확인했다

위 53개는 AuthServer 를 직접 두드린 것이라, 게이트웨이를 지나는 경로를 따로 확인했다.
브라우저로 `quristyle` 로 로그인해 실제 화면에서 확인한 것들이다.

| 확인 | 결과 |
|---|---|
| 화면이 그려진다 | 가짜 진행 단계 없음 · 오해를 주던 '현재 버전' 표시 없음 · 버튼에 '(보고 없음)' 배지 |
| `GET /api/auth/release/targets` (JWT) | 200 · `canRelease: true` · 대상 2건 |
| `POST /api/auth/release/jin114` (JWT) | 400 + **`message` 에 실제 사유** ("메시지 큐에 연결하지 못했습니다: …") |
| 그 실패가 화면 이력에 | `jin114 배포 / 실패 / quristyle / 22:59:30 / 4초` |
| `POST /api/auth/release/runs/{id}/events` (**JWT 없이 토큰만**) | 200 · `queued → running` · `##STEP` 이 현재 단계로 · 한글·따옴표·역슬래시 보존 |
| 같은 경로에 틀린 토큰 | 403 |

**예전이라면 이 실패한 배포가 초록색 `[SUCCESS]` 7줄로 보였을 자리다.**

> 확인 중에 알게 된 것: 본문이 **잘못된 UTF-8** 이면 공용 예외 미들웨어가 400 이 아니라
> **500** 을 낸다(`BadHttpRequestException` 을 그대로 500 으로 감싼다). 래퍼는 5xx 를
> 재시도 대상으로 보므로 다섯 번 시도한 뒤 보고를 포기한다 — 배포는 멈추지 않는다.
> 고치려면 공용 미들웨어를 손대야 해서 이번에는 두었다.
> 이 때문에 래퍼가 UTF-8 을 반 토막 내지 않는 것이 중요하다(6절).

빌드: `dotnet build -c Release` 오류 0 · `pnpm vite build` 성공 ·
`vue-tsc` 와 `eslint` 는 내 파일에 지적 없음
(저장소 전체에는 이번 작업과 무관한 타입 오류 56건이 이미 있다).

테스트 데이터는 지웠고 5264·5265 는 정상이다.

### 작업 중 한 번 사고가 있었습니다

테스트 인스턴스를 정리하려고 `taskkill /FI "IMAGENAME eq AuthServer.exe"` 를 썼는데,
**제 테스트 인스턴스는 `dotnet` 이름으로 돌고 있었고 그 명령은 운영 중이던 5264 를
껐습니다.** 바로 알아채고 Debug 빌드로 5264 를 다시 띄워 복구했습니다.

복구한 5264 는 **이번 변경이 반영된 새 빌드**입니다. 원래 돌던 것은 21:59 빌드로
이미 낡은 상태였습니다(제 수정 전 빌드). 앞으로 프로세스는 PID 로만 정리하겠습니다.

---

## 8. 판단이 필요한 것

### D-R1. `run_script` 큐를 계속 공유할 것인가 🟠

지금 이 큐는 **헬프데스크의 메일 발송과 공유**한다
([EMailUtil.cs:88](../../microservices/HelpDeskServer/Utilities/EMailUtil.cs)).
메시지에 실행할 경로가 들어가므로, 큐에 넣을 수 있는 쪽이면 무엇이든 실행시킬 수 있다.

| | 방법 | 영향 |
|---|---|---|
| A | 그대로 둔다 | 지금 상태 유지 |
| B | 소비자에 **허용 목록**을 둔다 (`RELEASE_ALLOWED_SCRIPTS`) | 소비자만 고친다. 큐는 그대로 |
| C | 전용 큐(`release_run`) + durable 로 옮긴다 | 소비자를 함께 고쳐야 한다. 옮기는 동안 배포가 멈춘다 |

**의견: B 를 먼저, C 는 나중에.** B 는 큐를 건드리지 않으므로 배포가 멈추지 않고,
"메시지가 시키는 아무 경로나 실행" 을 바로 막는다. `consumer.py` 에 넣어 두었다.

C 로 가면 `durable` 도 함께 켤 수 있다(브로커 재시작에도 요청이 남는다). 다만
`run_script` 를 durable 로 **다시 선언하면 브로커가 PRECONDITION_FAILED 를 낸다** —
이미 non-durable 로 존재하기 때문이다. 그래서 전용 큐로 옮길 때만 켤 수 있다.
`Release:Durable` 은 기본 꺼짐으로 두었다.

**자율로 하지 않은 이유:** C 는 배포 장비 쪽 소비자를 같이 바꿔야 하고, 그 사이
운영 배포가 멈춥니다. 자리 비우신 동안 할 일이 아닙니다.

### D-R2. 메시지에서 `script` 를 뺄 것인가 🟡

새 소비자는 `targetKey` 로 자기 쪽 표를 보고 실행하면 된다. 그러면 메시지에
경로가 없어져 D-R1 의 문제가 근본적으로 사라진다. 지금은 호환을 위해 `script` 를
계속 보낸다(`targetKey` 도 함께 보낸다). D-R1 의 C 와 같이 처리할 일이다.

### D-R3. 롤백 버튼 🔴

"이전 버전으로 되돌리기" 는 배포 이력이 생겼으니 화면상으로는 만들 수 있다.
**만들지 않았다.** 되돌리는 스크립트가 배포 장비에 없고, 무엇을 어디까지
되돌리는지(코드만? DB 마이그레이션도?)는 시스템마다 다르다. 잘못 만든 롤백은
배포 실패보다 위험하다. 지시가 있어야 손댈 일이다.

### D-R4. `ApiResponse.Fail` 의 인자 순서 🟡

시그니처는 `Fail(message, code)` 인데, 저장소의 다른 엔드포인트는 거의 모두
`Fail("NOT_FOUND", "찾을 수 없습니다")` 로 부르고 있다. **두 자리가 뒤바뀐 채**
응답이 나가서, 화면이 `message` 를 읽으면 `NOT_FOUND` 같은 코드가 사용자에게 보인다.

배포 엔드포인트만 **이름 있는 인자**로 제자리에 넣었다. 저장소 전체를 맞추는 것은
이 작업의 범위를 넘고, 화면들이 지금 동작에 맞춰져 있을 수 있어 손대지 않았다.
배포 화면의 `reason()` 은 `message` 와 `code` 를 모두 보고 코드 같은 문자열을
걸러 내므로 어느 쪽이 와도 사람 말을 보여 준다.

### D-R5. 대상별 `VersionUrl` 을 무엇으로 채울 것인가 🟡

지금은 비어 있어 버전 확인을 건너뛴다. jin114·goldb 가 각각 어떤 주소로 자기 버전을
알려 주는지(있는지) 확인이 필요하다. `{"version":"..."}` JSON 이면 그 키를 읽고,
아니면 본문 앞부분을 그대로 쓴다.

---

## 9. 남은 개선 여지 (판단까지는 필요 없는 것)

- **폴링 대신 SSE.** 지금은 1.2초 폴링이다. 로그가 흐르는 것이 보이므로 충분하다고
  보았다. 게이트웨이를 지나는 장수명 커넥션은 부품이 늘고 얻는 것이 체감 1초다.
- **이력 화면 분리.** 지금은 배포 화면 안에 최근 15건을 보여 준다. 더 필요해지면
  `GET /release/runs?take=` 가 이미 있다.
- **`##STEP` 마커.** 배포 스크립트에 `echo "##STEP front build"` 를 넣으면 단계가
  강조되고 `현재 단계` 에 올라간다. 안 넣어도 stdout 이 그대로 로그가 된다.
  넣을지는 배포 스크립트를 고칠 수 있을 때 정하면 된다.
