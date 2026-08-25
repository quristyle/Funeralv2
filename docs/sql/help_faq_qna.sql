-- ============================================================
-- 도움말 — F.A.Q · Q&A
-- ============================================================
--
-- 둘 다 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
-- 각 MSA 가 자기 FAQ·Q&A 를 따로 두지 않는다(공지와 같은 방침).
--
-- 화면
--   /help/faq   F.A.Q — 관리자가 쓰고, 나머지는 읽는다
--   /help/qna   Q&A   — 누구나 질문하고, 관리자가 답한다. 답글에 답글을 달 수 있다
--
-- '관리자' 를 무엇으로 판정하나
--   포털은 권한을 scom.role_menus 한 곳에서 관리한다. 그래서 새 플래그를 만들지 않고
--   메뉴 권한을 그대로 쓴다.
--
--     F.A.Q  can_create / can_update / can_delete  → 작성·수정·삭제 (= 관리자)
--            can_view / can_search                 → 읽기 (= 모든 사용자)
--
--     Q&A    can_create                            → 질문·답글 등록 (= 모든 사용자)
--            can_update / can_delete               → 본인 글 수정·삭제
--            can_cust1 ('답변·공개 관리')          → 남의 글까지 답하고 공개 여부를 정한다 (= 관리자)
--
--   Q&A 는 일반 사용자도 글을 써야 하므로 can_create 를 관리자 표시로 쓸 수 없다.
--   그래서 사용자 정의 권한 1번에 이름을 붙여 관리자 표시로 쓴다.
--   (이름은 메뉴 관리 화면에도 그대로 나온다 — menu_permission_items.sql 참고)
--
-- 2026-08-25 실행함. 반복 실행해도 안전하다.
--
--   1) 테이블 · 메뉴 권한 항목   없던 것을 만든다.
--   2) 역할 권한                 기존 role_menus 값을 바꾼다 — **켜기도 하고 끄기도 한다.**
--                                지금 네 역할 모두 F.A.Q 를 쓸 수 있어서, 관리자가 아닌
--                                역할에서는 꺼야 지시대로 동작한다. 자세한 설명은 아래.
--
-- 남은 판단거리는 docs/analysis/21-help-faq-qna.md 의 D-H1 에 적어 두었다.

BEGIN;

-- ── F.A.Q ──────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS scom.faqs (
  id           text        NOT NULL,
  -- 분류. 비우면 '기타' 로 묶어 보여준다.
  category     text,
  question     text        NOT NULL,
  -- 답변 본문 (HTML). 본문에 붙여넣은 이미지는 FileServer 로 가고 경로만 남는다.
  answer       text,
  -- 목록 노출 순서 (작을수록 먼저)
  order_no     integer     NOT NULL DEFAULT 0,
  -- 0 비활성 / 1 활성. 비활성은 관리자에게만 보인다.
  status       integer     NOT NULL DEFAULT 1,
  created_at   timestamptz NOT NULL DEFAULT now(),
  created_by   text,
  updated_at   timestamptz,
  updated_by   text,
  is_deleted   boolean     NOT NULL DEFAULT false,
  CONSTRAINT "PK_faqs" PRIMARY KEY (id)
);

-- 목록은 "활성 + 분류 + 순서" 로만 훑는다.
CREATE INDEX IF NOT EXISTS "IX_faqs_list"
  ON scom.faqs (is_deleted, status, category, order_no);

-- ── Q&A ────────────────────────────────────────────────────
--
-- 질문과 답글을 한 테이블에 담는다. parent_id 로 자기 자신을 가리켜
-- 답글의 답글의 답글까지 깊이 제한 없이 이어진다.
--
--   parent_id IS NULL   질문(스레드 뿌리)
--   root_id             스레드 뿌리 아이디. 뿌리 글은 자기 자신
--   depth               뿌리 = 0, 답글 = 부모 + 1
--
-- root_id 를 따로 두는 이유: 목록에서 스레드 하나를 통째로 가져올 때
-- 부모를 재귀로 따라 올라가지 않고 한 번의 조회로 끝난다.
CREATE TABLE IF NOT EXISTS scom.qna_posts (
  id           text        NOT NULL,
  -- 답글이면 부모 글. 질문이면 NULL
  parent_id    text,
  -- 스레드 뿌리. 뿌리 글은 자기 아이디가 들어간다
  root_id      text        NOT NULL,
  depth        integer     NOT NULL DEFAULT 0,
  -- 제목. 질문(뿌리)만 쓴다. 답글은 비운다
  title        text,
  -- 본문 (HTML)
  content      text        NOT NULL DEFAULT '',
  -- 공개 여부. 관리자가 정한다.
  -- 끄면 작성자 본인과 관리자에게만 보인다.
  is_public    boolean     NOT NULL DEFAULT false,
  -- 관리자가 쓴 답변인지. 화면에서 '답변' 표시를 붙이는 데 쓴다
  is_answer    boolean     NOT NULL DEFAULT false,
  -- 작성자. accounts.user_id (로그인 아이디) 와 그때의 표시 이름을 함께 둔다.
  -- created_by 를 쓰지 않는 이유: AppDbContext 의 감사 로직이 저장할 때
  -- created_by 를 자기 값으로 덮는다. 본인 글 판정에 쓸 수 없다.
  author_id    text,
  author_name  text,
  created_at   timestamptz NOT NULL DEFAULT now(),
  created_by   text,
  updated_at   timestamptz,
  updated_by   text,
  is_deleted   boolean     NOT NULL DEFAULT false,
  CONSTRAINT "PK_qna_posts" PRIMARY KEY (id),
  CONSTRAINT "FK_qna_posts_parent" FOREIGN KEY (parent_id)
    REFERENCES scom.qna_posts (id) ON DELETE CASCADE
);

-- 목록은 스레드 단위로 가져온다.
CREATE INDEX IF NOT EXISTS "IX_qna_posts_root"
  ON scom.qna_posts (root_id, created_at);

CREATE INDEX IF NOT EXISTS "IX_qna_posts_parent"
  ON scom.qna_posts (parent_id);

-- '내 글' 조회
CREATE INDEX IF NOT EXISTS "IX_qna_posts_author"
  ON scom.qna_posts (author_id);

COMMIT;


-- ============================================================
-- 메뉴가 쓰는 권한 항목
-- ============================================================
--
-- 메뉴가 "쓴다"고 지정하지 않은 항목은 역할에 켜져 있어도 꺼진 값으로 내려간다
-- (MenuService.GetMenuPermissionsAsync). 그래서 여기서 먼저 켜 준다.

BEGIN;

-- use_* · cust1_name 은 menu_permission_items.sql 이 만든다.
-- 그 스크립트를 아직 돌리지 않은 환경에서도 이 파일 하나로 끝나게 확인만 해 둔다.
-- (같은 정의다. 이미 있으면 아무 일도 하지 않는다.)
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_view   boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_search boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_create boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_delete boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_update boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_print  boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_excel  boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_cust1  boolean NOT NULL DEFAULT false;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS cust1_name text;

-- 메뉴가 꺼져 있으면 라우트 자체가 생기지 않는다. 두 화면을 쓰기로 했으므로 켠다.
UPDATE scom.system_menus SET
  status = 1, is_deleted = false,
  updated_at = now(), updated_by = 'help-faq-qna'
WHERE path IN ('/help/faq', '/help/qna')
  AND (status <> 1 OR is_deleted);

-- F.A.Q — 읽기 + 작성·수정·삭제. 출력·엑셀은 쓰지 않는다.
UPDATE scom.system_menus SET
  use_view   = true,  use_search = true,
  use_create = true,  use_update = true,  use_delete = true,
  use_print  = false, use_excel  = false,
  use_cust1  = false, cust1_name = NULL,
  updated_at = now(), updated_by = 'help-faq-qna'
WHERE path = '/help/faq';

-- Q&A — 읽기 + 질문·답글 등록 + 본인 글 수정·삭제
--        + 사용자 정의 1 = 관리자(답변·공개 관리)
UPDATE scom.system_menus SET
  use_view   = true,  use_search = true,
  use_create = true,  use_update = true,  use_delete = true,
  use_print  = false, use_excel  = false,
  use_cust1  = true,  cust1_name = '답변·공개 관리',
  updated_at = now(), updated_by = 'help-faq-qna'
WHERE path = '/help/qna';

COMMIT;


-- ============================================================
-- 역할 권한 부여
-- ============================================================
--
-- 두 갈래로 나눈다.
--
--   모든 역할     읽기(can_view·can_search) + Q&A 질문·본인 글 수정·삭제
--   관리자 역할   F.A.Q 작성·수정·삭제 + Q&A 답변·공개 관리(can_cust1)
--
-- ── 관리자 역할을 어떻게 잡았나 ────────────────────────────
--
-- 처음에는 역할 이름을 코드에 박지 않으려고 "관리자 화면(/system · /auth)을
-- 수정할 수 있는 역할" 로 잡았다. **실제로 돌려 보니 4개 역할이 전부 잡혔다.**
-- role_partner_tighten.sql 을 아직 실행하지 않아서 PARTNER(계정 39개)도
-- 그 권한을 그대로 갖고 있었다. 그러면 파트너 39명이 F.A.Q 를 쓰고
-- Q&A 를 공개할 수 있게 되어 지시와 반대가 된다. 그래서 명시 목록으로 바꿨다.
--
-- 지금 등록된 역할은 넷이다.
--
--   ADMINISTRATOR          관리자        계정 1개   → 관리자
--   SYSTEM_ADMINISTRATOR   시스템관리자   계정 1개   → 관리자
--   PARTNER_ADMINISTRATOR  파트너 관리자  계정 0개   → 판단 대기 (아래 D-H1)
--   PARTNER                파트너        계정 39개  → 읽기·질문만
--
-- PARTNER_ADMINISTRATOR 는 넣지 않았다. 이름은 관리자지만 파트너 쪽 관리자이고,
-- 지금 만든 Q&A 는 회사별로 갈라져 있지 않다(포털 전체가 하나의 Q&A 다).
-- 파트너 관리자가 남의 회사 질문까지 공개·답변하게 되는 것이 맞는지는 판단이 필요하다.
-- 배정된 계정이 0개라 지금 당장 달라지는 것은 없다.
--
-- ── 이 스크립트가 권한을 **끄기도 한다** ───────────────────
--
-- role_menu_backfill.sql 이 "메뉴가 쓴다고 지정한 항목은 모두 허용" 으로 채워 둔 탓에
-- 지금은 네 역할 모두 F.A.Q 를 등록·수정·삭제할 수 있다. 지시대로 만들려면
-- 관리자가 아닌 역할에서 그 권한을 **꺼야 한다.** 아래 UPDATE 가 그 일을 한다.
--
-- 되돌리려면 역할 권한 화면에서 다시 켜거나 role_menu_backfill.sql 을 실행한다.

BEGIN;

-- 관리자로 볼 역할. 바꿀 곳은 여기 한 곳이다.
CREATE TEMP TABLE help_admin_roles (role_id text PRIMARY KEY) ON COMMIT DROP;
INSERT INTO help_admin_roles (role_id) VALUES
  ('ADMINISTRATOR'),
  ('SYSTEM_ADMINISTRATOR');

-- 없는 행만 만든다. 이미 있는 행은 아래 UPDATE 두 개가 값을 맞춘다.
INSERT INTO scom.role_menus (
  role_id, menu_id,
  can_view, can_search, can_create, can_delete, can_update, can_print, can_excel,
  can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
  created_at, created_by, is_deleted
)
SELECT r.id, m.id,
       false, false, false, false, false, false, false,
       false, false, false, false, false, false, false, false,
       now(), 'help-faq-qna', false
FROM scom.roles r
CROSS JOIN scom.system_menus m
WHERE r.is_deleted = false
  AND m.path IN ('/help/faq', '/help/qna')
  AND NOT EXISTS (
    SELECT 1 FROM scom.role_menus rm WHERE rm.role_id = r.id AND rm.menu_id = m.id
  );

-- 읽기는 모든 역할에 준다.
UPDATE scom.role_menus rm SET
  can_view = true, can_search = true,
  updated_at = now(), updated_by = 'help-faq-qna'
FROM scom.system_menus m
WHERE rm.menu_id = m.id
  AND m.path IN ('/help/faq', '/help/qna');

-- Q&A 는 누구나 질문하고 본인 글을 고칠 수 있다.
UPDATE scom.role_menus rm SET
  can_create = true, can_update = true, can_delete = true,
  updated_at = now(), updated_by = 'help-faq-qna'
FROM scom.system_menus m
WHERE rm.menu_id = m.id
  AND m.path = '/help/qna';

-- F.A.Q 작성·수정·삭제와 Q&A 답변·공개 관리는 관리자 역할만 갖는다.
-- 관리자가 아닌 역할에서는 **끈다** (지금 켜져 있기 때문이다 — 위 설명 참고).
-- 관리자 여부는 EXISTS 로 본다. UPDATE ... FROM 의 LEFT JOIN 안에서는
-- 대상 테이블(rm)을 가리킬 수 없어서 조인 대신 이렇게 쓴다.
UPDATE scom.role_menus rm SET
  can_create = CASE WHEN m.path = '/help/faq' THEN
                 EXISTS (SELECT 1 FROM help_admin_roles a WHERE a.role_id = rm.role_id)
               ELSE rm.can_create END,
  can_update = CASE WHEN m.path = '/help/faq' THEN
                 EXISTS (SELECT 1 FROM help_admin_roles a WHERE a.role_id = rm.role_id)
               ELSE rm.can_update END,
  can_delete = CASE WHEN m.path = '/help/faq' THEN
                 EXISTS (SELECT 1 FROM help_admin_roles a WHERE a.role_id = rm.role_id)
               ELSE rm.can_delete END,
  can_cust1  = CASE WHEN m.path = '/help/qna' THEN
                 EXISTS (SELECT 1 FROM help_admin_roles a WHERE a.role_id = rm.role_id)
               ELSE rm.can_cust1 END,
  updated_at = now(), updated_by = 'help-faq-qna'
FROM scom.system_menus m
WHERE rm.menu_id = m.id
  AND m.path IN ('/help/faq', '/help/qna');

COMMIT;


-- ============================================================
-- 확인
-- ============================================================

-- 테이블
SELECT table_name, count(*) AS 컬럼수
FROM information_schema.columns
WHERE table_schema = 'scom' AND table_name IN ('faqs', 'qna_posts')
GROUP BY table_name ORDER BY table_name;

-- 메뉴가 쓴다고 지정한 항목
SELECT path, title, status, use_view, use_create, use_update, use_delete,
       use_cust1, cust1_name
FROM scom.system_menus
WHERE path IN ('/help/faq', '/help/qna');

-- 역할별 권한 (관리자 역할에만 F.A.Q 작성 · Q&A cust1 이 켜져 있어야 한다)
SELECT m.path, rm.role_id,
       rm.can_view, rm.can_create, rm.can_update, rm.can_delete, rm.can_cust1
FROM scom.role_menus rm
JOIN scom.system_menus m ON m.id = rm.menu_id
WHERE m.path IN ('/help/faq', '/help/qna')
ORDER BY m.path, rm.role_id;
