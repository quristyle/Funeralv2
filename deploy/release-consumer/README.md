# 배포 장비 쪽 (release-consumer)

JSini 포털의 배포 도구(`/portal/release`)가 큐에 넣은 요청을 배포 장비에서 실행하고
**진행 상황을 되돌려 보고하는** 부분이다.

```
포털 화면 ─POST─▶ AuthServer ─큐─▶ 배포 장비의 소비자 ─▶ release-run.sh ─▶ 실제 배포 스크립트
   ▲                    ▲                                      │
   └──── 폴링 ──────────┴──────── HTTP 콜백 ─────────────────────┘
```

## 왜 필요한가

예전에는 서버가 큐에 넣고 잊었다. 진행 상황을 알 길이 없으니 **화면이 단계를 스스로
만들어 냈다** — `setTimeout` 으로 7단계를 순서대로 초록색 `[SUCCESS]` 로 찍었다.
그래서 배포 장비에서 스크립트가 실패해도, 소비자가 아예 안 떠 있어도 화면은 전부
초록이었다.

`release-run.sh` 는 실제 배포 스크립트를 감싸고 그 stdout/stderr 를 한 줄씩 포털로
보낸다. 끝나면 종료 코드를 보고한다. 이제 화면에 보이는 줄은 전부 실제로 일어난 일이다.

## 무엇을 붙여야 하나 — 소비자를 고치지 않는 길

**지금 도는 소비자를 건드리지 않아도 된다.**

지금 소비자는 큐 메시지의 `script` 를 `args` 와 함께 실행하는 일만 한다. 그래서
포털이 `script` 자리에 래퍼를 넣고 `args` 앞에 run 정보를 끼워 보내면, 소비자는
예전과 똑같이 "스크립트를 실행" 하는데 그 스크립트가 래퍼가 된다.

### 1. 래퍼를 배포 장비에 둔다

```bash
scp deploy/release-consumer/release-run.sh lee@배포장비:/home/lee/projects/wrkScripts/
ssh lee@배포장비 chmod +x /home/lee/projects/wrkScripts/release-run.sh
```

필요한 것은 `sh` · `curl` · `awk` 뿐이다. `jq` 는 쓰지 않는다.

### 2. 손으로 한 번 확인한다

포털을 거치지 않고 래퍼만 먼저 돌려 본다. `runId`·`token` 은 아무 값이나 주면
포털이 403 을 돌려주는데, **그때도 배포는 끝까지 진행되어야 한다.**

```bash
/home/lee/projects/wrkScripts/release-run.sh \
  test-run test-token http://포털주소:5265/api/auth/release/runs/test-run/events \
  /home/lee/projects/wrkScripts/ReleaseRecept.sh Release Release
```

### 3. 포털 설정을 켠다

`microservices/AuthServer/appsettings.Local.json`:

```json
{
  "Release": {
    "CallbackBaseUrl": "http://포털주소:5265/api/auth",
    "Targets": [
      {
        "Key": "jin114",
        "WrapperPath": "/home/lee/projects/wrkScripts/release-run.sh",
        "ReportsProgress": true,
        "TimeoutSeconds": 900,
        "VersionUrl": "https://jin114.co.kr/version.json"
      }
    ]
  }
}
```

`ReportsProgress` 는 **대상별로** 켠다. 하나만 먼저 켜서 확인하고 나머지를 켜면 된다.
켜지 않은 대상은 개선 전과 똑같이 동작하되, 화면이 "요청을 보냈다" 까지만 말한다
(성공했다고 하지 않는다).

> `CallbackBaseUrl` 이 비어 있는데 `ReportsProgress` 를 켜면 포털이 실행을 **거절한다.**
> 조용히 보고를 못 받는 것보다 왜 안 되는지 말하는 편이 낫다.

## 단계 표시 (선택)

배포 스크립트가 아래처럼 찍으면 화면이 그 줄을 단계로 강조하고 `현재 단계` 에 올린다.

```sh
echo "##STEP front build"
```

안 찍어도 된다. 그때는 stdout 이 그대로 로그가 된다 — 그것만으로도 예전의 가짜
단계보다 정확하다.

## 메시지 모양

```json
{
  "script":  "/home/lee/projects/wrkScripts/release-run.sh",
  "args":    ["<runId>", "<token>", "<callbackUrl>", "/…/ReleaseRecept.sh", "Release", "Release"],
  "targetKey": "jin114",
  "runId":   "…",
  "wrapped": true,
  "callbackUrl": "http://…/api/auth/release/runs/…/events",
  "token":   "…",
  "targetScript": "/…/ReleaseRecept.sh"
}
```

`script` · `args` 만 봐도 동작한다 — **예전 메시지와 호환된다.** 나머지는 새 소비자가
쓸 수 있게 함께 보내는 것이다. `WrapperPath` 를 비워 두면 `script` 는 실제 배포
스크립트 그대로 가고, 보고는 소비자가 직접 `runId`·`callbackUrl`·`token` 을 읽어
처리해야 한다.

## 콜백 규약

```
POST {callbackUrl}
X-Release-Token: {token}
Content-Type: application/json; charset=utf-8

{ "events": [ {"level":"stdout","message":"…"}, … ], "final": false }
{ "events": [ … ], "final": true, "exitCode": 0 }
```

`level` 은 `info` · `stdout` · `step` · `warn` · `error` · `result` 중 하나다.
모르는 값은 서버가 `stdout` 으로 본다.

응답 코드로 래퍼가 할 일이 갈린다.

| 코드 | 뜻 | 래퍼가 하는 일 |
|---|---|---|
| 2xx | 받았다 | 계속 보낸다 |
| 403 | 토큰이 틀렸거나 이미 끝난 run | **보고만** 멈춘다. 배포는 계속한다 |
| 404 | 그런 run 이 없다 | 같음 |
| 409 | 순번이 충돌했다 | 잠시 뒤 다시 보낸다 |

인증은 계정이 아니라 **run 별 1회용 토큰**이다. 배포 장비에는 로그인 정보가 없고,
토큰은 그 run 이 끝나면 서버가 지운다. 게이트웨이의 `/api/auth/**` 는 이미
Anonymous 라 라우트를 새로 열 필요가 없다.

## 반드시 지키는 것

**보고에 실패해도 배포는 계속한다.** 포털이 내려가 있다고 배포가 멈추면 도구가
원래 하려던 일보다 더 큰 문제를 만든다. 래퍼의 모든 `curl` 은 실패를 삼킨다.

연달아 다섯 번(기본값) 실패하면 보고를 아예 포기한다. 포기하지 않으면 포털이
내려가 있는 동안 보고마다 타임아웃을 기다려 **배포가 느려진다.** 포기하면 포털 쪽
run 은 제한 시간이 지나 '중단' 으로 남는다 — 연락이 끊긴 것이 사실이므로 그렇게
보이는 것이 맞다.

## 확인한 것

Git Bash(Windows)에서 가짜 콜백 서버를 띄우고 돌려 확인했다.

| 확인한 것 | 결과 |
|---|---|
| UTF-8 한글 로그 | 그대로 전달된다 |
| 역슬래시 · 따옴표 · 탭 | JSON 으로 올바르게 이스케이프된다 |
| ANSI 색상코드 | 지워진다 |
| 제어문자(백스페이스 등) | 버려진다 |
| stderr | 함께 잡힌다 |
| 종료 코드 0 / 7 | 그대로 보고되고 래퍼도 같은 코드로 끝난다 |
| 서버가 403 | 한 번만 보내고 멈춘다. 배포는 rc=3 으로 정상 완료 |
| 서버가 아예 없음 | 배포는 rc=5 로 정상 완료 |
| 배포 스크립트가 없음 | rc=127 |

> 이스케이프를 처음에는 줄마다 `sed` 여섯 번으로 했는데, **sed 구현에 따라 결과가
> 달랐다.** 역슬래시 이스케이프가 먹지 않아 JSON 이 깨지고 한글이 다른 인코딩으로
> 바뀌었다. 지금은 `LC_ALL=C awk` 한 번으로 바이트 단위로 처리한다 —
> 줄마다 프로세스를 띄우지 않게 된 것은 덤이다.

## consumer.py 는 무엇인가

참고 구현이다. 지금 도는 소비자를 이것으로 바꿀 필요는 없다(위의 `WrapperPath` 방식이
소비자를 안 고치는 길이다). 다만 포털과 배포 장비 사이의 약속이 저장소 안에
적혀 있어야 하므로 함께 둔다.

바꿀 생각이라면 `RELEASE_ALLOWED_SCRIPTS` 를 채우는 것을 권한다. 이 큐(`run_script`)는
헬프데스크의 메일 발송과 공유하고 있어서, 큐에 메시지를 넣을 수 있는 쪽이면
**무엇이든 실행시킬 수 있는 상태**다. 허용 목록이 그것을 막는다.

```bash
pip install pika
RELEASE_QUEUE_HOST=localhost \
RELEASE_ALLOWED_SCRIPTS=/home/lee/projects/wrkScripts/release-run.sh \
  python3 consumer.py
```
