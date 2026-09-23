-- ============================================================
-- WBS 대시보드 업무자료 적재 — 사내 DB 덤프를 projmng 으로 옮긴다
-- ============================================================
--
-- `projmng-wbs-2026-09-23.sql` 이 만든 빈 표에 **실제 자료**를 넣는 스크립트다.
-- 자료 자체는 이 저장소에 없다 — 사내망 DB 에서 따로 뽑아 와야 한다.
--
-- ── 쓰는 법 ─────────────────────────────────────────────────
--
-- ① 사내 DB 에서 표를 뽑는다. **원래 이름 그대로** 뽑아야 한다.
--
--    pg_dump -h <사내DB> -U postgres -d postgres \
--            --data-only --column-inserts --no-owner \
--            -t public.dev_user -t public.dev_docs -t public.dev_user_pref \
--            -t public.hhip_wbs_wrk2 -t public.hhip_wbs_task \
--            -t public.hhip_wbs_pv -t public.hhip_wbs_pv_task -t public.hhip_wbs_pv_node \
--            -t public.if_system -t public.if_attr_def \
--            -t public.if_master -t public.if_step -t public.if_note -t public.if_attr \
--            > wbs_data.sql
--
-- ② 받는 쪽에 **받침 스키마**를 만들고 거기에 푼다. 원본 표를 projmng 에
--    바로 풀면 이름이 부딪히고, 무엇보다 **실수를 되돌릴 자리가 없어진다.**
--
--    psql -d projmng -c 'CREATE SCHEMA IF NOT EXISTS wbs_import'
--    psql -d projmng -c 'SET search_path = wbs_import' -f 사내스키마.sql   -- 표 만들기
--    psql -d projmng -c 'SET search_path = wbs_import' -f wbs_data.sql      -- 자료 넣기
--
-- ③ 이 스크립트를 돌린다. `prj` 는 `projmng.dev_proj.prj_rid` 다.
--
--    psql -d projmng -v prj=8 -f projmng-wbs-data-load.sql
--
-- ④ 맨 아래가 찍어 주는 건수를 보고 맞으면 받침 스키마를 지운다.
--
--    psql -d projmng -c 'DROP SCHEMA wbs_import CASCADE'
--
-- ── 번호를 그대로 가져오지 않는다 ───────────────────────────
--
-- `if_system`·`if_attr_def`·`if_master`·`if_step` 의 번호는 **줄번호일 뿐**이고,
-- 받는 쪽에는 이미 다른 번호가 쓰이고 있을 수 있다. 실제로 그렇다 — 시험하다
-- 앞 번호를 써 버려서 사내 `1·2·3` 이 여기서는 `3·4·5` 다.
--
-- 그대로 옮기면 **항목값이 엉뚱한 항목에 붙고 조용히 틀린다.** 그래서 뜻 있는
-- 열쇠로 잇고(`system_cd`·`attr_cd`), 새로 번호를 받는 표는 옮기는 동안
-- 짝을 적어 둔다(`map_*`).
--
-- ── 여러 번 돌려도 안전하다 ─────────────────────────────────
--
-- 전부 `on conflict do nothing` 이다. 다만 **고친 값을 덮지는 않는다** —
-- 이미 들어간 줄은 그대로 둔다. 다시 채우려면 그 프로젝트 자료를 지우고 돌린다.

\set ON_ERROR_STOP on

BEGIN;

-- ── 들어오기 전 실태 ────────────────────────────────────────
--
-- 옮기고 나서 「몇 건이어야 했더라」를 뒤지지 않도록 먼저 찍어 둔다.
\echo '── 받침 스키마에 들어온 것 ──'
SELECT 'dev_user'        AS 표, count(*) AS 건수 FROM wbs_import.dev_user
UNION ALL SELECT 'hhip_wbs_wrk2',   count(*) FROM wbs_import.hhip_wbs_wrk2
UNION ALL SELECT 'hhip_wbs_task',   count(*) FROM wbs_import.hhip_wbs_task
UNION ALL SELECT 'if_master',       count(*) FROM wbs_import.if_master
ORDER BY 1;


-- ════════════════════════════════════════════════════════════
--  개발자 명부
-- ════════════════════════════════════════════════════════════
--
-- **개인정보가 들어 있다** — 성명 · 전화 · 비상연락처 · 생년월일 · MAC ·
-- 장비번호. 옮기기 전에 한 번 보라고 원본 꾸러미가 경고해 두었다.
--
-- 사번이 비었거나 겹치는 줄은 뺀다. 사내 표에는 기본키가 없어서 그런 줄이
-- 실제로 있을 수 있고, 여기서는 그것이 열쇠다.
INSERT INTO projmng.wbs_user (
    prj_rid, bp_id, name, email, position_nm, tel_no, emerg_tel_no, birth_dt,
    git, startkit, dxb, vm_conn, aipro, claudecode, dev_db, wiki, projectview, svn,
    pv_user_id, notebook, hub_hdmi, summer_size, winter_size,
    use_ip, mac_addr, notebook_no, notebook_chk_no,
    monitor1_no, monitor1_chk_no, monitor2_no, monitor2_chk_no,
    monitor3_no, monitor3_chk_no, block_yn, super_yn)
SELECT DISTINCT ON (btrim(u.bp_id))
       :prj, btrim(u.bp_id), u.name, u.email, u.position_nm, u.tel_no, u.emerg_tel_no, u.birth_dt,
       u.git, u.startkit, u.dxb, u.vm_conn, u.aipro, u.claudecode, u.dev_db, u.wiki,
       u.projectview, u.svn,
       u.pv_user_id, u.notebook, u.hub_hdmi, u.summer_size, u.winter_size,
       u.use_ip, u.mac_addr, u.notebook_no, u.notebook_chk_no,
       u.monitor1_no, u.monitor1_chk_no, u.monitor2_no, u.monitor2_chk_no,
       u.monitor3_no, u.monitor3_chk_no, u.block_yn, u.super_yn
  FROM wbs_import.dev_user u
 WHERE u.bp_id IS NOT NULL AND btrim(u.bp_id) <> ''
 ORDER BY btrim(u.bp_id), u.name NULLS LAST
ON CONFLICT (prj_rid, bp_id) DO NOTHING;

-- **포털 계정은 손으로 잇는다.** 사번과 계정을 맞출 근거가 자료에 없다 —
-- 짐작으로 이으면 남의 얼굴이 남의 일감에 붙는다.
--   UPDATE projmng.wbs_user SET login_id = '<포털계정>'
--    WHERE prj_rid = :prj AND bp_id = '<사번>';


-- ════════════════════════════════════════════════════════════
--  원장과 일감
-- ════════════════════════════════════════════════════════════
--
-- `activity_id` 가 비면 안 들어간다 — 여기서는 그것이 열쇠다. 사내 표에는
-- 기본키가 없어 겹치는 줄도 있을 수 있어서 하나만 남긴다. **몇 줄이
-- 빠졌는지는 맨 아래가 찍어 준다.**
INSERT INTO projmng.wbs_work (
    prj_rid, activity_id, module, module_name, systemcode, system_nm, program_id,
    menu_nm, comment, plan_sdt, plan_edt, priority_order, plan_sdt_c, plan_edt_c,
    prog_type, prog_type_desc, trg_exsit_chk, trg_use_chk, trg_evt_rel, etc_desc,
    new_dev2, asis_cs, report_use, sheet_use, dwg_view_use, por_view_use, menu_desc,
    mon_req_comp, user_bp_id, complate_yn, comp_desc, user_real_id, complate_real_yn,
    recheck_yn, complate_big_yn, complate_real_big_yn, recheck_big_yn, db_ready_big_yn)
SELECT DISTINCT ON (btrim(w.activity_id))
       :prj, btrim(w.activity_id), w.module, w.module_name, w.systemcode, w.system_nm,
       w.program_id, w.menu_nm, w.comment, w.plan_sdt, w.plan_edt, w.priority_order,
       w.plan_sdt_c, w.plan_edt_c, w.prog_type, w.prog_type_desc, w.trg_exsit_chk,
       w.trg_use_chk, w.trg_evt_rel, w.etc_desc, w.new_dev2, w.asis_cs, w.report_use,
       w.sheet_use, w.dwg_view_use, w.por_view_use, w.menu_desc, w.mon_req_comp,
       w.user_bp_id, w.complate_yn, w.comp_desc, w.user_real_id, w.complate_real_yn,
       w.recheck_yn, w.complate_big_yn, w.complate_real_big_yn, w.recheck_big_yn,
       w.db_ready_big_yn
  FROM wbs_import.hhip_wbs_wrk2 w
 WHERE w.activity_id IS NOT NULL AND btrim(w.activity_id) <> ''
 -- 겹치면 **내용이 많은 쪽**을 남긴다. 빈 줄이 이기면 채워 둔 것이 사라진다.
 ORDER BY btrim(w.activity_id),
          (w.plan_edt IS NOT NULL) DESC,
          (w.user_bp_id IS NOT NULL) DESC,
          w.menu_nm NULLS LAST
ON CONFLICT (prj_rid, activity_id) DO NOTHING;

-- 일감. 부모가 없는 줄은 외래키가 막으므로 미리 걸러 낸다 — 막히고 나서
-- 전체가 되돌아가면 어느 줄 때문인지 찾기가 성가시다.
INSERT INTO projmng.wbs_task
    (task_id, prj_rid, activity_id, task_div, memo, done_yn, sort_order, created_at, updated_at)
SELECT t.task_id, :prj, btrim(t.activity_id), t.task_div, t.memo, t.done_yn,
       t.sort_order, t.created_at, t.updated_at
  FROM wbs_import.hhip_wbs_task t
 WHERE EXISTS (SELECT 1 FROM projmng.wbs_work w
                WHERE w.prj_rid = :prj AND w.activity_id = btrim(t.activity_id))
ON CONFLICT (task_id) DO NOTHING;

-- 번호를 그대로 넣었으므로 다음 번호를 그 뒤로 민다. 안 하면 **다음 등록이
-- 곧바로 기본키 충돌**로 죽는다.
SELECT setval(pg_get_serial_sequence('projmng.wbs_task', 'task_id'),
              GREATEST((SELECT COALESCE(max(task_id), 0) FROM projmng.wbs_task), 1));


-- ════════════════════════════════════════════════════════════
--  공유 문서 · 화면 설정
-- ════════════════════════════════════════════════════════════

INSERT INTO projmng.wbs_docs (prj_rid, title, content, sort_order, updated_by, updated_at)
SELECT :prj, d.title, d.content, d.sort_order, d.updated_by, d.updated_at
  FROM wbs_import.dev_docs d
 WHERE NOT EXISTS (SELECT 1 FROM projmng.wbs_docs x
                    WHERE x.prj_rid = :prj AND x.title = d.title);

INSERT INTO projmng.wbs_user_pref (prj_rid, bp_id, pref_key, pref_val, updated_at)
SELECT :prj, p.bp_id, p.pref_key, p.pref_val, p.updated_at
  FROM wbs_import.dev_user_pref p
ON CONFLICT (prj_rid, bp_id, pref_key) DO NOTHING;


-- ════════════════════════════════════════════════════════════
--  ProjectView 캐시
-- ════════════════════════════════════════════════════════════
--
-- **안 옮겨도 된다.** 캐시라 비워 두면 [ProjectView 동기화] 화면에서 다시
-- 걷으면 그만이다. 옮기면 그 한 번을 아낀다.

INSERT INTO projmng.wbs_pv (
    prj_rid, activity_id, pv_project_id, pv_work_id, pv_work_title, pv_seen_at,
    pv_finish_rate, pv_actual_rate, pv_plan_sdt, pv_plan_edt,
    pv_actual_sdt, pv_actual_edt, pv_snapshot_at, updated_at)
SELECT :prj, p.activity_id, p.pv_project_id, p.pv_work_id, p.pv_work_title, p.pv_seen_at,
       p.pv_finish_rate, p.pv_actual_rate, p.pv_plan_sdt, p.pv_plan_edt,
       p.pv_actual_sdt, p.pv_actual_edt, p.pv_snapshot_at, p.updated_at
  FROM wbs_import.hhip_wbs_pv p
ON CONFLICT (prj_rid, activity_id) DO NOTHING;

INSERT INTO projmng.wbs_pv_task (
    prj_rid, pv_task_id, activity_id, pv_work_id, pv_task_code, pv_task_title,
    pv_plan_sdt, pv_plan_edt, pv_node_cnt, pv_node_empty, pv_seen_at, updated_at,
    pv_charger_id, pv_charger_nm, pv_status, pv_status_at)
SELECT :prj, t.pv_task_id, t.activity_id, t.pv_work_id, t.pv_task_code, t.pv_task_title,
       t.pv_plan_sdt, t.pv_plan_edt, t.pv_node_cnt, t.pv_node_empty, t.pv_seen_at,
       t.updated_at, t.pv_charger_id, t.pv_charger_nm, t.pv_status, t.pv_status_at
  FROM wbs_import.hhip_wbs_pv_task t
ON CONFLICT (prj_rid, pv_task_id) DO NOTHING;

-- 단계는 일감에 매달린다. 부모 없는 줄은 외래키가 막으므로 걸러 낸다.
INSERT INTO projmng.wbs_pv_node
    (prj_rid, pv_task_id, node_no, node_id, stage_nm, node_dt, worker_id, worker_nm, updated_at)
SELECT :prj, n.pv_task_id, n.node_no, n.node_id, n.stage_nm, n.node_dt,
       n.worker_id, n.worker_nm, n.updated_at
  FROM wbs_import.hhip_wbs_pv_node n
 WHERE EXISTS (SELECT 1 FROM projmng.wbs_pv_task t
                WHERE t.prj_rid = :prj AND t.pv_task_id = n.pv_task_id)
ON CONFLICT (prj_rid, pv_task_id, node_no) DO NOTHING;


-- ════════════════════════════════════════════════════════════
--  인터페이스 카탈로그
-- ════════════════════════════════════════════════════════════
--
-- 여기서만 **번호를 다시 맺는다**(머리말). 새 번호는 DB 가 주고, 옮기는
-- 동안 사내 번호와의 짝을 임시 표에 적어 둔다.

-- ── 연계 시스템 ─────────────────────────────────────────────
INSERT INTO projmng.if_system (
    prj_rid, system_cd, system_nm, system_kind, host, port, db_nm, schema_nm,
    system_desc, ext, sort_order, use_yn, created_by, updated_by)
SELECT :prj, s.system_cd, s.system_nm, s.system_kind, s.host, s.port, s.db_nm,
       s.schema_nm, s.system_desc, s.ext, s.sort_order, s.use_yn, s.created_by, s.updated_by
  FROM wbs_import.if_system s
ON CONFLICT (prj_rid, system_cd) DO NOTHING;

CREATE TEMP TABLE map_system ON COMMIT DROP AS
SELECT s.system_id AS old_id, t.system_id AS new_id
  FROM wbs_import.if_system s
  JOIN projmng.if_system t ON t.prj_rid = :prj AND t.system_cd = s.system_cd;

-- ── 추가 관리항목 정의 ──────────────────────────────────────
INSERT INTO projmng.if_attr_def (
    prj_rid, attr_cd, attr_nm, attr_type, code_grp, required_yn, default_val,
    attr_desc, sort_order, use_yn)
SELECT :prj, d.attr_cd, d.attr_nm, d.attr_type, d.code_grp, d.required_yn,
       d.default_val, d.attr_desc, d.sort_order, d.use_yn
  FROM wbs_import.if_attr_def d
ON CONFLICT (prj_rid, attr_cd) DO NOTHING;

CREATE TEMP TABLE map_attr_def ON COMMIT DROP AS
SELECT d.attr_def_id AS old_id, t.attr_def_id AS new_id
  FROM wbs_import.if_attr_def d
  JOIN projmng.if_attr_def t ON t.prj_rid = :prj AND t.attr_cd = d.attr_cd;

-- ── 인터페이스 기본정보 ─────────────────────────────────────
--
-- 시스템 번호를 여기서 갈아 끼운다. 못 찾으면 비운다 — 틀린 시스템을
-- 가리키는 것보다 비어 있는 편이 낫다.
INSERT INTO projmng.if_master (
    prj_rid, if_cd, if_nm, if_desc, direction_cd, src_system_id, tgt_system_id,
    domain_cd, trigger_cd, cycle_cd, status_cd, owner_nm, owner_bp_id,
    plan_sdt, plan_edt, open_dt, remark, ext, sort_order, use_yn,
    created_by, updated_by)
SELECT :prj, m.if_cd, m.if_nm, m.if_desc, m.direction_cd,
       ms.new_id, mt.new_id,
       m.domain_cd, m.trigger_cd, m.cycle_cd, m.status_cd, m.owner_nm, m.owner_bp_id,
       m.plan_sdt, m.plan_edt, m.open_dt, m.remark, m.ext, m.sort_order, m.use_yn,
       m.created_by, m.updated_by
  FROM wbs_import.if_master m
  LEFT JOIN map_system ms ON ms.old_id = m.src_system_id
  LEFT JOIN map_system mt ON mt.old_id = m.tgt_system_id
ON CONFLICT (prj_rid, if_cd) DO NOTHING;

CREATE TEMP TABLE map_master ON COMMIT DROP AS
SELECT m.if_id AS old_id, t.if_id AS new_id
  FROM wbs_import.if_master m
  JOIN projmng.if_master t ON t.prj_rid = :prj AND t.if_cd = m.if_cd;

-- ── 처리 단계 ───────────────────────────────────────────────
INSERT INTO projmng.if_step (
    if_id, step_no, step_nm, step_type_cd, system_id, object_owner, object_nm,
    object_type, step_desc, params, ext, use_yn, created_by, updated_by)
SELECT mm.new_id, s.step_no, s.step_nm, s.step_type_cd, ms.new_id,
       s.object_owner, s.object_nm, s.object_type, s.step_desc, s.params, s.ext,
       s.use_yn, s.created_by, s.updated_by
  FROM wbs_import.if_step s
  JOIN map_master mm ON mm.old_id = s.if_id
  LEFT JOIN map_system ms ON ms.old_id = s.system_id
ON CONFLICT (if_id, step_no) DO NOTHING;

CREATE TEMP TABLE map_step ON COMMIT DROP AS
SELECT s.step_id AS old_id, t.step_id AS new_id
  FROM wbs_import.if_step s
  JOIN map_master mm ON mm.old_id = s.if_id
  JOIN projmng.if_step t ON t.if_id = mm.new_id AND t.step_no = s.step_no;

-- ── 메모 · 이슈 ─────────────────────────────────────────────
--
-- 같은 인터페이스에 같은 제목·같은 날짜가 둘 있을 수 있어 유일 제약이 없다.
-- 그래서 겹침을 `not exists` 로 본다 — 두 번 돌려도 두 벌이 되지 않게.
INSERT INTO projmng.if_note (
    if_id, step_id, note_type_cd, title, content, writer_nm, note_dt,
    done_yn, sort_order, ext, created_by, updated_by)
SELECT mm.new_id, mst.new_id, n.note_type_cd, n.title, n.content, n.writer_nm,
       n.note_dt, n.done_yn, n.sort_order, n.ext, n.created_by, n.updated_by
  FROM wbs_import.if_note n
  JOIN map_master mm ON mm.old_id = n.if_id
  LEFT JOIN map_step mst ON mst.old_id = n.step_id
 WHERE NOT EXISTS (
     SELECT 1 FROM projmng.if_note x
      WHERE x.if_id = mm.new_id
        AND x.note_dt = n.note_dt
        AND x.title IS NOT DISTINCT FROM n.title);

-- ── 추가 관리항목 값 ────────────────────────────────────────
--
-- **이 줄이 번호를 잘못 이으면 조용히 틀린다** — 값이 남의 항목에 붙고
-- 화면에는 멀쩡해 보인다. 그래서 위에서 뜻 있는 열쇠로 짝을 지어 두었다.
INSERT INTO projmng.if_attr (if_id, attr_def_id, attr_val, created_by, updated_by)
SELECT mm.new_id, md.new_id, a.attr_val, a.created_by, a.updated_by
  FROM wbs_import.if_attr a
  JOIN map_master mm ON mm.old_id = a.if_id
  JOIN map_attr_def md ON md.old_id = a.attr_def_id
ON CONFLICT (if_id, attr_def_id) DO NOTHING;


-- ════════════════════════════════════════════════════════════
--  들어온 것과 빠진 것
-- ════════════════════════════════════════════════════════════
--
-- **빠진 건수를 반드시 본다.** 조용히 줄어든 자료가 가장 찾기 어렵다.
\echo ''
\echo '── 옮긴 결과 ──'
SELECT '원장'          AS 표,
       (SELECT count(*) FROM wbs_import.hhip_wbs_wrk2)                       AS 원본,
       (SELECT count(*) FROM projmng.wbs_work WHERE prj_rid = :prj)          AS 옮김,
       (SELECT count(*) FROM wbs_import.hhip_wbs_wrk2
         WHERE activity_id IS NULL OR btrim(activity_id) = '')               AS "번호없어뺌"
UNION ALL
SELECT '일감',
       (SELECT count(*) FROM wbs_import.hhip_wbs_task),
       (SELECT count(*) FROM projmng.wbs_task WHERE prj_rid = :prj),
       (SELECT count(*) FROM wbs_import.hhip_wbs_task t
         WHERE NOT EXISTS (SELECT 1 FROM projmng.wbs_work w
                            WHERE w.prj_rid = :prj AND w.activity_id = btrim(t.activity_id)))
UNION ALL
SELECT '개발자',
       (SELECT count(*) FROM wbs_import.dev_user),
       (SELECT count(*) FROM projmng.wbs_user WHERE prj_rid = :prj),
       (SELECT count(*) FROM wbs_import.dev_user
         WHERE bp_id IS NULL OR btrim(bp_id) = '')
UNION ALL
SELECT '인터페이스',
       (SELECT count(*) FROM wbs_import.if_master),
       (SELECT count(*) FROM projmng.if_master WHERE prj_rid = :prj), 0
UNION ALL
SELECT '처리단계',
       (SELECT count(*) FROM wbs_import.if_step),
       (SELECT count(*) FROM projmng.if_step s
          JOIN projmng.if_master m ON m.if_id = s.if_id WHERE m.prj_rid = :prj), 0
UNION ALL
SELECT '메모',
       (SELECT count(*) FROM wbs_import.if_note),
       (SELECT count(*) FROM projmng.if_note n
          JOIN projmng.if_master m ON m.if_id = n.if_id WHERE m.prj_rid = :prj), 0
UNION ALL
SELECT '항목값',
       (SELECT count(*) FROM wbs_import.if_attr),
       (SELECT count(*) FROM projmng.if_attr a
          JOIN projmng.if_master m ON m.if_id = a.if_id WHERE m.prj_rid = :prj), 0
UNION ALL
SELECT 'PV 캐시',
       (SELECT count(*) FROM wbs_import.hhip_wbs_pv),
       (SELECT count(*) FROM projmng.wbs_pv WHERE prj_rid = :prj), 0
ORDER BY 1;

\echo ''
\echo '원장의 「옮김」이 「원본 - 번호없어뺌」보다 적으면 activity_id 가 겹친 줄이 있다는 뜻이다.'
\echo '겹친 줄 보기:'
\echo '  SELECT activity_id, count(*) FROM wbs_import.hhip_wbs_wrk2'
\echo '   GROUP BY activity_id HAVING count(*) > 1;'

COMMIT;

-- 끝난 뒤 받침 스키마를 지운다.
--   DROP SCHEMA wbs_import CASCADE;
