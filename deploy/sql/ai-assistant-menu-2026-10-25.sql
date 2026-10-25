-- ============================================================
-- 「업무 비서」 메뉴를 들인다 (scom · 포털 DB)
-- ============================================================
--
--   업무 비서  SYS_AI_ASSISTANT  /admin/ai/assistant  admin.ai.assistant
--
-- 「RAG 문서함」(SYS_RAG_DOCS) 바로 다음 자리에 넣는다 — 같은 System 묶음
-- (pid = RAG 문서함의 pid) 산하이고, 같은 사람이 같은 맥락(사내 자료 ·
-- AI)에서 찾는 화면이라 나란히 둔다.
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/Assistant.razor
-- 서버: microservices/AIAgentServer/Endpoints/AssistantEndpoints.cs
--
-- ── 이 메뉴 줄이 유일한 빗장이다 ────────────────────────────
--
-- 이 화면은 **회사 경계를 넘어 전 직원의** 헬프데스크 요청글 · 일정 · 할 일 ·
-- 메일을 모델에게 도구로 쥐어 주고 물어본다(요청자 자신의 권한을 보지 않는다
-- — AssistantEndpoints.cs 머리말, AssistantClient.cs 의 AssistantScope 주석).
-- 서버 쪽에는 그 이상의 역할 검사가 없다 — 화면에 들어올 수 있는 사람을
-- **메뉴 권한 하나로** 좁혀 두었다는 전제다. 그래서 이 메뉴의 role_menus 를
-- 잘못 넓히면 그대로 전사 자료 유출이다.
--
-- ── 줄 자격 — 슈퍼관리자 · 임원 ──────────────────────────────
--
-- 설계는 **슈퍼관리자(SYSTEM_ADMINISTRATOR)** 와 **임원** 둘에게만 열기로
-- 했다. 그런데 2026-10-25 현재 scom.roles 에는 「임원」에 해당하는 역할이
-- 없다(아래 질의로 확인함 — 17개 역할 중 이름·id 어디에도 '임원'·'exec' 가
-- 없다). 그래서 **이 파일은 role_menus 를 건드리지 않는다** — 없는 역할을
-- 지어내 매지 않고, 있는 역할(SYSTEM_ADMINISTRATOR)에만 슬쩍 열어 「임원」
-- 몫을 흉내 내지도 않는다. 역할을 만들고 난 뒤 메뉴 권한 화면에서 두 역할
-- 모두에게 직접 연다 — 자세한 절차는
-- .superpowers/sdd/2026-10-25-portal-ai-assistant/task-11-report.md.
--
--   SELECT id, name, remark FROM scom.roles ORDER BY id;
--   -- 임원이 있는지 다시 확인하려면:
--   SELECT id, name FROM scom.roles WHERE name ILIKE '%임원%' OR id ILIKE '%exec%';
--
-- ── path 와 @page 가 같아야 한다 ────────────────────────────
--
-- 라우팅의 주인은 화면의 `@page`(web/CLAUDE.md) 이고 여기 적는 path 는
-- 그 값 그대로다. 사이드바가 실제로 거는 링크는 route_key 로 푼다.
--
-- ── 사용 권한 칸 — 보기만 ────────────────────────────────────
--
-- 대화뿐인 화면이라 use_view 만 켠다. 검색·등록·수정·삭제·인쇄·엑셀은
-- 이 화면에 그런 동작이 없다 — 켜 두면 권한 화면에 뜻 없는 칸이 생긴다
-- (SYS_AI_USAGE 와 같은 결정).
--
--   psql -h … -U funeralv2 -d jsiniportal -f ai-assistant-menu-2026-10-25.sql
--
-- 두 번 돌려도 안전하다.
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
    'SYS_AI_ASSISTANT', 'Assistant', '/admin/ai/assistant',
    rag.pid,                 -- RAG 문서함과 같은 부모(System 묶음)
    'MENU',
    '업무 비서', 'lucide:bot-message-square',
    rag.order_no + 1,        -- RAG 문서함 바로 다음 자리
    false, 1, now(), 'ai-assistant-menu', false,
    true, false, false, false, false, false, false,
    true, true,
    'admin.ai.assistant'
  FROM scom.system_menus rag
 WHERE rag.id = 'SYS_RAG_DOCS'
   AND NOT EXISTS (
       SELECT 1 FROM scom.system_menus x WHERE x.id = 'SYS_AI_ASSISTANT'
   );

COMMIT;

-- ── 확인 ─────────────────────────────────────────────────────
-- SELECT id, title, path, route_key, pid, order_no
--   FROM scom.system_menus WHERE id = 'SYS_AI_ASSISTANT';
--
-- 역할을 맨 뒤(메뉴 권한 화면에서 처리한 뒤) 확인:
-- SELECT role_id, can_view
--   FROM scom.role_menus WHERE menu_id = 'SYS_AI_ASSISTANT';
--
-- 되돌리기
--   DELETE FROM scom.menu_favorites WHERE menu_id = 'SYS_AI_ASSISTANT';
--   DELETE FROM scom.role_menus     WHERE menu_id = 'SYS_AI_ASSISTANT';
--   DELETE FROM scom.system_menus   WHERE id      = 'SYS_AI_ASSISTANT';
