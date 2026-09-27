-- AI 작업 제한 시간 기본값을 60분에서 120분으로 변경한다.
-- 기존 작업의 지정 시간은 건드리지 않고, 앞으로 만들어지는 작업의 기본값만 바꾼다.

ALTER TABLE projmng.ai_task
    ALTER COLUMN timeout_minutes SET DEFAULT 120;

-- 확인: column_default 가 120 을 돌려주면 반영된 것이다.
SELECT column_default
  FROM information_schema.columns
 WHERE table_schema = 'projmng'
   AND table_name = 'ai_task'
   AND column_name = 'timeout_minutes';
