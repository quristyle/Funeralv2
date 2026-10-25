-- ============================================================
-- 「Gmail 보관함」 메뉴를 들인다 (scom · 포털 DB)
-- ============================================================
--
--   Gmail 보관함  SYS_GMAIL_ARCHIVE  /admin/ai/gmail  admin.ai.gmail
--
-- 「업무 비서」(SYS_AI_ASSISTANT) 바로 다음 자리. 같은 System 묶음 산하이고,
-- RAG 문서함 → 업무 비서 → Gmail 보관함이 한 맥락(사내 자료 · AI)이라 나란히 둔다.
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/GmailArchivePage.razor
-- 서버: microservices/AIAgentServer/Endpoints/GmailEndpoints.cs
-- 설계: docs/gmail-archive.md
--
--   psql -h … -U funeralv2 -d jsiniportal -f gmail-archive-menu-2026-10-25.sql
--
-- 두 번 돌려도 안전하다.
--
-- ── 사용 권한 칸 — 보기와 등록 ──────────────────────────────
--
-- use_view 와 use_create 둘만 켠다. 이 화면에서 「만드는」 일이 셋이다 —
-- 메일함 등록 · mbox 적재 · RAG 문서 만들기. 수정·삭제는 없다(적재는 덮어쓰지
-- 않고 겹침을 버리며, 올린 문서를 여기서 지우지 않는다 — 그것은 RAG 문서함의 일이다).
-- 검색·인쇄·엑셀도 이 화면에 그런 동작이 없다.
--
-- ── 왜 시스템관리자에게만 여는가 ────────────────────────────
--
-- 이 화면이 다루는 것은 **사람의 메일 원본**이다. mbox 를 부으면 본문이 통째로
-- 표에 들어가고, RAG 로 올리면 그 글이 사내 문서함에서 검색된다. 메일함마다
-- 주인이 있어 남의 메일함에는 못 붓지만(owner_login_id), 그 앞에 메뉴 권한으로
-- 한 번 더 좁혀 둔다.
-- ============================================================

BEGIN;

-- ── 메뉴 한 줄 ───────────────────────────────────────────────
INSERT INTO scom.system_menus
    (id, name, path, pid, type, title, icon, order_no,
     hide_in_menu, status, created_at, created_by, keep_alive,
     use_view, use_search, use_create, use_update, use_delete, use_print, use_excel,
     use_mobile, use_tablet,
     route_key)
SELECT
    'SYS_GMAIL_ARCHIVE', 'GmailArchive', '/admin/ai/gmail',
    a.pid,                   -- 업무 비서와 같은 부모(System 묶음)
    'MENU',
    'Gmail 보관함', 'lucide:mail-search',
    a.order_no + 1,          -- 업무 비서 바로 다음 자리
    false, 1, now(), 'gmail-archive-menu', false,
    true, false, true, false, false, false, false,
    true, true,
    'admin.ai.gmail'
  FROM scom.system_menus a
 WHERE a.id = 'SYS_AI_ASSISTANT'
   AND NOT EXISTS (
       SELECT 1 FROM scom.system_menus x WHERE x.id = 'SYS_GMAIL_ARCHIVE'
   );

-- ── 시스템관리자에게 연다 ────────────────────────────────────
INSERT INTO scom.role_menus
    (role_id, menu_id, can_view,
     can_search, can_create, can_delete, can_update, can_print, can_excel,
     can_cust1, can_cust2, can_cust3, can_cust4, can_cust5, can_cust6, can_cust7, can_cust8,
     created_at, created_by, is_deleted)
SELECT 'SYSTEM_ADMINISTRATOR', 'SYS_GMAIL_ARCHIVE', true,
       false, true, false, false, false, false,
       false, false, false, false, false, false, false, false,
       now(), 'gmail-archive-menu', false
 WHERE NOT EXISTS (
     SELECT 1 FROM scom.role_menus
      WHERE role_id = 'SYSTEM_ADMINISTRATOR' AND menu_id = 'SYS_GMAIL_ARCHIVE'
 );

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT id, title, path, route_key, pid, order_no
--   FROM scom.system_menus WHERE id = 'SYS_GMAIL_ARCHIVE';
-- SELECT role_id, can_view, can_create
--   FROM scom.role_menus WHERE menu_id = 'SYS_GMAIL_ARCHIVE';
--
-- 되돌리기
--   DELETE FROM scom.menu_favorites WHERE menu_id = 'SYS_GMAIL_ARCHIVE';
--   DELETE FROM scom.role_menus     WHERE menu_id = 'SYS_GMAIL_ARCHIVE';
--   DELETE FROM scom.system_menus   WHERE id      = 'SYS_GMAIL_ARCHIVE';
