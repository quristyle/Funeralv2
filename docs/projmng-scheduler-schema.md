# 프로젝트 일정표(`/projmng/proj/scheduler`)의 DB 표와 스키마

운영 DB 를 직접 조회해 적었다(2026-10-31, `jin114.co.kr:31015`, DB `projmng`, 스키마 `projmng`).

## 길

```
ProjectScheduler.razor (web/src/Apps/JSini.Web.ProjMng/Components/Pages)
  └ WbsClient            GET/POST/PUT/DELETE  /api/projmng/wbs[/{wbsId}]   (게이트웨이 :5265)
      └ ProjMngServer (:5450)  /api/wbs  WbsController → WbsService (Dapper, 프로시저 없음)
          └ projmng.dev_wbs
```

고르개 값은 따로 읽는다 —

| 고르개 | 부르는 이름 | 읽는 표 |
|---|---|---|
| 프로젝트 | `CommonCodes.GetAsync("projlist")` | `projmng.dev_proj` (내가 참여한 것만 — `dev_proj_user_map`) |
| 구분 | `CommonCodes.GetAsync("schedule_type")` | `projmng.devcomm` `cm_pcd = 'SCHEDULE_TYPE'` |
| 추가대상 | `CommonCodes.GetAsync("ADD_TARGET")` | `projmng.devcomm` `cm_pcd = 'ADD_TARGET'` |
| 사람(담당·요청자) | `BizOptions.GetAsync("portal_account")` | 포털 계정(AuthServer, `jsiniportal` DB) — 이 DB 밖이다 |

## `projmng.dev_wbs` — 일정의 본 표

**WBS 화면과 같은 표다.** `schedule_type` 하나로 갈린다. 표 설명은 `project wbs`.
운영 615 줄(`WBS` 318 · `Public` 295 · `Private` 2 · `Event` 0).

| 칸 | 형 | NULL | 기본값 | 일정표에서의 뜻 |
|---|---|---|---|---|
| `prj_rid` | integer | NOT NULL | | 프로젝트(`dev_proj.prj_rid`) |
| `wbs_id` | integer | NOT NULL | | 일정 번호. **표 전체에서 하나**다 — 새 줄은 `MAX(wbs_id)+1` (시퀀스 없음) |
| `proc_id` | varchar(1000) | | | 공정 번호. 목록 정렬 첫 열쇠 |
| `gb1` | varchar(1000) | | | 구분 1 |
| `gb2` | varchar(1000) | | | 구분 2 |
| `proc_nm` | varchar(1000) | | | **일정 제목** |
| `proc_tp` | varchar(1000) | | | 공정 유형 |
| `proc_lvl` | varchar(1000) | | | 단계 |
| `build_user` | varchar(1000) | | | 구축 담당 |
| `build_status` | varchar(1000) | | | |
| `dev_user` | varchar(1000) | NOT NULL | | **담당자**. 안 주면 서버가 `''` 로 메운다 |
| `plan_sdt` | timestamptz | | | **계획 시작** — 달력에 놓이는 자리 |
| `plan_edt` | timestamptz | | | **계획 종료** |
| `dev_sdt` | timestamptz | | | 실제 시작 — 있으면 진행중 |
| `dev_edt` | timestamptz | | | 실제 종료 — 있으면 완료 |
| `dev_chk` | varchar(1000) | | | |
| `build_chk` | varchar(1000) | | | |
| `build_chk_dt` | date | | | |
| `qc_user` | varchar(1000) | | | 검수자 |
| `qc_chk` | varchar(1000) | | | |
| `qc_chk_dt` | date | | | |
| `cre_user` | varchar(1000) | | | 등록자(서버가 로그인 신원으로 채움) |
| `cre_dt` | date | | | 등록일 |
| `mod_user` | varchar(1000) | | | 수정자 |
| `mod_dt` | date | | | 수정일 |
| `comm` | text | | | 일정 메모(LLM 요약 포함) |
| `schedule_type` | varchar(100) | NOT NULL | `'WBS'` | 구분 — `WBS` · `Private` · `Public` · `Event` |
| `column1` | varchar(50) | | | 쓰는 코드 없음 |
| `summary` | text | | | 일정 요약(일정 메모 화면에서 AI 가 채우고 사람이 고침). 수정 때 null 이면 그대로 둔다 |
| `body_text` | text | | | 일정 메모 원문. 수정 때 null 이면 그대로 둔다 |
| `req_user` | varchar(1000) | | | 요청자 성명 |
| `add_target` | varchar(20) | | | 추가대상 — `ADD` 추가 · `CHANGE` 변경 · `MOVE` 이동 |

제약: `dev_wbs_unique` UNIQUE(`wbs_id`) · `dev_wbs_unique_1` UNIQUE(`prj_rid`, `wbs_id`).
기본 키와 외래 키는 없다.

### 표에 없고 서버가 세는 값

| 응답 칸 | 셈 |
|---|---|
| `PlanGap` | `COALESCE(plan_edt, dev_edt)::date - COALESCE(plan_sdt, dev_edt)::date + 1` (일) |
| `WbsState` | `dev_sdt`·`dev_edt` 둘 다 없으면 `READY`, `dev_edt` 가 없으면 `RUNNING`, 아니면 `COMP` |

진행 상태는 **따로 적는 칸이 없다** — 화면의 「진행중으로 변경」은 `dev_sdt` 를, 「완료」는 `dev_edt` 를 채우는 것이다.

### 조회 조건

- 기간은 **겹침**으로 본다: `COALESCE(plan_edt, plan_sdt, dev_edt) >= from` 그리고 `COALESCE(plan_sdt, plan_edt, dev_edt) <= to`. 날짜가 하나도 없는 줄은 구간 조회에서 빠진다.
- 구분은 `schedule_type = ANY(@scheduleTypes)`, 안 고르면 전체.
- 프로젝트를 안 고르면 `dev_proj_user_map` 에서 내가 참여한 프로젝트 전부.
- 정렬: `proc_id, gb1, gb2, proc_tp, proc_nm, wbs_id`.

### 저장 때 서버가 메우는 것

- `plan_sdt`/`plan_edt` 를 안 주면 개발 날짜로, 그것도 없으면 `now()` — 계획이 빈 줄은 어떤 집계에도 안 잡힌다.
- `schedule_type` 을 안 주면 `'WBS'`.
- `add_target` 은 빈 글자면 NULL(지움), null 이면 그대로.

## 곁 표

### `projmng.dev_proj` — 프로젝트 (운영 8 줄)

| 칸 | 형 | 기본값 |
|---|---|---|
| `prj_rid` | integer | |
| `prj_name` | varchar(2000) | |
| `prj_desc` | varchar(2000) | |
| `prj_sdt` / `prj_edt` | date | |
| `prj_nick` | varchar(2000) | |
| `prj_type` | varchar(2000) | |
| `proj_pay` / `prj_use_pay` | integer | |
| `mod_dt` / `cre_dt` | timestamptz | |
| `prj_srt` | integer | `99999` (고르개 차례) |

키·제약이 하나도 없다.

### `projmng.dev_proj_user_map` — 프로젝트 참여자 (운영 9 줄)

| 칸 | 형 | NULL |
|---|---|---|
| `prj_rid` | integer | NOT NULL |
| `user_id` | varchar(30) | NOT NULL |

PK `dev_proj_user_map_pk` (`prj_rid`, `user_id`).

### `projmng.devcomm` — 공통코드

`cm_rid` integer(UNIQUE) · `cm_cd` · `cm_nm` · `cm_prop` · `cm_pcd`(묶음) · `cm_val` · `cm_type` · `cm_val2` · `cm_val3` · `cm_srt` integer · `cm_rmk` varchar(2000). 일정표가 읽는 묶음 —

| `cm_pcd` | `cm_cd` | `cm_nm` | `cm_srt` |
|---|---|---|---|
| `SCHEDULE_TYPE` | `WBS` | wbs | 1 |
| `SCHEDULE_TYPE` | `Private` | 개인일정 | 2 |
| `SCHEDULE_TYPE` | `Public` | 공유일정 | 3 |
| `SCHEDULE_TYPE` | `Event` | 이벤트 | 4 |
| `ADD_TARGET` | `ADD` | 추가 | 1 |
| `ADD_TARGET` | `CHANGE` | 변경 | 2 |
| `ADD_TARGET` | `MOVE` | 이동 | 3 |

## 직접 보기

```bash
PGPASSWORD=… psql -h jin114.co.kr -p 31015 -U projmng -d projmng -P pager=off \
  -c '\d+ projmng.dev_wbs'
```
