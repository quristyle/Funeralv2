-- AI 작업: 실패하면 스스로 다시 시도한다
--
-- 설계 6.11 은 자동 재시도를 막아 두었는데, 그 조건이 「작업공간이 실행마다
-- 완전히 갈리는 것」이었다(7.3). worktree · 복사본 대상은 지금 그 조건을
-- 만족하므로 상한을 열어 준다.
--
-- 열어 주는 것은 **상한뿐**이다. 실제로 다시 돌릴지는 서버가 판정한다
-- (`AiRunService.CompleteAsync`) —
--
--   · 실패 · 시간 초과만. 연락 끊김(interrupted)과 취소는 제외
--   · 원본 직접(inplace) 대상은 제외
--
-- 그래서 이 SQL 을 돌려도 원본 직접 대상의 작업은 예전과 똑같이 동작한다.
--
-- 되돌리려면 attempt_max 를 1 로 다시 내리면 된다. 코드는 상한이 1이면
-- 재시도하지 않는다.

BEGIN;

-- ① 앞으로 만들어질 작업의 기본값.
ALTER TABLE projmng.ai_task
    ALTER COLUMN attempt_max SET DEFAULT 3;

-- ② 이미 있는 작업.
--
-- **사람이 일부러 올려 둔 값은 건드리지 않는다.** 1 인 것만 올린다 —
-- 2 나 5 로 적어 둔 것은 그 사람의 판단이다.
UPDATE projmng.ai_task
   SET attempt_max = 3,
       row_version = row_version + 1
 WHERE attempt_max = 1
   AND is_deleted = false;

COMMIT;

-- 확인
--
--   SELECT attempt_max, count(*)
--     FROM projmng.ai_task
--    WHERE is_deleted = false
--    GROUP BY attempt_max
--    ORDER BY attempt_max;
