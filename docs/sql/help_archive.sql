-- ============================================================
-- 도움말 — 자료실 (/help/archive)
-- ============================================================
--
-- 지시: "자료의 설명을 확인하고 다운로드 할 수 있도록 만들어라."
--
-- F.A.Q 와 같은 방침이다 — JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로
-- 보인다. 관리자가 자료를 올리고 나머지 사용자는 설명을 읽고 내려받는다.
--
-- ── 자료 하나에 파일 여럿 ──────────────────────────────────
--
-- 표를 둘로 나눈다. 자료실 항목은 "설명이 붙은 묶음" 이고 파일은 그 안에 여러 개
-- 들어갈 수 있다(설치 파일 + 설명서 + 예제처럼). 항목마다 파일 하나로 묶으면
-- 같은 설명을 여러 번 적게 된다.
--
-- 파일 표의 모양은 `scom.notice_files` 와 똑같이 맞췄다. 첨부는 전부 FileServer 로
-- 가고 우리는 FileServer 가 발급한 file_id 만 들고 있다(공지와 같은 구조).
--
-- ── 다운로드 수를 어떻게 세나 ──────────────────────────────
--
-- 파일은 FileServer 가 내려준다(`/api/file/download/id/{fileId}`). 브라우저가 그
-- 주소를 직접 열면 AuthServer 는 아무것도 모르므로 셀 수가 없다.
-- 그래서 AuthServer 에 세는 경로를 두고 거기서 FileServer 로 302 로 넘긴다.
--
--   GET /api/auth/help/archives/{id}/files/{fileId}/download
--       → download_count + 1  →  302  /api/file/download/id/{fileId}
--
-- 반복 실행해도 안전하다.

BEGIN;

-- ── 자료실 항목 ────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS scom.help_archives (
  id             text        NOT NULL,
  -- 분류. 비우면 화면이 '기타' 로 묶어 보여준다 (F.A.Q 와 같은 방식).
  category       text,
  -- 자료명
  title          text        NOT NULL,
  -- 자료 설명. 이 화면의 핵심이다 — 무엇을 내려받는 것인지 알려 준다.
  -- HTML 이다. 본문에 붙여넣은 이미지는 FileServer 로 가고 경로만 남는다.
  description    text,
  -- 목록 노출 순서 (작을수록 먼저)
  order_no       integer     NOT NULL DEFAULT 0,
  -- 0 비활성 / 1 활성. 비활성은 관리자에게만 보인다.
  status         integer     NOT NULL DEFAULT 1,
  -- 내려받은 횟수 (항목 기준 합계)
  download_count integer     NOT NULL DEFAULT 0,
  created_at     timestamptz NOT NULL DEFAULT now(),
  created_by     text,
  updated_at     timestamptz,
  updated_by     text,
  is_deleted     boolean     NOT NULL DEFAULT false,
  CONSTRAINT "PK_help_archives" PRIMARY KEY (id)
);

COMMENT ON TABLE scom.help_archives IS
  '자료실 항목. 설명이 붙은 파일 묶음이다. 관리자가 올리고 모든 사용자가 내려받는다.';

-- 목록은 "활성 + 분류 + 순서" 로만 훑는다 (F.A.Q 와 같다).
CREATE INDEX IF NOT EXISTS "IX_help_archives_list"
  ON scom.help_archives (is_deleted, status, category, order_no);

-- ── 자료실 첨부파일 ────────────────────────────────────────
CREATE TABLE IF NOT EXISTS scom.help_archive_files (
  id             text        NOT NULL,
  archive_id     text        NOT NULL,
  -- FileServer 가 발급한 파일 아이디. 실제 파일은 우리가 갖고 있지 않다.
  file_id        text        NOT NULL,
  -- 원본 파일명. 내려받을 때 이 이름으로 저장된다.
  file_name      text        NOT NULL,
  -- 바이트 크기. 목록에서 "얼마나 큰 파일인지" 를 미리 알려 준다.
  file_size      bigint      NOT NULL DEFAULT 0,
  content_type   text,
  sort_no        integer     NOT NULL DEFAULT 0,
  -- 이 파일이 내려받힌 횟수
  download_count integer     NOT NULL DEFAULT 0,
  created_at     timestamptz NOT NULL DEFAULT now(),
  created_by     text,
  updated_at     timestamptz,
  updated_by     text,
  is_deleted     boolean     NOT NULL DEFAULT false,
  CONSTRAINT "PK_help_archive_files" PRIMARY KEY (id)
);

COMMENT ON TABLE scom.help_archive_files IS
  '자료실 첨부파일. 모양을 scom.notice_files 와 맞췄다. 실제 파일은 FileServer 가 갖는다.';

CREATE INDEX IF NOT EXISTS "IX_help_archive_files_archive"
  ON scom.help_archive_files (archive_id, sort_no);

-- 자료를 지우면 첨부 목록도 함께 지운다.
-- 남겨 두면 아무 곳도 가리키지 않는 행이 쌓인다.
DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM pg_constraint WHERE conname = 'FK_help_archive_files_archives'
  ) THEN
    ALTER TABLE scom.help_archive_files
      ADD CONSTRAINT "FK_help_archive_files_archives"
      FOREIGN KEY (archive_id) REFERENCES scom.help_archives (id) ON DELETE CASCADE;
  END IF;
END $$;

-- ── 메뉴가 쓰는 권한 항목 ──────────────────────────────────
--
-- 메뉴가 꺼져 있으면 라우트 자체가 생기지 않는다. 쓰기로 했으므로 켠다.
UPDATE scom.system_menus SET
  status = 1, is_deleted = false,
  updated_at = now(), updated_by = 'help-archive'
WHERE path = '/help/archive'
  AND (status <> 1 OR is_deleted);

-- 자료실 — 읽기 + 등록·수정·삭제. 출력·엑셀은 쓰지 않는다 (F.A.Q 와 같다).
UPDATE scom.system_menus SET
  use_view   = true,  use_search = true,
  use_create = true,  use_update = true,  use_delete = true,
  use_print  = false, use_excel  = false,
  use_cust1  = false, cust1_name = NULL,
  updated_at = now(), updated_by = 'help-archive'
WHERE path = '/help/archive';

COMMIT;


-- ============================================================
-- 역할 권한
-- ============================================================
--
-- **이 블록은 권한을 끄기도 한다.**
--
-- role_menu_backfill.sql 이 "메뉴가 쓴다고 지정한 항목은 모두 허용" 으로 채워 둔 탓에
-- 지금은 네 역할 모두 자료실에 등록·수정·삭제까지 할 수 있다. 그러면 파트너 39명이
-- 자료를 올리고 지울 수 있어 지시와 반대가 된다.
--
--   모든 역할     읽기(can_view · can_search) + 다운로드
--   관리자 역할   등록·수정·삭제
--
-- 관리자 역할은 F.A.Q 와 **똑같은 목록**으로 잡았다(help_faq_qna.sql 참고).
--
--   ADMINISTRATOR          관리자        → 관리자
--   SYSTEM_ADMINISTRATOR   시스템관리자   → 관리자
--   PARTNER_ADMINISTRATOR  파트너 관리자  → 읽기만 (아래 참고)
--   PARTNER                파트너        → 읽기만
--
-- PARTNER_ADMINISTRATOR 를 넣지 않은 이유도 F.A.Q 와 같다. 이름은 관리자지만
-- 파트너 쪽 관리자이고, 자료실은 회사별로 갈라져 있지 않다(포털 전체가 하나다).
-- 파트너 관리자가 전사 자료를 올리고 지우는 것이 맞는지는 판단이 필요하다
-- → 21-help-faq-qna.md 의 D-H1 과 같은 건이다. 배정 계정이 0개라 지금은 차이가 없다.

BEGIN;

-- 모든 역할 — 읽기는 열어 둔다.
UPDATE scom.role_menus rm SET
  can_view = true, can_search = true,
  updated_at = now(), updated_by = 'help-archive'
WHERE rm.menu_id = 'ARCHIVE'
  AND (rm.can_view = false OR rm.can_search = false);

-- 관리자 역할 — 등록·수정·삭제를 켠다.
UPDATE scom.role_menus rm SET
  can_create = true, can_update = true, can_delete = true,
  updated_at = now(), updated_by = 'help-archive'
WHERE rm.menu_id = 'ARCHIVE'
  AND rm.role_id IN ('ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR')
  AND (rm.can_create = false OR rm.can_update = false OR rm.can_delete = false);

-- 그 밖의 역할 — 등록·수정·삭제를 끈다.
UPDATE scom.role_menus rm SET
  can_create = false, can_update = false, can_delete = false,
  updated_at = now(), updated_by = 'help-archive'
WHERE rm.menu_id = 'ARCHIVE'
  AND rm.role_id NOT IN ('ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR')
  AND (rm.can_create OR rm.can_update OR rm.can_delete);

COMMIT;

-- 확인
SELECT r.name AS 역할, rm.can_view AS 열람, rm.can_search AS 검색,
       rm.can_create AS 등록, rm.can_update AS 수정, rm.can_delete AS 삭제
FROM scom.role_menus rm
JOIN scom.roles r ON r.id = rm.role_id
WHERE rm.menu_id = 'ARCHIVE'
ORDER BY r.id;
