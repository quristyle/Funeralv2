# 시각은 UTC 로 돈다

이 시스템에서 **「순간」은 언제나 UTC** 다. 저장도, 전선에 싣는 것도, 계산도.
사람에게 보여 주는 한국 시각은 **맨 바깥에서 한 번만** 만든다.

규칙은 두 줄이다.

| | 무엇인가 | 어떻게 다루나 |
|---|---|---|
| **순간**(instant) | 「언제 일어났나」 — 요청 시각 · 시작 시각 · 마지막 확인 시각 | **UTC.** DB 는 `timestamptz`, 코드는 `UtcNow`, 견주기도 UTC 끼리 |
| **달력 날짜**(date) | 「며칟날인가」 — 계획시작일 · 목표일 · 운송일 · 기간 고르개 | 시간대가 **없다.** 「오늘」을 물을 때만 **한국 달력**으로 답한다 |

둘을 섞으면 반드시 아홉 시간이 어디선가 어긋난다. 이 문서는 그 경계가 어디에
그어져 있는지를 적는다.

---

## 왜 손댔나 (2026-09-29)

DB 의 시각 칸이 **두 가지로 섞여 있었다.**

* `timestamp with time zone` — 진짜 순간. 안에는 UTC 로 적히고 읽을 때
  세션 시간대로 옮겨진다. 대부분이 이것이었다.
* `timestamp without time zone` — 시간대 없는 **벽시계 숫자**. DB 서버의
  timezone 이 `Asia/Seoul` 이라 `now()` 를 넣으면 **한국 시각의 숫자가 그대로**
  박혔다. `projmng` 45칸 · `helpdesk` 7칸이 이랬다.

그래서 화면마다 「이 칸은 UTC 인가 KST 인가」를 따로 따져야 했고, 같은 표
안에서도 칸마다 답이 달랐다. 실제로 그 자국이 코드에 남아 있었다 —
「`ToLocalTime()` 을 부르지 않는다, 부르면 아홉 시간이 더해진다」는 머리말이
프로젝트관리 화면 셋에 따로따로 적혀 있었고, 그 규칙은 **그 세 화면에서만**
맞았다.

## 무엇을 했나

| 층 | 무엇을 |
|---|---|
| **DB** | 시간대 없는 시각 칸 52개를 `timestamptz` 로. 기존 값은 한국 벽시계 숫자라 `AT TIME ZONE 'Asia/Seoul'` 로 읽어 옳은 순간으로 옮겼다. DB 기본 시간대도 UTC 로 고정 |
| **DB(달력)** | `projmng.home_todo.target_day` 는 시각이 아니라 날짜라 `date` 로. 달력의 「오늘」은 `projmng.today_kst()` 가 답한다 |
| **컨테이너** | `TZ=Asia/Seoul` → **`TZ=Etc/UTC`** (`deploy/docker` 셋) |
| **백엔드** | `DateTime.Now`·`DateTime.Today` 를 전부 걷어냈다. 공용 시계는 `JSini.Shared.Infrastructure.Time.AppTime` |
| **프론트** | 같은 규칙. 공용 시계는 `JSini.Web.Components.Data.AppTime`, 보여 주기는 `Kst(...)` 확장 |
| **막는 장치** | 아키텍처 테스트 `UtcTimeTests` 둘 |

반영 SQL 은 [`deploy/sql/utc-timestamptz-2026-09-29.sql`](../deploy/sql/utc-timestamptz-2026-09-29.sql)
이고 **두 번 돌려도 안전하다**(이미 `timestamptz` 인 칸은 건너뛴다).
운영 DB **일곱 곳 전부**(`projmng` · `helpdesk` · `cargotrust` · `funeralv2` ·
`ghub` · `jsiniportal` · `jsinisite`)에 반영했고, 시간대 없는 시각 칸은 이제
**한 곳에도 없다**.

일부러 놔둔 DB 가 둘 있다. `jinrecept`(옛 헬프데스크 자료)는 이 저장소의 어느
서비스도 읽지 않고 바깥의 옛 시스템이 아직 naive KST 로 읽고 있을 수 있어서,
`goldb` 는 아예 다른 제품이라서다.

---

## 코드에서 쓰는 법

### 백엔드 (`JSini.Shared.Infrastructure.Time.AppTime`)

```csharp
var now   = AppTime.UtcNow;            // 지금. 저장·견주기
var today = AppTime.TodayInKorea;      // 한국 달력의 오늘. date 칸에만
var text  = AppTime.ToKorea(at);       // 보여 주기 직전에만
var from  = AppTime.StartOfDayUtc(d);  // 사람이 고른 날짜 → UTC 기간 경계
```

### 프론트 (`JSini.Web.Components.Data.AppTime`)

```csharp
AppTime.UtcNow - t.StartedAt          // 얼마나 지났나
AppTime.Today                         // 한국 달력의 오늘 (DateOnly)
AppTime.TodayDate                     // 같은 값의 DateTime — 날짜 고르개용
at.Kst("yyyy-MM-dd HH:mm")            // 보여 주기
at.KstTime()                          // 그리드 칸이 받을 DateTime
```

> **`DateTime.Now` 를 쓰지 않는 이유가 Blazor Server 에서 특히 고약하다.**
> 그것은 「보는 사람의 시각」이 아니라 **서버 프로세스의 시각**이다. 운영
> 컨테이너는 UTC 라 맞고 **개발 장비(한국 시각)에서만 아홉 시간 어긋난다** —
> 재현되는 자리와 드러나는 자리가 반대라 가장 늦게 들킨다. 반대로 「오늘」을
> `DateTime.Today` 로 물으면 **운영에서만** 틀린다(한국의 오전 9시 전 아홉
> 시간이 어제다).

### 그리드 칸은 `DisplayFormat` 에서 옮길 자리가 없다

`DxGridDataColumn` 은 값을 그대로 서식에 넣는다. 그래서 시각 칸을 묶을 때는
**한국 시각으로 옮긴 속성을 DTO 에 하나 더 둔다**
(`AiDashboardRecent.StartedAtKst`).

---

## 경계가 한국 시각으로 남아 있는 자리 — 그리고 그 까닭

**전부 「사람의 달력·시계로 물어야 답이 되는」 자리다.** 저장값은 여전히 UTC 다.

| 자리 | 무엇이 한국 시각인가 | 까닭 |
|---|---|---|
| `AiDashboardService.StartedKst` | 일별·월별·시간대·요일 **칸 가르기** | 「몇 시에 몰리나」를 UTC 로 가르면 한국의 오전 9시 전 아홉 시간이 전날 칸으로 밀린다 |
| `HelpDeskServer/Utilities/Kst` | 현황판의 오늘·요일·시간대 | 위와 같다 |
| `CargoTrustServer/Common/Kst` | 운송일·지급일의 「오늘」 | 그 칸이 `date`(달력 날짜)다 |
| `LifeEnvServer/Utilities/Kst` | 기상청 요청의 `base_date`·`base_time` | **바깥 규격**이다. 기상청이 KST 로 발표한다 |
| `projmng.today_kst()` | `plan_sdt`·`plan_edt`·`target_day` 와 견주는 「오늘」 | 계획일이 `date` 라 시간대가 없다. `current_date` 는 세션 시간대(UTC)를 따라가 아홉 시간 동안 어제를 가리킨다 |
| `JSini.PublicSite` 바닥글 | 저작권 연도 | 한 해의 첫 아홉 시간에 지난해가 적힌다 |

**새로 만드는 자리는 이 표에 들어가지 않는 한 UTC 다.** 들어가야 한다면
그 까닭을 여기에 한 줄 적는다 — 적을 까닭이 없으면 대개 UTC 가 맞다.

---

## 확인하는 법

```bash
# 남은 시간대 없는 시각 칸은 0 이어야 한다
psql -h <호스트> -p <포트> -U <계정> -d projmng -t -A -c "
select count(*) from pg_attribute a
  join pg_class c on c.oid=a.attrelid join pg_namespace n on n.oid=c.relnamespace
 where a.atttypid='timestamp'::regtype and a.attnum>0 and not a.attisdropped
   and c.relkind in ('r','p') and n.nspname not in ('pg_catalog','information_schema')"

# 세션 시간대도 UTC 여야 한다
psql … -c "show timezone"
```

코드 쪽은 `web/` 에서 `dotnet test` — `UtcTimeTests` 가 `DateTime.Now` ·
`DateTime.Today` 를 찾아 막는다.

---

## 되돌리려면

칸의 형을 되돌리는 것은 **값을 잃지 않는다**(`timestamptz` → `timestamp` 는
세션 시간대로 펼쳐질 뿐이다). 그래도 **코드와 함께 되돌려야 한다** — DB 만
되돌리면 `AT TIME ZONE 'Asia/Seoul'` 로 한 번 더 옮기게 되어 열여덟 시간이 어긋난다.
컨테이너의 `TZ` 도 한 짝이다.
