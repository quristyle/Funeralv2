-- ============================================================
-- 프로젝트 참여자 조회 프로시저 (프로젝트관리 3단계)
-- ============================================================
--
-- 배경: [36-projmng-tobe-feature-cleanup.md] 4.3 · 4.4
--   [프로젝트 참여자]와 [프로젝트 진행 현황]이 `sp_dev_user_exec` 를 부르는데,
--   그 프로시저는 `projmng.dev_user`(자체 사용자 테이블)를 읽는다.
--   사용자의 정본은 포털 계정 한 곳이므로 `dev_user` 는 걷어낼 대상이다.
--
--   사람의 **이름**은 포털 계정에서 가져오고, 여기서는
--   **누가 어느 프로젝트에 참여하는가** 만 돌려준다. 그건 업무 자료라 남는다.
--
-- 원본 `sp_dev_user_exec` 가 돌려주던 `inv_cnt`(참여 프로젝트 수)를 그대로 살렸다.
-- 프로젝트로 걸러도 `inv_cnt` 는 **전체 기준**이다 — 원본과 같은 뜻이 되게 했다.
--
-- 기존 프로시저는 손대지 않는다. 이것만 새로 더한다.
-- 반복 실행해도 안전하다(CREATE OR REPLACE).
-- ============================================================

CREATE OR REPLACE PROCEDURE projmng.sp_proj_user_map_list(
  IN    p_prj_rid character varying,
  IN    p_user_id character varying,
  INOUT p_cur     refcursor
)
LANGUAGE plpgsql
AS $procedure$
BEGIN

  OPEN p_cur FOR
  SELECT m.user_id                          -- 포털 로그인 아이디
       , t.inv_cnt                          -- 참여 프로젝트 수 (필터와 무관한 전체 기준)
       , m.prj_rid
       , p.prj_name
    FROM projmng.dev_proj_user_map m
    LEFT JOIN projmng.dev_proj p
      ON p.prj_rid = m.prj_rid
    JOIN ( SELECT user_id, count(*)::int AS inv_cnt
             FROM projmng.dev_proj_user_map
            GROUP BY user_id ) t
      ON t.user_id = m.user_id
   WHERE ( nvl(p_prj_rid, '') = '' OR m.prj_rid = p_prj_rid::int )
     AND ( nvl(p_user_id, '') = '' OR m.user_id = p_user_id )
   ORDER BY m.user_id, m.prj_rid;

END;
$procedure$;

COMMENT ON PROCEDURE projmng.sp_proj_user_map_list(character varying, character varying, refcursor)
  IS '프로젝트 참여자. dev_user 를 읽지 않는다 — 이름은 포털 계정이 댄다.';

-- ── 확인 ─────────────────────────────────────────────────────
-- BEGIN; CALL projmng.sp_proj_user_map_list('', '', 'c1'); FETCH ALL FROM "c1"; ROLLBACK;
