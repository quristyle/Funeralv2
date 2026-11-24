-- ============================================================
-- 사이트 문의내역(/site/inquiries) — 삭제 권한을 연다 (2026-11-24)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Site/Components/Pages/InquiryList.razor
-- API:  DELETE /api/site/admin/inquiries/{id} (논리 삭제, SiteEndpoints.cs)
--
--   psql -h … -U funeralv2 -d jsiniportal -f site-inquiry-delete-permission-2026-11-24.sql
--
-- 삭제 단추는 화면·API 에 이미 있으나 메뉴의 use_delete 가 꺼져 있어
-- 아무에게도 그려지지 않았다. 권한은 메뉴(use_delete)와 역할(can_delete)이
-- 둘 다 참이어야 선다(AuthServer MenuService). 역할은 서버가 삭제를 허락하는
-- 둘(SYSTEM_ADMINISTRATOR · ADMINISTRATOR)에만 준다. 두 번 돌려도 같다.

BEGIN;

UPDATE scom.system_menus
   SET use_delete = true, updated_at = now(), updated_by = 'site-inquiry-delete'
 WHERE id = 'SITE_INQUIRY' AND use_delete = false;

UPDATE scom.role_menus
   SET can_delete = true, updated_at = now(), updated_by = 'site-inquiry-delete'
 WHERE menu_id = 'SITE_INQUIRY'
   AND role_id IN ('SYSTEM_ADMINISTRATOR', 'ADMINISTRATOR')
   AND can_delete = false;

COMMIT;
