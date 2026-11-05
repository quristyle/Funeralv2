-- 일정표(/projmng/proj/scheduler) HHIP_MM(prj_rid 8) 에서 제목 앞머리 [xxxx] 와 날짜가 같은
-- 「…운영반영」·「…개발완료예정일」 줄을 하나로 합치고, 남긴 줄의 끝말을 「개발완료,운영반영」으로 바꾼다.
-- 반영 전 26 쌍(52 줄) — 쌍마다 wbs_id 가 작은 줄(전부 「운영반영」 쪽, 724~762)을 남기고 813~883 25 줄을 지웠다.
-- [3320] 은 돌리기 직전에 화면에서 725(운영반영)가 먼저 지워져 짝이 없었다 — 남은 814 의 끝말만 손으로 바꿨다.
-- 「운영반영」 쪽을 남기는 까닭: [TBD2550] 처럼 제목이 더 온전하다(「(3일 이상)」이 다른 쪽에선 빠져 있다).
-- 지운 줄은 projmng.dev_wbs_dedup_bak_20261106 에 그대로 있다 — 되돌리기는 그 표를 dev_wbs 에 다시 넣고
-- 남긴 줄의 끝말을 regexp_replace(proc_nm, '개발완료,운영반영(\s*)$', '운영반영\1') 로 돌린다.
BEGIN;

CREATE TEMP TABLE dedup ON COMMIT DROP AS
SELECT wbs_id,
       row_number() OVER (PARTITION BY substring(proc_nm FROM '^\[[^]]*\]'),
                                       (plan_sdt AT TIME ZONE 'Asia/Seoul')::date
                          ORDER BY wbs_id) AS rn,
       count(*)     OVER (PARTITION BY substring(proc_nm FROM '^\[[^]]*\]'),
                                       (plan_sdt AT TIME ZONE 'Asia/Seoul')::date) AS cnt
  FROM projmng.dev_wbs
 WHERE prj_rid = 8
   AND proc_nm ~ '^\[[^]]*\]'
   AND proc_nm ~ '(운영반영|개발완료예정일)\s*$';

CREATE TABLE IF NOT EXISTS projmng.dev_wbs_dedup_bak_20261106 AS
SELECT * FROM projmng.dev_wbs WHERE false;

INSERT INTO projmng.dev_wbs_dedup_bak_20261106
SELECT w.* FROM projmng.dev_wbs w JOIN dedup d USING (wbs_id) WHERE d.cnt > 1 AND d.rn > 1;

DELETE FROM projmng.dev_wbs w USING dedup d
 WHERE w.wbs_id = d.wbs_id AND d.cnt > 1 AND d.rn > 1;

UPDATE projmng.dev_wbs w
   SET proc_nm = regexp_replace(w.proc_nm, '(운영반영|개발완료예정일)(\s*)$', '개발완료,운영반영\2'),
       mod_dt  = projmng.today_kst()
  FROM dedup d
 WHERE w.wbs_id = d.wbs_id AND d.cnt > 1 AND d.rn = 1;

COMMIT;
