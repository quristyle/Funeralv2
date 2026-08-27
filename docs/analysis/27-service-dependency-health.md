# 서비스가 '제 일을 할 수 있는지' 를 상태 화면에 드러내기

> 지시: "AiAgentServer 는 내부적으로 LLM 서버와 통신한다. MSA 는 동작 중이지만 LLM 장비는
> 꺼진 경우가 많다. `/system/server-status` 에서 AiAgentServer 가 단순히 동작하고 있기에
> 실제로 동작한다고 오해하기 쉽다. 어떻게 하면 LLM 서버까지 연결 처리 되는지 확인할 수
> 있겠는가? 그리고 화면에서 어떻게 표현하면 좋겠는가?"

작업일: 2026-08-26

---

## 1. 오해가 생긴 이유 (재현함)

게이트웨이는 각 서비스의 `/health` 를 찔러 **HTTP 상태 코드만** 보고 판정했다.

```csharp
status = response.IsSuccessStatusCode ? "UP" : "DEGRADED";
```

AIAgentServer 의 `/health` 는 `AddHealthChecks()` 만 붙은 기본형이라 "이 프로세스가
응답한다" 외에 아무것도 확인하지 않았다. 손대기 전 실제로 재어 보니:

| | |
|---|---|
| LLM (`jin114.co.kr:11434`) | **6초 타임아웃 · 응답 없음** |
| AIAgentServer `/health` | **`Healthy` 200** → 화면에 '정상'(초록) |

**프로세스가 살아 있는 것과 서비스가 제 일을 하는 것은 다르다.** 그 차이가 화면까지
올라오지 않은 것이 원인이었다.

---

## 2. LLM 연결을 어떻게 확인하나

세 단계로 나눌 수 있고, **①+② 를 자동으로, ③ 을 버튼으로** 두었다(지시 확인).

| 단계 | 방법 | 잡아내는 것 | 비용 |
|---|---|---|---|
| ① 접속 | LLM 주소에 HTTP | 장비 꺼짐 · 네트워크 단절 | 매우 낮음 |
| ② 모델 목록 | `GET /v1/models` | 서비스가 실제 서빙 중 + **설정한 모델이 올라와 있는지** | 낮음 |
| ③ 최소 추론 | `chat/completions` 1토큰 | 생성까지 되는지 (완전 확인) | GPU · 수십 초 가능 |

**②가 특히 중요하다.** 장비는 켜져 있는데 `google/gemma-4-26b-a4b` 가 로딩돼 있지 않으면
지금까지는 **실제 대화 요청에서만 터지는 조용한 실패**였다. 이제 미리 잡힌다.

구현 주의: 설정값 `LLM:ApiBase` 는 `.../v1/chat/completions` 까지 경로가 들어 있어
그대로 쓰면 모델 목록을 못 받는다. `/v1` 까지 남기고 `/models` 를 붙인다
(`LlmHealthCheck.BuildModelsUrl`).

### 어디서 확인하나 — 게이트웨이가 아니라 서비스 자신이

게이트웨이가 LLM 을 직접 찌르게 하면 게이트웨이가 LLM 주소·모델명·API 키를 알아야 하고,
그 설정이 두 곳으로 갈라진다. 그래서 **서비스가 스스로 점검해 `/health` 본문에 담고,
게이트웨이는 읽어 올리기만** 한다.

ASP.NET Core 의 헬스체크를 그대로 쓴다. 이 방식이라 다른 의존 대상도 같은 틀로 붙는다.

---

## 3. 만든 것

```
Common/JSini.Shared.Infrastructure/HealthChecks/
  HealthCheckJson.cs          /health 를 JSON 으로 (status + 항목별 상세), MapJsiniHealthChecks()
  DependencyHealthCheck.cs    타임아웃·캐시·예외 처리를 한 번만 쓰는 범용 점검 + AddDependencyCheck()

AIAgentServer/Services/LlmHealthCheck.cs   접속 + 모델 목록 (30초 캐시 · 3초 타임아웃)
AIAgentServer/Endpoints/AIEndpoints.cs     POST /health/deep  ← '정밀 확인'

ApiGateway/HealthBody.cs      /health 본문을 읽어 dependencies 로 올린다
ApiGateway/Program.cs         상태 코드만 보던 것을 본문까지 읽도록
```

DB 점검은 6개 서비스에 붙였다 — AuthServer · FileServer · HelpDeskServer ·
NotificationServer · SiteServer · funeralv2Api.

### 상태 세 가지의 뜻을 정했다

| | 뜻 | HTTP |
|---|---|---|
| `Healthy` | 서비스와 딸린 것 모두 정상 | 200 |
| `Degraded` | **프로세스는 살아 있지만 제 일을 못 한다** (LLM 끊김) | **200** |
| `Unhealthy` | 서비스 자체가 처리 불가 (DB 끊김) | 503 |

**LLM 이 끊긴 것을 503 으로 만들지 않았다.** 그러면 로드밸런서가 이 서비스를 내려
화면에서 아예 사라진다. 서비스는 살아 있으므로 200 을 유지하고 본문에 이유를 담는다 —
그래서 게이트웨이가 **본문을 읽어야** 했다.

반대로 **DB 가 끊기면 `Unhealthy`(503)** 다. 그 서비스는 사실상 아무것도 못 한다.

---

## 4. 화면에 어떻게 표현했나

핵심은 **카드 전체가 딸린 것까지 반영한 색으로 칠해지는 것**이다. 자식 줄만 추가하고
배지를 초록으로 두면 오해가 그대로 남는다.

```
│ AIAgentServer                          [응답 이상]      ← 강조선·배지 모두 노랑
│ AI 에이전트 · 번역 · 추천
│ 응답 시간 2640ms    HTTP 200
│ http://localhost:5450                      ai-cluster
│ ── 연결 대상 ─────────────────────────────────
│  ● LLM 서버  연결 안 됨                      3002ms
│    LLM 장비가 3초 안에 응답하지 않습니다. 장비가 꺼져 있을 수 있습니다.
│    http://jin114.co.kr:11434  google/gemma-4-26b-a4b
│    [ 정밀 확인 (실제 응답 생성) ]
```

- **`DEGRADED`(응답 이상) 를 그대로 썼다.** 게이트웨이와 화면 양쪽에 이미 있던 상태라
  새로 만들지 않았고, 뜻도 정확하다.
- 상단 요약의 '응답 이상' 수에 잡히므로 카드를 보지 않아도 눈에 띈다.
- **왜 그런지 한 줄**을 함께 보여 준다. 상태만 보여 주면 결국 서버에 들어가 봐야 한다.
- 모델 불일치는 문구가 다르다 — "장비는 응답하지만 설정한 모델 '…' 이 목록에 없습니다".
  원인이 완전히 달라서 같은 메시지로 뭉개면 안 된다.
- '정밀 확인' 은 누를 때만 GPU 를 쓴다. 끝나면 자동 점검 캐시를 버려 화면이 바로 새 값을 받는다.

---

## 5. 확인한 것

전부 격리 인스턴스로 확인했다. 개발자가 띄워 둔 프로세스는 건드리지 않았다.

**LLM 점검 네 가지 상태** (스텁 LLM 서버를 띄워 만들었다)

| 상황 | 결과 |
|---|---|
| 모델 있음 | `Healthy` · "모델 'google/gemma-4-26b-a4b' 사용 가능" · 31ms |
| **모델 없음** | `Degraded` · "설정한 모델 '…' 이 목록에 없습니다" · 있는 모델 목록 함께 |
| 연결 거부 | `Degraded` · "연결할 수 없습니다 (대상 컴퓨터에서 연결을 거부…)" |
| 장비 꺼짐 (실제) | `Degraded` · "3초 안에 응답하지 않습니다" · 3013ms |

모두 HTTP 200 을 유지했고, 캐시(30초)와 3초 타임아웃도 동작했다.

**게이트웨이** — 격리 게이트웨이(:15265)를 격리 AI 로 향하게 해 확인

```
ai-cluster           응답 이상   2640ms  http=200
      이유: LLM 장비가 3초 안에 응답하지 않습니다. 장비가 꺼져 있을 수 있습니다.
      └ [llm] Degraded  ... {"endpoint":"http://jin114.co.kr:11434","model":"google/gemma-4-26b-a4b"}
auth-cluster         정상          3ms  http=200
(나머지 서비스는 정상 · dependencies 없음 — 옛 /health 와 호환됨)
```

**화면** — 실제 응답과 같은 값으로 렌더링 확인

```
AIAgentServer | 응답 이상 | 2640ms | 연결 대상 | LLM 서버 | 연결 안 됨 | 3002ms
| LLM 장비가 3초 안에 응답하지 않습니다… | http://jin114.co.kr:11434 | google/gemma-4-26b-a4b
| 정밀 확인 (실제 응답 생성)
```
강조선 `bg-amber-500` — 카드 전체가 노랑으로 칠해진다.

**DB 점검** — AuthServer `/health` → `[database] Healthy 834ms · "DB 에 연결됩니다."`

8개 프로젝트 전부 컴파일 0 오류 · `pnpm vite build` 통과.

---

## 6. 남은 것 🟡

- **게이트웨이와 각 서비스를 다시 띄워야 반영된다.** 실행 중인 프로세스는 이 변경 전
  빌드다. 그전까지 AiAgentServer 는 계속 '정상' 으로 보인다.
- **배포 큐(RabbitMQ) 점검은 넣지 않았다.** `ReleaseService` 가 호출 시점에
  `ConnectionFactory` 를 직접 만들어 쓰는데, 점검을 위해 연결을 열고 닫는 것은
  비용이 있고 브로커 설정(durable 등)에 잘못 손대면 배포가 멈출 수 있다
  ([28-release-tool.md](28-release-tool.md) 의 주의 참고). 큐 연결을 서비스로 뽑은 뒤에
  붙이는 편이 안전하다.
- ~~FileServer 저장소 점검~~ → 아래 7절에서 처리했다.
- 화면의 '연결 대상' 은 서비스가 보고한 것만 보여 준다. 즉 **아직 점검을 붙이지 않은
  의존 대상은 화면에도 없다.** 없는 것이 '정상' 으로 보이지 않도록, 붙일 때마다 이 문서에
  추가한다.
- LLM 점검은 `Degraded` 를 쓰므로 HTTP 200 이다. 오케스트레이터가 `/health` 를
  liveness 로 쓰고 있다면 의도대로(내려가지 않음) 동작하지만, readiness 로 쓰고 싶다면
  경로를 따로 두어야 한다(`/health/ready` 등). 지금은 그럴 필요가 없어 두지 않았다.

---

## 7. FileServer 저장소 점검 (2026-08-26)

> 지시: "FileServer 저장 경로는 appsettings.Local.json 에 있다"

`Storage:LocalPath` = `/funeralv2_storage` (Local 설정, git 제외).
코드는 `FileService` · `FileEndpoints` 가 같은 키를 읽고, 없으면
`<실행 폴더>/Uploads` 로 떨어진다.

### 7.1 존재 확인만으로는 부족하다

디렉터리가 있어도 권한이 없거나 읽기 전용으로 마운트되면 쓰기가 안 된다.
그래서 **아주 작은 파일을 실제로 만들고 지운다**. 이 서비스의 일이 "파일을 보관하는 것"
이므로 그것이 유일한 정직한 확인이다.

**경로는 `FileService` 와 똑같은 규칙으로 찾는다**(`StorageHealthCheck.ResolvePath`).
다르게 찾으면 점검은 통과하는데 실제 업로드는 실패하는, 최악의 어긋남이 생긴다.
규칙을 바꿀 때는 두 곳을 함께 바꿔야 한다.

### 7.2 상태를 세 갈래로 나눴다

원인에 따라 할 일이 다르기 때문이다.

| 상황 | 상태 | 왜 |
|---|---|---|
| 경로 없음 · 접근 불가 | `Unhealthy` (503) | 내주는 것도 보관하는 것도 안 된다 |
| 있지만 쓸 수 없음 | `Degraded` (200) | **이미 있는 파일 내보내기는 된다.** 업로드만 막힌다 |
| 여유 공간 1GB 미만 | `Degraded` (200) | 지금은 되지만 곧 멈춘다 — 미리 알아야 한다 |

여유 공간을 함께 내려보내므로 화면에 `여유 38.5GB` 처럼 나온다. 원본 사진·영상이
오가는 저장소라 남은 용량은 그 자체로 봐야 하는 값이다.

### 7.3 확인한 것

격리 인스턴스로 확인했다. **포트 주의** — FileServer 는 `appsettings.json` 의
`Kestrel:Endpoints:Http:Url` 로 포트를 고정하므로 `ASPNETCORE_URLS` 로는 바뀌지 않는다
(처음에 그것 때문에 5350 충돌로 기동이 실패했다).

| 상황 | 결과 |
|---|---|
| 정상 (실제 경로) | `200` · `[storage] Healthy` · "저장소에 읽고 쓸 수 있습니다. (여유 38.5GB)" · data `{path, freeGb: 38.5, totalGb: 464.9}` |
| 저장 경로 없음 | **`503`** · `[storage] Unhealthy` · "저장 경로가 없습니다: /nope_no_such_storage_dir" |
| DB 점검 동시 | `[database] Healthy` · 155ms |

**'쓰기 불가' 갈래는 이 장비에서 재현하지 못했다.** `icacls` 로 쓰기를 거부해도
셸이 상위 권한으로 돌아 그대로 쓰였다. 코드는 `File.WriteAllBytes` 의 예외를 잡아
`Degraded` 로 돌리는 단순한 구조이고 다른 갈래는 실제로 확인했지만,
**이 한 갈래만은 실행으로 확인하지 못했다**는 점을 남겨 둔다.

확인용으로 만든 임시 인스턴스와 `C:\funeralv2_ro_probe` 디렉터리는 모두 지웠다.
