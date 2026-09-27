-- ============================================================
-- 옛 줄에 얼굴 되짚어 넣기 — scom.push_send_logs.icon
-- ============================================================
--
-- 칸을 만들기 전에 보낸 줄에는 아이콘이 없다(notification-push-icon-2026-09-27.sql).
-- 그런데 「내 알림함」에 지금 떠 있는 안 읽은 알림이 그 줄들이라, 채우지
-- 않으면 **사람이 보는 목록이 통째로 앱 아이콘**이다.
--
-- ── 되짚는 실마리는 주소다 ───────────────────────────────────
--
-- 아이콘의 주인(iconOwnerKey)은 안 남았지만, 갈래 둘은 **주소가 곧 사람을
-- 가리킨다.**
--
--   AI_TASK        /projmng/ai/task/{번호}  또는  /projmng/ai/tasks?task={번호}
--                  → projmng.ai_task.cre_id  (그 작업을 지시한 사람)
--   SUBSCRIPTION   /admin/system/account/{아이디}/app
--                  → 그 아이디 자신 (새 기기를 등록한 사람)
--
-- 나머지(WEATHER 54 · SIGNUP 26 · TEST 11 · BIRTHDAY 3 · NOTE 2)는 비워 둔다 —
-- 주소에 사람이 안 실려 있거나, 날씨 특보처럼 **본디 지목한 사람이 없다.**
-- 그 줄들은 앱에서도 앱 아이콘으로 떴으므로 화면과 어긋나지 않는다.
--
-- ── 열쇠(?t=)를 안 붙인다 ────────────────────────────────────
--
-- 이 값을 읽는 것은 알림함뿐이고 그쪽은 언제나 로그인해 있다. 붙일 수도
-- 없다 — 60분짜리라 만드는 순간부터 썩는다(위 파일의 머리말).
--
-- ── DB 가 둘이라 한 문장으로 못 한다 ─────────────────────────
--
-- 알림 기록은 jsiniportal(scom), 작업은 projmng 다. 아래 1번 블록은
-- **projmng 에서 뽑아 온 (작업번호, 지시자)** 를 임시 표에 싣는 자리이고,
-- 그 목록을 만드는 질의는 주석에 적어 두었다.
--
-- 멱등하다 — icon IS NULL 인 줄만 건드린다.
-- **운영 반영 완료** (2026-09-27 — 472 + 9 줄).

BEGIN;

-- ── 1. projmng 에서 실어 온 (작업번호 → 지시자) ──────────────
--
-- 뽑는 질의 (projmng DB 에서):
--   \copy (SELECT task_key, trim(cre_id) FROM projmng.ai_task
--           WHERE cre_id IS NOT NULL AND trim(cre_id) <> '')
--     TO 'task-owner.psv' WITH (FORMAT csv, DELIMITER '|')
CREATE TEMP TABLE _task_owner(task_key bigint, cre_id text) ON COMMIT DROP;
\copy _task_owner FROM 'task-owner.psv' WITH (FORMAT csv, DELIMITER '|')

-- ── 2. 사람 → 대표 사진의 파일 아이디 ────────────────────────
--
-- AvatarIconResolver 와 **같은 규칙**이다 — is_primary 를 먼저 고르고,
-- 우리 파일 주소에서만 GUID 를 꺼낸다. 사진을 한 번도 안 올린 계정에는
-- 바깥 기본 이미지 주소(alipayobjects.com)가 적혀 있는데 그것은 우리
-- 파일이 아니므로 「사진 없음」으로 본다.
CREATE TEMP TABLE _avatar ON COMMIT DROP AS
SELECT DISTINCT ON (a.user_id)
       a.user_id,
       (regexp_match(
           d.content,
           '/(?:api/file/(?:download|thumbnail|medium|large)|files(?:/thumbnail|/avatar)?)/(?:id/)?([0-9a-fA-F-]{36})'
       ))[1] AS file_id
  FROM scom.accounts a
  JOIN scom.account_profile_details d ON d.account_id = a.id
 WHERE NOT a.is_deleted
   AND d.detail_type = 'Avatar'
   AND NOT d.is_deleted
   AND d.content <> ''
 ORDER BY a.user_id, d.is_primary DESC;

-- ── 3. AI 작업 알림 ──────────────────────────────────────────
--
-- 주소 모양이 둘이다. 옛 줄은 목록 주소(`?task=`)를 쓰고 새 줄은 건별
-- 주소를 쓴다 — 둘 다 작업 번호를 싣고 있어 어느 쪽이든 되짚힌다.
--
-- 지시자가 사진을 안 올렸으면 사람 형상 그림자다. 그것이 **앱에 뜬 그림과
-- 같다** — 그 경우 발송 때도 AvatarIconResolver 가 그림자를 채워 보냈다.
UPDATE scom.push_send_logs l
   SET icon = coalesce('/files/avatar/' || v.file_id, '/avatar-fallback.png')
  FROM _task_owner t
  LEFT JOIN _avatar v ON v.user_id = t.cre_id
 WHERE l.icon IS NULL
   AND l.category = 'AI_TASK'
   AND (t.task_key = (substring(l.url FROM '/projmng/ai/tasks?\?task=(\d+)$'))::bigint
     OR t.task_key = (substring(l.url FROM '/projmng/ai/task/(\d+)$'))::bigint);

-- ── 4. 구독 알림 ─────────────────────────────────────────────
--
-- 주소가 곧 그 사람이다. **category 로 거르지 않는다** — 구분을 붙이기
-- 전에 쌓인 줄이 섞여 있어서, 갈래로 거르면 그것들이 빠진다.
UPDATE scom.push_send_logs l
   SET icon = coalesce('/files/avatar/' || v.file_id, '/avatar-fallback.png')
  FROM (SELECT id, substring(url FROM '/admin/system/account/([^/]+)/app') AS who
          FROM scom.push_send_logs
         WHERE icon IS NULL
           AND url LIKE '/admin/system/account/%/app') s
  LEFT JOIN _avatar v ON v.user_id = s.who
 WHERE l.id = s.id;

COMMIT;

-- 확인 — 남은 NULL 이 「되짚을 실마리가 없거나 본디 사람이 없는」 줄이다.
--   SELECT coalesce(category,'(구분 없음)') AS 구분,
--          count(*) FILTER (WHERE icon IS NOT NULL) AS 채움,
--          count(*) FILTER (WHERE icon IS NULL)     AS 빔
--     FROM scom.push_send_logs GROUP BY 1 ORDER BY 2 DESC;
--
-- 되돌리기 (칸은 두고 값만)
--   UPDATE scom.push_send_logs SET icon = NULL;
