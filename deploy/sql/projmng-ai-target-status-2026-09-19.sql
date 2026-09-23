-- ============================================================
-- AI 작업 대상의 git 상태 — 표 하나 (projmng 스키마)
-- ============================================================
--
-- 설계: docs/ai-task-runner.md 11.7
--
-- **EF 마이그레이션이 아니다.** ProjMngServer 는 Dapper + Npgsql 이라
-- 스키마를 SQL 파일이 만든다(그 문서 4.6). 운영 반영은 손으로 한 번 돌린다.
--
--   psql -h … -U projmng -d projmng -f projmng-ai-target-status-2026-09-19.sql
--
-- 두 번 돌려도 안전하다.
--
-- ── 왜 표로 두나 — 물어보면 될 것 아닌가 ────────────────────
--
-- **서버는 그 경로를 볼 수 없다.** ProjMngServer 는 컨테이너 안에서 돌고
-- 대상 경로는 호스트의 것이라, 여기서 `git status` 를 부를 방법이 없다
-- (같은 이유로 `AiTargetService.ValidatePath` 도 realpath 를 안 쓴다).
--
-- 그 경로를 실제로 볼 수 있는 것은 **호스트에 상주하는 실행기**뿐이다.
-- 그래서 실행기가 주기적으로 들여다보고 여기에 적어 두고, 화면은 이 표를
-- 읽는다. 화면이 보는 것은 언제나 **「언제 기준」이 붙은 스냅샷**이고,
-- 그 시각을 화면이 같이 보여 준다 — 안 보여 주면 옛 값을 지금 값으로 읽는다.
--
-- 대상 하나에 줄 하나다. 이력을 쌓지 않는다 — 「지금 어떤가」에만 답하는
-- 자리고, 이력이 필요하면 그것은 git 자신이 갖고 있다.

CREATE TABLE IF NOT EXISTS projmng.ai_target_status (
    target_key      bigint        PRIMARY KEY
                                  REFERENCES projmng.ai_target (target_key),

    -- 누가 보고 왔나. **대상마다 장비가 다를 수 있다**(ai_target.runner_nm) —
    -- 어느 장비에서 본 것인지 모르면 경로가 없다는 말의 뜻이 갈린다.
    runner_nm       varchar(100),

    -- 그 장비에 그 경로가 있나. 거짓이면 아래 값들은 전부 비어 있다.
    path_exists     boolean       NOT NULL DEFAULT false,

    -- .git 이 있나. **선언된 종류(target_kind)와 다를 수 있고, 그 어긋남을
    -- 보자는 것이 이 화면의 절반이다** — repo 로 등록해 놓고 .git 이 없으면
    -- 작업은 「저장소로 등록된 대상인데 .git 이 없습니다」로 죽는다.
    -- worktree 의 .git 은 폴더가 아니라 파일이라 둘 다 본다.
    is_repo         boolean       NOT NULL DEFAULT false,

    branch          varchar(200),
    upstream        varchar(200),
    -- 앞선/뒤처진 커밋 수. **fetch 하지 않고 센다** — 원격 추적 참조만 본다.
    -- 여기서 네트워크를 타면 대상 수만큼의 fetch 가 2분마다 돈다.
    ahead           int           NOT NULL DEFAULT 0,
    behind          int           NOT NULL DEFAULT 0,

    staged          int           NOT NULL DEFAULT 0,
    unstaged        int           NOT NULL DEFAULT 0,
    untracked       int           NOT NULL DEFAULT 0,
    conflicted      int           NOT NULL DEFAULT 0,
    -- 실행기가 정본에 남은 변경을 옮겨 두는 자리다(`Workspace.ParkAsync`).
    -- 쌓여 있으면 누가 치워야 한다는 뜻이라 세어 둔다.
    stash_count     int           NOT NULL DEFAULT 0,

    head_sha        varchar(64),
    head_subject    varchar(500),
    head_author     varchar(200),
    head_dt         timestamp,

    remote_url      varchar(500),

    -- `git status --porcelain` 앞 몇 줄. 숫자만으로는 「무엇이 더러운지」를
    -- 알 수 없어서 둔다. 길이를 자르는 것은 실행기 쪽이다.
    dirty_files     text,

    -- 들여다보다 실패한 이유. **비어 있어야 정상이다.**
    probe_error     text,

    probed_at       timestamp,

    -- 화면의 「지금 확인」이 찍는다. 실행기가 다음 바퀴에서 이것을 보고
    -- 주기를 기다리지 않고 바로 본 뒤, 보고와 함께 지운다.
    probe_req_dt    timestamp,

    cre_dt          timestamp     DEFAULT now(),
    mod_dt          timestamp
);

COMMENT ON TABLE projmng.ai_target_status IS
    'AI 작업 대상의 git 상태 스냅샷. 실행기가 적고 화면이 읽는다. 대상당 한 줄.';

-- 「확인해 달라」가 찍힌 것만 빠르게 찾는다. 실행기가 한 바퀴마다 본다.
CREATE INDEX IF NOT EXISTS ix_ai_target_status_req
    ON projmng.ai_target_status (probe_req_dt)
 WHERE probe_req_dt IS NOT NULL;

-- 확인
--
--   SELECT t.target_nm, s.branch, s.ahead, s.behind,
--          s.staged + s.unstaged + s.untracked AS dirty, s.probed_at
--     FROM projmng.ai_target t
--     LEFT JOIN projmng.ai_target_status s ON s.target_key = t.target_key
--    WHERE t.is_deleted = false
--    ORDER BY t.target_nm;
