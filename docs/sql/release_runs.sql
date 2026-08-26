-- ============================================================
-- 배포 실행 기록 (/portal/release)
-- ============================================================
--
-- 지시: "실제 처리되는 내용과 처리 결과를 받으면서 배포처리를 하려면"
--
-- ── 무엇이 문제였나 ────────────────────────────────────────
--
-- 화면(views/portal/release/index.vue)이 `setTimeout` 으로 7단계를 순서대로 초록색
-- [SUCCESS] 로 찍고 있었다. 서버에서 오는 정보가 아니었다. 그래서
--
--   * 배포 장비에서 스크립트가 실패해도 화면은 전부 초록이었다
--   * 큐 소비자가 아예 안 떠 있어도 성공으로 보였다
--   * 누가 언제 무엇을 배포했는지 남는 곳이 없었다 (콘솔 로그뿐)
--
-- ── 없던 것은 run id 였다 ──────────────────────────────────
--
-- 요청 한 건을 행 하나로 만들면 나머지가 따라온다. 배포 장비의 래퍼가 그 id 로
-- 진행 상황을 되돌려 보고하고, 화면은 그 id 를 폴링한다. 새로 고쳐도 이어 본다.
--
--   POST /api/auth/release/{key}              → release_runs 행 생성 + 큐에 발행
--   POST /api/auth/release/runs/{id}/events   ← 배포 장비가 보고 (run 별 토큰 인증)
--   GET  /api/auth/release/runs/{id}?sinceSeq=N → 화면이 폴링
--
-- ── status 값의 뜻 ─────────────────────────────────────────
--
--   queued      큐에 넣었고 아직 아무도 집어가지 않았다  ← 지금까지 감춰져 있던 상태
--   running     배포 장비가 집어가서 돌고 있다
--   succeeded   스크립트가 0 으로 끝났다
--   failed      0 이 아닌 코드로 끝났다
--   timeout     제한 시간을 넘겨도 소식이 없다 (소비자가 죽었거나 없다)
--   dispatched  보고를 하지 않는 대상에 요청만 보냈다 (개선 전과 같은 동작)
--
-- 'dispatched' 가 따로 있는 이유: 배포 장비의 큐 소비자는 이 저장소 밖에 있고
-- 아직 래퍼를 붙이지 않았다. 붙이기 전에는 보고가 올 수 없으므로 성공/실패를
-- 아는 척하지 않고 "요청을 보냈다" 까지만 말한다. 대상별 설정
-- (Release:Targets[].ReportsProgress) 을 켜면 queued → running → succeeded 로 간다.
--
-- 반복 실행해도 안전하다.

BEGIN;

-- ── 배포 실행 한 건 ────────────────────────────────────────
CREATE TABLE IF NOT EXISTS scom.release_runs (
  id                text        NOT NULL,
  -- 어떤 대상을 배포했나. 설정(Release:Targets)의 Key 다.
  target_key        text        NOT NULL,
  -- 그때의 표시 이름. 설정이 나중에 바뀌어도 이력은 그대로 읽혀야 한다.
  target_name       text        NOT NULL,
  -- 그때 실행을 요청한 스크립트 경로. 이력을 보고 "무엇이 돌았나" 를 알 수 있어야 한다.
  script_path       text,
  -- 스크립트에 넘긴 인자 (JSON 배열 문자열)
  args              text,
  -- queued / running / succeeded / failed / timeout / dispatched (위 설명 참고)
  status            text        NOT NULL DEFAULT 'queued',
  -- 이 대상이 보고를 하기로 되어 있었나. 나중에 설정을 바꿔도 이력의 해석이 흔들리지 않게 박아 둔다.
  reports_progress  boolean     NOT NULL DEFAULT false,
  -- 요청한 사람 (게이트웨이가 넘긴 로그인 아이디)
  requested_by      text,
  -- 배포 장비가 처음 보고를 보내 온 시각. 큐에서 집어간 시각이다.
  started_at        timestamptz,
  finished_at       timestamptz,
  -- 스크립트 종료 코드. 0 이면 성공으로 본다.
  exit_code         integer,
  -- 마지막으로 지나간 단계 이름 (스크립트가 '##STEP ...' 을 찍은 경우)
  current_step      text,
  -- 사람이 읽을 최종 한 줄. 실패 사유가 여기 들어간다.
  message           text,
  -- 배포 후 대상이 스스로 알려 준 버전. 설정에 VersionUrl 이 있는 대상만 채워진다.
  -- 종료 코드 0 이어도 이 값이 안 바뀌면 "돌기는 했는데 반영이 안 됐다" 를 알 수 있다.
  deployed_version  text,
  -- 이 시간을 넘기면 timeout 으로 본다 (초).
  timeout_seconds   integer     NOT NULL DEFAULT 600,
  -- 배포 장비가 보고할 때 쓰는 1회용 토큰. 계정 인증이 아니라 실행 인증이다.
  -- 끝나면 지운다 — 남겨 두면 끝난 run 에 아무나 로그를 덧붙일 수 있다.
  callback_token    text,
  -- 지금까지 받은 이벤트의 마지막 순번. 화면이 sinceSeq 로 이어 받는다.
  last_seq          integer     NOT NULL DEFAULT 0,
  created_at        timestamptz NOT NULL DEFAULT now(),
  created_by        text,
  updated_at        timestamptz,
  updated_by        text,
  is_deleted        boolean     NOT NULL DEFAULT false,
  CONSTRAINT "PK_release_runs" PRIMARY KEY (id)
);

COMMENT ON TABLE scom.release_runs IS
  '배포 실행 한 건. 요청·진행·결과를 남긴다. 배포 장비의 래퍼가 run id 로 되돌려 보고한다.';

-- 이력 화면은 "최근 것부터" 로만 훑는다.
CREATE INDEX IF NOT EXISTS "IX_release_runs_recent"
  ON scom.release_runs (is_deleted, created_at DESC);

CREATE INDEX IF NOT EXISTS "IX_release_runs_target"
  ON scom.release_runs (target_key, created_at DESC);

-- ── 같은 대상을 동시에 두 번 배포하지 못하게 한다 ──────────
--
-- 화면에서 버튼을 잠그는 것만으로는 두 사람이 동시에 누르는 것을 막지 못한다.
-- 그러면 같은 체크아웃에서 스크립트 둘이 돌아 결과를 예측할 수 없다.
-- 애플리케이션이 먼저 확인하기도 하지만, 경합은 여기서만 확실히 막힌다.
--
-- 'dispatched' 는 넣지 않는다 — 보고가 오지 않는 대상이라 영원히 안 풀린다.
CREATE UNIQUE INDEX IF NOT EXISTS "UX_release_runs_active_target"
  ON scom.release_runs (target_key)
  WHERE status IN ('queued', 'running') AND is_deleted = false;

-- ── 진행 로그 한 줄 ────────────────────────────────────────
--
-- 배포 장비가 스크립트의 stdout 을 한 줄씩 보내 온다. 화면이 그대로 보여 준다.
-- 이제 화면에 보이는 줄은 전부 실제로 일어난 일이다.
CREATE TABLE IF NOT EXISTS scom.release_run_events (
  id           text        NOT NULL,
  run_id       text        NOT NULL,
  -- run 안에서의 순번. 화면이 sinceSeq 로 이어 받으므로 빈틈이 없어야 한다.
  seq          integer     NOT NULL,
  -- info / stdout / step / warn / error / result
  level        text        NOT NULL DEFAULT 'stdout',
  -- 'step' 인 경우의 단계 이름
  step         text,
  message      text        NOT NULL DEFAULT '',
  -- 서버가 받은 시각이다. 배포 장비의 시계를 믿지 않는다.
  created_at   timestamptz NOT NULL DEFAULT now(),
  created_by   text,
  updated_at   timestamptz,
  updated_by   text,
  is_deleted   boolean     NOT NULL DEFAULT false,
  CONSTRAINT "PK_release_run_events" PRIMARY KEY (id)
);

COMMENT ON TABLE scom.release_run_events IS
  '배포 진행 로그 한 줄. 배포 장비가 스크립트 stdout 을 보내 온 것이다.';

-- 화면은 "이 run 의 seq 이후" 로만 읽는다. 같은 seq 가 두 번 들어오면 안 된다.
CREATE UNIQUE INDEX IF NOT EXISTS "UX_release_run_events_seq"
  ON scom.release_run_events (run_id, seq);

-- run 을 지우면 로그도 함께 지운다. 남겨 두면 아무 곳도 가리키지 않는 행이 쌓인다.
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'FK_release_run_events_runs'
  ) THEN
    ALTER TABLE scom.release_run_events
      ADD CONSTRAINT "FK_release_run_events_runs"
      FOREIGN KEY (run_id) REFERENCES scom.release_runs (id) ON DELETE CASCADE;
  END IF;
END $$;

COMMIT;

-- ============================================================
-- 메뉴 권한은 그대로 쓴다
-- ============================================================
--
-- release_menu.sql 이 이미 '/portal/release' 에 can_cust1 = '배포 실행' 을 만들어
-- SYSTEM_ADMINISTRATOR 에게만 주었다. 이번 작업은 그 판정을 **서버에서도** 하도록
-- 고친 것이라(예전에는 화면의 v-perm 뿐이었다) 권한 데이터는 손대지 않는다.
--
-- 확인만 한다.
SELECT r.name AS 역할, rm.can_view AS 열람, rm.can_cust1 AS 배포실행
FROM scom.role_menus rm
JOIN scom.roles r ON r.id = rm.role_id
WHERE rm.menu_id = 'PORTAL_RELEASE'
ORDER BY r.id;
