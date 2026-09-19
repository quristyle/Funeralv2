-- 기존 AI 작업 대상에서도 Copilot CLI를 선택할 수 있게 한다.
-- 이미 들어 있는 값은 건드리지 않는다.
UPDATE projmng.ai_target
   SET runner_kinds = runner_kinds || ',copilot',
       mod_dt = now()
 WHERE is_deleted = false
   AND position(',copilot,' IN ',' || replace(runner_kinds, ' ', '') || ',') = 0;
