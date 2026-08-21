-- ============================================================
-- 공지 (JSini 관리 포털 공통)
-- ============================================================
--
-- 공지는 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
-- 화면 진입 시 팝업으로 띄우며, 두 가지 경우가 있다.
--
--   is_public = true   로그인 전에도 보인다. 화면이 뜨자마자 띄운다.
--   is_public = false  로그인한 사용자에게만. 로그인 직후 띄운다.
--
-- 첨부파일은 FileServer 에 올리고 여기에는 파일 아이디만 둔다.
--
-- 반복 실행해도 안전하다.

BEGIN;

CREATE TABLE IF NOT EXISTS scom.notices (
  id           text        NOT NULL,
  title        text        NOT NULL,
  content      text,
  -- 로그인하지 않은 사용자도 볼 수 있는지
  is_public    boolean     NOT NULL DEFAULT false,
  -- 팝업으로 띄울지. 끄면 공지 목록에만 남는다.
  is_popup     boolean     NOT NULL DEFAULT true,
  -- 게시 기간. NULL 이면 제한 없음
  start_at     timestamptz,
  end_at       timestamptz,
  -- 0 비활성 / 1 활성
  status       integer     NOT NULL DEFAULT 1,
  -- 목록·팝업 노출 순서 (작을수록 먼저)
  order_no     integer     NOT NULL DEFAULT 0,
  created_at   timestamptz NOT NULL DEFAULT now(),
  created_by   text,
  updated_at   timestamptz,
  updated_by   text,
  is_deleted   boolean     NOT NULL DEFAULT false,
  CONSTRAINT "PK_notices" PRIMARY KEY (id)
);

-- 팝업 조회는 "활성 + 기간 내" 를 매번 훑으므로 인덱스를 둔다.
CREATE INDEX IF NOT EXISTS "IX_notices_active"
  ON scom.notices (status, is_deleted, start_at, end_at);

CREATE TABLE IF NOT EXISTS scom.notice_files (
  id           text        NOT NULL,
  notice_id    text        NOT NULL,
  -- FileServer 가 발급한 파일 아이디
  file_id      text        NOT NULL,
  file_name    text        NOT NULL,
  file_size    bigint      NOT NULL DEFAULT 0,
  content_type text,
  sort_no      integer     NOT NULL DEFAULT 0,
  created_at   timestamptz NOT NULL DEFAULT now(),
  created_by   text,
  updated_at   timestamptz,
  updated_by   text,
  is_deleted   boolean     NOT NULL DEFAULT false,
  CONSTRAINT "PK_notice_files" PRIMARY KEY (id),
  CONSTRAINT "FK_notice_files_notices" FOREIGN KEY (notice_id)
    REFERENCES scom.notices (id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "IX_notice_files_notice_id"
  ON scom.notice_files (notice_id);

COMMIT;

-- 확인
SELECT table_name, count(*) AS 컬럼수
FROM information_schema.columns
WHERE table_schema = 'scom' AND table_name IN ('notices', 'notice_files')
GROUP BY table_name ORDER BY table_name;
