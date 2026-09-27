-- 이미 쌓인 발송 기록에 **알림구분**을 되짚어 넣는다 (2026-09-27).
--
-- 앞선 `notification-push-category-2026-09-27.sql` 이 칸을 만들었고, 그 뒤로
-- 보내는 자리들이 스스로 구분을 적는다. 이 파일은 **그 전에 쌓인 544줄**의 몫이다.
--
-- [무엇을 근거로 가르나]
--
-- 그 줄에 「어디서 보냈나」가 통째로 남아 있지는 않다. 남은 것은 셋이고,
-- 그중 **보낸 이 표시(`sent_by`)와 누를 주소(`url`)가 제목보다 단단하다** —
-- 제목은 사람이 적은 글이라 문구가 바뀌지만 그 둘은 코드가 박아 넣은 값이다.
--
-- | 근거 | 예 |
-- |---|---|
-- | `sent_by` | `AI_TASK`(프로젝트관리 알리미) · `AUTH_SIGNUP`(가입) · `system` |
-- | `url` | `/life/weather/*` · `/life/birthday*` · `/admin/note/box` |
-- | `title` | 위 둘로 안 갈리는 것만 (`새로운 알림 구독` · `JSini 포털 시험 알림`) |
--
-- [모르면 비워 둔다]
--
-- 어느 규칙에도 안 걸리는 줄은 **NULL 로 남긴다.** 그럴듯한 갈래로 밀어 넣으면
-- 그 뒤로는 틀린 것을 확인할 길이 없어진다 — 「구분 없음」으로 남아 있는 편이
-- 낫고, 화면이 그 갈래를 따로 보여 준다.
--
-- [다시 돌려도 안전하다]
--
-- 모든 갱신이 `category IS NULL` 인 줄만 건드린다. 그래서 **사람이 화면에서
-- 고쳐 놓은 값을 덮지 않고**, 보내는 코드가 적어 둔 값도 그대로 둔다.

BEGIN;

-- ── 1. AI 작업 (요청 · 남긴말 · 결과) ─────────────────────
--
-- 프로젝트관리의 알리미 셋이 모두 `X-User-Id: AI_TASK` 로 부른다.
UPDATE scom.push_send_logs
   SET category = 'AI_TASK'
 WHERE category IS NULL
   AND sent_by = 'AI_TASK';

-- ── 2. 가입 신청 ──────────────────────────────────────────
--
-- 푸시(새 가입 신청)와 메일(신청·결과·승인)이 같은 갈래다 — 한 흐름이라
-- 나눠 두면 「그 가입 건에 무엇이 나갔나」를 두 번 물어야 한다.
UPDATE scom.push_send_logs
   SET category = 'SIGNUP'
 WHERE category IS NULL
   AND sent_by = 'AUTH_SIGNUP';

-- ── 3. 기상·날씨 ──────────────────────────────────────────
--
-- 특보·기준 충족·내 위치 날씨 셋 다 시스템이 보내 `sent_by` 가 비어 있다.
-- 누를 주소가 갈래를 말해 준다.
UPDATE scom.push_send_logs
   SET category = 'WEATHER'
 WHERE category IS NULL
   AND (url LIKE '/life/weather/%' OR title LIKE '[기상]%' OR title LIKE '[날씨]%');

-- ── 4. 생일 ───────────────────────────────────────────────
--
-- 사람이 생일 목록에서 손으로 보낸 것도 여기 든다 — 보낸 방법이 아니라
-- **무엇에 대한 알림인가**가 갈래다.
UPDATE scom.push_send_logs
   SET category = 'BIRTHDAY'
 WHERE category IS NULL
   AND (url LIKE '/life/birthday%' OR title LIKE '%생일 축하%');

-- ── 5. 쪽지 ───────────────────────────────────────────────
UPDATE scom.push_send_logs
   SET category = 'NOTE'
 WHERE category IS NULL
   AND (url = '/admin/note/box' OR title LIKE '쪽지 · %');

-- ── 6. 알림 구독 ──────────────────────────────────────────
--
-- 제목으로 가른다. 주소가 둘(빈 값 · 그 사람의 앱 현황)이라 주소로는 안 묶인다.
UPDATE scom.push_send_logs
   SET category = 'SUBSCRIPTION'
 WHERE category IS NULL
   AND title = '새로운 알림 구독';

-- ── 7. 시험 발송 ──────────────────────────────────────────
--
-- 제목을 서버가 박는다(`SendTestPushToMe`). 사람이 같은 제목을 손으로 적어
-- 보냈을 수도 있지만, 그 경우에도 하려던 일은 시험이다.
UPDATE scom.push_send_logs
   SET category = 'TEST'
 WHERE category IS NULL
   AND title = 'JSini 포털 시험 알림';

COMMIT;

-- 확인 — 남은 NULL 이 「판단이 어려워 비워 둔」 줄이다.
-- SELECT coalesce(category,'(구분 없음)') AS 구분, count(*)
--   FROM scom.push_send_logs GROUP BY 1 ORDER BY 2 DESC;
--
-- SELECT left(coalesce(title,'(제목없음)'),40), coalesce(url,'(주소없음)'),
--        coalesce(sent_by,'(없음)'), count(*)
--   FROM scom.push_send_logs WHERE category IS NULL
--  GROUP BY 1,2,3 ORDER BY 4 DESC;
