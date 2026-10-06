-- ============================================================
-- 시험용 차량 한 대 (cargotrust DB · cargotrust 스키마)
-- ============================================================
--
-- 보통은 **화면으로 넣는다** — 포털관리 › 차량 관리(`/admin/system/vehicle`)에서
-- 계정을 고르고 등록하면 끝이고, 운송관리 사용자 줄이 없으면 서버가 만들어 준다.
-- 이 파일은 화면 없이(또는 자동 설치로) 넣어야 할 때의 길이다.
--
-- [로그인 아이디를 알아야 한다]
--
-- 계정 표(scom.accounts)는 **다른 DB**(포털)에 있어 여기서 조인할 수 없다.
-- 그래서 아이디를 literal 로 적는다. 찾는 법:
--
--   포털 DB 에서
--     SELECT user_id, real_name FROM scom.accounts WHERE real_name = '이진문';
--
--   또는 포털관리 › 계정 관리 화면에서 「로그인 아이디」 칸을 본다.
--
-- 그 값이 게이트웨이가 각 서비스에 보내는 X-User-Id 이고,
-- 운송관리의 app_user.external_user_id 와 같은 값이다.
--
-- **로그인 아이디와 차량번호를 채워서 돌린다.** 둘 다 사람을 가리키는 값이라
-- 저장소에 적어 두지 않는다. 멱등하다 — 여러 번 돌려도 차량이 늘지 않는다.

SET search_path TO cargotrust;

DO $$
DECLARE
    -- ── 여기만 고친다 ──────────────────────────────────────
    v_login_id   TEXT := 'CHANGE_ME';  -- 포털 로그인 아이디 (위 「찾는 법」)
    v_user_name  TEXT := 'CHANGE_ME';  -- 계정 이름
    v_plate_no   TEXT := 'CHANGE_ME';  -- 차량번호
    v_nickname   TEXT := '윙바디';
    v_class      TEXT := 'C4';          -- LIGHT · C1 ~ C5
    v_axles      SMALLINT := 3;
    v_tonnage    NUMERIC(6,2) := 11;
    -- ───────────────────────────────────────────────────────
    v_user_id    BIGINT;
    v_vehicle_id BIGINT;
BEGIN
    IF 'CHANGE_ME' IN (v_login_id, v_user_name, v_plate_no) THEN
        RAISE EXCEPTION '로그인 아이디 · 이름 · 차량번호를 먼저 채운다 (머리말 참고).';
    END IF;

    -- 운송관리 사용자 줄. 그 사람이 운송관리를 한 번도 안 열었으면 없다.
    SELECT user_id INTO v_user_id FROM app_user WHERE external_user_id = v_login_id;

    IF v_user_id IS NULL THEN
        INSERT INTO app_user (external_user_id, display_name, user_type, status)
        VALUES (v_login_id, v_user_name, 'DRIVER', 'ACTIVE')
        RETURNING user_id INTO v_user_id;
        RAISE NOTICE '운송관리 사용자 줄을 만들었다: % → %', v_login_id, v_user_id;
    END IF;

    -- 같은 사람의 같은 번호판은 한 줄이다(uq_vehicle_plate).
    SELECT vehicle_id INTO v_vehicle_id
      FROM vehicle
     WHERE user_id = v_user_id AND plate_no = v_plate_no AND is_deleted = false;

    IF v_vehicle_id IS NULL THEN
        INSERT INTO vehicle (
            user_id, plate_no, nickname, vehicle_class, axle_count, tonnage,
            is_business, has_hipass, is_default, memo)
        VALUES (
            v_user_id, v_plate_no, v_nickname, v_class, v_axles, v_tonnage,
            true, true, true, '시험용으로 넣은 줄')
        RETURNING vehicle_id INTO v_vehicle_id;

        -- 기본 차량은 한 사람에 한 대. 이 줄을 기본으로 올리고 나머지를 내린다.
        UPDATE vehicle SET is_default = false
         WHERE user_id = v_user_id AND vehicle_id <> v_vehicle_id;

        RAISE NOTICE '차량을 등록했다: % (%)', v_plate_no, v_vehicle_id;
    ELSE
        RAISE NOTICE '이미 있는 차량이다: % (%)', v_plate_no, v_vehicle_id;
    END IF;
END $$;

-- 확인
--
--   SELECT v.vehicle_id, u.external_user_id, u.display_name,
--          v.plate_no, v.vehicle_class, v.axle_count, v.is_business, v.is_default
--     FROM vehicle v JOIN app_user u ON u.user_id = v.user_id
--    WHERE v.is_deleted = false
--    ORDER BY u.external_user_id, v.vehicle_id;
--
-- 되돌리기 (표시만 지운다 — 계산 이력이 이 줄을 가리킨다)
--
--   UPDATE vehicle SET is_deleted = true, is_default = false
--    WHERE plate_no = '<위에 적은 차량번호>';
