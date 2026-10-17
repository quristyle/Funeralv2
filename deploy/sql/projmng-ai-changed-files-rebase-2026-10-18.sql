-- ---------------------------------------------------------------
-- 「처리 파일」이 그 차수의 것이 아니던 여섯 건을 되살린다 (2026-10-18)
-- ---------------------------------------------------------------
--
-- [무슨 일이 있었나]
--
-- 실행기가 기준(`ai_task_run.base_sha`)을 **정본의 HEAD** 로 잡고 있었는데
-- worktree 는 그 자리가 아니라 `origin/<ref>` 에서 갈라진다. 정본에 448건이
-- staged 로 남아 `git merge --ff-only` 가 막히면서 기준이 108 커밋 뒤
-- (`c5180616`)에 굳었고, 그래서 실행 724~729 가 **전부 201줄**(상한 200 +
-- 「외 N개」)로 쌓였다 — 그 차수가 한 줄도 건드린 적 없는 `CLAUDE.md` ·
-- `ApiGateway/Program.cs` 따위다.
--
-- 되살리는 길은 **그 차수가 올린 커밋**이다. 끝난 시각과 커밋 시각이 2분 안이고
-- 제목이 그대로 맞아 짝이 하나로 정해진다. 결과문에서 다시 뽑지 않았다 —
-- git 이 있는데 추정을 쓸 까닭이 없다.
--
--   run 724 (작업 426 · 모바일 팝업 키보드 대응)      ← 4accb7ed   6줄
--   run 727 (작업 425 · 빠른실행 4단 상태문자)        ← e10eff23   4줄
--   run 725 (작업 439 · 502 오류 화면)                ← 3900f525   5줄
--   run 728 (작업 428 · 파일목록 차수별 5차)          ← 882706c6  19줄
--   run 726 (작업 440 · 빠른 지시 모바일 가로 뷰)     ← aeecec27   2줄
--   run 729 (작업 441 · 계정 마지막 사용 시간)        ← 7548367a  17줄
--
-- **이 파일은 기록이다.** 값은 2026-10-17 15:26 UTC 에 이미 넣었고
-- (`files_merged_at` 이 그 시각이다) 같은 내용을 여기 적어 둔다 — 되살린 목록을
-- 어디서 가져왔는지가 코드에도 DB 에도 안 남기 때문이다. 다시 돌릴 일이 생기면
-- 저장소에서 `git show --name-status --find-renames <커밋>` 으로 같은 값을 뽑는다.
--
-- 뿌리 원인은 실행기에서 고쳤다(`Workspace.PrepareAsync` — worktree 의 HEAD 를
-- 기준으로 쓴다). 그 전까지 돈 건은 이 여섯뿐이다
-- (`changed_files` 가 12줄을 넘는 건을 전부 훑어 확인했다).
--
-- 확인:
--   select run_key, seq,
--          (select count(*) from regexp_split_to_table(changed_files, E'\n') x
--            where x <> '') n
--     from projmng.ai_task_run
--    where run_key between 724 and 729 order by run_key;
--   -- 기대: 6 · 5 · 2 · 4 · 19 · 17  (201 이 하나도 없어야 한다)

BEGIN;

-- run 724 (작업 426 · 모바일 팝업 키보드 대응 개선) ← 커밋 4accb7ed
UPDATE projmng.ai_task_run
   SET changed_files = 'M	web/CLAUDE.md
M	web/src/Shared/JSini.Web.Components/Data/CommPopup.razor
M	web/src/Shared/JSini.Web.Components/Layout/JSiniHead.razor
M	web/src/Shared/JSini.Web.Components/wwwroot/app.css
A	web/src/Shared/JSini.Web.Components/wwwroot/keyboard-inset.js
A	web/tests/JSini.Web.Architecture.Tests/KeyboardInsetTests.cs',
       files_merged_at = now()
 WHERE run_key = 724;

-- run 727 (작업 425 · 빠른실행 4단 상태문자 2글자 표시) ← 커밋 e10eff23
UPDATE projmng.ai_task_run
   SET changed_files = 'M	web/src/Apps/JSini.Web.ProjMng/Components/Shared/AiAskPanel.razor
M	web/src/Apps/JSini.Web.ProjMng/Components/Shared/AiAskPanel.razor.cs
M	web/src/Apps/JSini.Web.ProjMng/wwwroot/projmng.css
M	web/tests/JSini.Web.Architecture.Tests/AiAskLayoutTests.cs',
       files_merged_at = now()
 WHERE run_key = 727;

-- run 725 (작업 439 · 502 오류 화면 개선 및 반영 시도) ← 커밋 3900f525
UPDATE projmng.ai_task_run
   SET changed_files = 'M	CLAUDE.md
M	deploy/nginx/README.md
A	deploy/nginx/html/50x.html
M	deploy/nginx/jsini.co.kr.conf
M	deploy/nginx/portal.jsini.co.kr.conf',
       files_merged_at = now()
 WHERE run_key = 725;

-- run 728 (작업 428 · 파일목록을 차수별로 관리하라 (5차)) ← 커밋 882706c6
UPDATE projmng.ai_task_run
   SET changed_files = 'M	docs/ai-task-runner.md
M	microservices/ProjMngServer.Tests/AiChangedFilesTests.cs
M	microservices/ProjMngServer/Controllers/AiRunsController.cs
M	microservices/ProjMngServer/Program.cs
M	microservices/ProjMngServer/Services/AiChangedFiles.cs
A	microservices/ProjMngServer/Services/AiRunFileCheckService.cs
M	microservices/ProjMngServer/Services/AiRunFileWriter.cs
M	microservices/ProjMngServer/Services/AiTaskNotifier.cs
A	tools/AiTaskRunner/FileCheckWorker.cs
A	tools/AiTaskRunner/FileProbe.cs
M	tools/AiTaskRunner/Program.cs
M	tools/AiTaskRunner/RunnerOptions.cs
M	tools/AiTaskRunner/ServerClient.cs
M	tools/AiTaskRunner/appsettings.json
M	web/src/Apps/JSini.Web.ProjMng/Components/Shared/AiRunFiles.cs
M	web/src/Apps/JSini.Web.ProjMng/Components/Shared/AiTaskFilesTab.razor
M	web/src/Apps/JSini.Web.ProjMng/Components/Shared/AiTaskFilesTab.razor.cs
M	web/src/Apps/JSini.Web.ProjMng/wwwroot/projmng.css
M	web/tests/JSini.Web.Architecture.Tests/AiRunFilesTests.cs',
       files_merged_at = now()
 WHERE run_key = 728;

-- run 726 (작업 440 · 빠른 지시 화면 모바일 가로 뷰 개선) ← 커밋 aeecec27
UPDATE projmng.ai_task_run
   SET changed_files = 'M	web/src/Apps/JSini.Web.ProjMng/wwwroot/projmng.css
M	web/tests/JSini.Web.Architecture.Tests/AiAskLayoutTests.cs',
       files_merged_at = now()
 WHERE run_key = 726;

-- run 729 (작업 441 · 계정 마지막 사용 시간 컬럼 추가) ← 커밋 7548367a
UPDATE projmng.ai_task_run
   SET changed_files = 'M	CLAUDE.md
A	deploy/sql/account-last-seen-2026-10-18.sql
A	docs/account-activity.md
M	docs/menu-usage.md
M	microservices/AuthServer/DTOs/AccountDto.cs
M	microservices/AuthServer/Entities/Account.cs
A	microservices/AuthServer/Migrations/20261017145946_AddAccountLastSeenAt.Designer.cs
A	microservices/AuthServer/Migrations/20261017145946_AddAccountLastSeenAt.cs
M	microservices/AuthServer/Migrations/AppDbContextModelSnapshot.cs
M	microservices/AuthServer/Program.cs
A	microservices/AuthServer/Services/AccountActivityService.cs
M	microservices/AuthServer/Services/LoginCompletion.cs
M	microservices/AuthServer/Services/MenuUsageService.cs
M	microservices/AuthServer/Services/UserService.cs
M	web/src/Apps/JSini.Web.Admin/Api/AdminModels.cs
M	web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor
M	web/src/Apps/JSini.Web.Admin/Components/Pages/UserList.razor.cs',
       files_merged_at = now()
 WHERE run_key = 729;

COMMIT;
