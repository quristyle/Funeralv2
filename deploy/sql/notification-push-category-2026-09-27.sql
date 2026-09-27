-- 알림에 **구분**을 붙인다 (2026-09-27).
--
-- 1) scom.push_send_logs 에 category 칸 — 보낼 때 함께 보관한다.
-- 2) 공통코드 묶음 NOTI_CATEGORY 와 그 코드들 — 구분의 정본이 여기다.
-- 3) 메타데이터(scom.biz_select_configs) 한 줄 — 범용 셀렉트가 이 묶음을 읽는다.
--
-- [왜 칸을 따로 두나]
--
-- 갈래를 제목으로 되짚고 있으면 문구를 한 번 다듬는 순간 옛 줄과 새 줄이
-- 갈라진다. **보낸 쪽이 자기 갈래를 적어 두는 것**이 조회의 유일한 단단한
-- 근거다.
--
-- [옛 줄은 비어 있다]
--
-- 구분을 붙이기 전에 쌓인 줄에는 어디서 보냈는지가 아무 데도 안 남아 있어
-- 채워 넣을 길이 없다. NULL 로 두고 화면에서 「구분 없음」으로 모아 본다 —
-- 그 자리가 곧 「어느 발송 자리가 구분을 빠뜨렸나」를 찾는 자리다.
--
-- NotificationServer 에는 전용 EF Migration 을 두지 않는다. 기존 scom 표를
-- 손대는 운영 SQL 이며 **여러 번 실행해도 안전해야 한다.**

BEGIN;

-- ── 1. 발송 기록의 구분 칸 ────────────────────────────────

ALTER TABLE scom.push_send_logs
    ADD COLUMN IF NOT EXISTS category text;

COMMENT ON COLUMN scom.push_send_logs.category IS
    '알림구분. 공통코드 묶음 NOTI_CATEGORY 의 코드값. 옛 줄과 구분 없이 보낸 것은 NULL.';

-- 구분으로 거르는 조회는 **언제나 기간과 함께** 온다(알림함·발송 이력의
-- 조건줄이 그렇다). 그래서 구분만 담지 않고 보낸 시각을 함께 담는다.
CREATE INDEX IF NOT EXISTS ix_push_send_logs_category_sent_at
    ON scom.push_send_logs (category, sent_at DESC);

-- ── 2. 공통코드 묶음과 코드 ───────────────────────────────
--
-- 값은 서비스 코드의 JSini.Shared.DTOs.PushCategories 상수와 **글자까지
-- 같아야 한다** — 보내는 쪽이 그 상수를 싣고, 화면은 이 표의 이름을 그린다.
-- 여기 없는 값으로 보내도 저장은 되지만 화면에는 코드값 그대로 보인다.
--
-- 다시 돌려도 안전하게 group_code 로 있는지 보고 넣는다(이 표에는 유일
-- 제약이 없어 ON CONFLICT 를 걸 곳이 없다).

INSERT INTO scom.common_code_groups
    (id, group_code, group_name, is_hierarchical, sort_order, remark,
     created_at, created_by, is_deleted)
SELECT gen_random_uuid()::text, 'NOTI_CATEGORY', '알림구분', false, 0,
       '앱 푸시·알림함의 갈래. 보낼 때 발송 기록에 함께 보관한다.',
       now(), 'system', false
WHERE NOT EXISTS (
    SELECT 1 FROM scom.common_code_groups
     WHERE group_code = 'NOTI_CATEGORY' AND is_deleted = false
);

INSERT INTO scom.common_codes
    (id, group_id, parent_id, code_value, code_name, sort_order, level, is_leaf,
     status, remark, created_at, created_by, is_deleted)
SELECT gen_random_uuid()::text, g.id, NULL, v.code_value, v.code_name,
       v.sort_order, 1, true, 1, v.remark, now(), 'system', false
  FROM scom.common_code_groups g
  JOIN (VALUES
        ('NOTICE',       '공지·안내',    10, '사람이 손으로 보내는 알림(포털관리 「메시지 발송」 따위)'),
        ('DEPLOY',       '배포',         20, '배포 성공·실패 알림'),
        ('HELPDESK',     '헬프데스크',   30, '헬프데스크 요청 알림'),
        ('AI_TASK',      'AI 작업',      40, 'AI 작업 요청·남긴말·결과 알림'),
        ('NOTE',         '쪽지',         50, '쪽지가 왔다는 알림'),
        ('WEATHER',      '기상',         60, '기상 특보·기준 충족·내 위치 날씨'),
        ('BIRTHDAY',     '생일',         70, '생일 축하 알림'),
        ('SIGNUP',       '가입신청',     80, '가입 신청이 들어왔다는 알림'),
        ('SUBSCRIPTION', '알림구독',     90, '새 기기가 알림을 구독했다는 알림'),
        ('TEST',         '시험발송',    100, '설정 화면의 「시험 발송」')
       ) AS v(code_value, code_name, sort_order, remark)
    ON true
 WHERE g.group_code = 'NOTI_CATEGORY'
   AND g.is_deleted = false
   AND NOT EXISTS (
        SELECT 1 FROM scom.common_codes c
         WHERE c.group_id = g.id
           AND c.code_value = v.code_value
           AND c.is_deleted = false
   );

-- ── 3. 범용 셀렉트 메타데이터 ─────────────────────────────
--
-- 「메타데이터 관리」(/admin/system/metadata)에 한 줄을 둔다. 포털관리 화면
-- 셋은 자기 클라이언트로 공통코드를 곧장 읽지만(모듈끼리 부품을 나눠 쓰지
-- 못한다 — web/CLAUDE.md 의 의존 규칙), **다른 업무 모듈이 이 구분을 쓸 때는
-- 코드를 고치지 않고 BizSelect 로 붙일 수 있어야 한다.**

INSERT INTO scom.biz_select_configs
    (id, biz_type, service_code, api_url, http_method,
     label_field, value_field, result_path, remark,
     created_at, created_by, is_deleted)
SELECT gen_random_uuid()::text, 'NOTI_CATEGORY', 'auth',
       '/system/common-code/NOTI_CATEGORY?hierarchical=false', 'GET',
       'codeName', 'codeValue', 'result',
       '알림구분 드롭다운. 공통코드 NOTI_CATEGORY 를 읽는다.',
       now(), 'system', false
WHERE NOT EXISTS (
    SELECT 1 FROM scom.biz_select_configs
     WHERE biz_type = 'NOTI_CATEGORY' AND is_deleted = false
);

COMMIT;

-- 확인
-- SELECT category, count(*) FROM scom.push_send_logs GROUP BY category ORDER BY 2 DESC;
-- SELECT c.code_value, c.code_name, c.sort_order
--   FROM scom.common_codes c JOIN scom.common_code_groups g ON g.id = c.group_id
--  WHERE g.group_code = 'NOTI_CATEGORY' ORDER BY c.sort_order;
