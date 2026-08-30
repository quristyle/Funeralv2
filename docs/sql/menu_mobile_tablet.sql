-- ============================================================
-- 메뉴 — 화면 크기별 메뉴목록 노출 여부
-- ============================================================
--
-- DB: jsiniportal (스키마 scom)
--
-- 포털은 PWA 라 휴대폰·태블릿에서도 같은 메뉴를 받는다(39·40번 문서).
-- 그런데 데스크톱에서만 쓸모 있는 화면(넓은 그리드·조직도·배치 편집 등)까지
-- 휴대폰 메뉴목록에 그대로 나와 목록이 길어진다.
--
-- 메뉴마다 "이 크기에서 메뉴목록에 보일지"를 정할 수 있게 두 칸을 둔다.
--   use_mobile : 휴대폰 크기(<768px)에서 메뉴목록에 보일지
--   use_tablet : 태블릿 크기(768~1023px)에서 메뉴목록에 보일지
--
-- 데스크톱(>=1024px)은 이 값과 무관하게 항상 보인다.
-- **메뉴목록에서만 빠진다** — 라우트는 그대로 만들어지므로 주소로 직접 들어가거나
-- 즐겨찾기·탭으로 열면 화면은 열린다. status=0(비활성)과는 다른 뜻이다.
--
-- 기존 동작이 바뀌지 않도록 기본값은 둘 다 true 다.
--
-- 반복 실행해도 안전하다.

BEGIN;

ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_mobile boolean NOT NULL DEFAULT true;
ALTER TABLE scom.system_menus ADD COLUMN IF NOT EXISTS use_tablet boolean NOT NULL DEFAULT true;

COMMENT ON COLUMN scom.system_menus.use_mobile IS '휴대폰 크기(<768px) 메뉴목록 노출 여부. false 면 데스크톱에만 보인다(라우트는 유지).';
COMMENT ON COLUMN scom.system_menus.use_tablet IS '태블릿 크기(768~1023px) 메뉴목록 노출 여부. false 면 데스크톱에만 보인다(라우트는 유지).';

COMMIT;

-- 확인
SELECT count(*) AS 전체,
       count(*) FILTER (WHERE use_mobile) AS 휴대폰노출,
       count(*) FILTER (WHERE use_tablet) AS 태블릿노출
FROM scom.system_menus
WHERE is_deleted = false;
