-- 빠른지시 대상(portal-jsini)의 격리를 원본 직접 → worktree 로 바꾼다
--
-- ── 무슨 일이 있었나 ────────────────────────────────────────
--
-- 「빠른 지시」로 보낸 건이 연달아 여덟 번 실패했다(2026-09-19). 사유가 둘뿐이다.
--
--   정본이 깨끗하지 않습니다. 사람이 먼저 정리해야 합니다: M docs/… ?? deploy/…
--   정본(main)을 최신으로 맞추지 못했습니다: …
--
-- 동시에 보내서 깨진 것으로 보였지만 아니다. 같은 대상에 두 건이 겹치는 것은
-- 이미 세 겹으로 막혀 있다 — 서버의 `running_run_key`, 실행기의 `TargetGate`,
-- 끝낼 때의 `Workspace.ParkAsync`. 실제로 일어난 일은 **앞 건 하나가 정본을
-- 더럽힌 채 끝났고, 그 뒤로 집혀 간 건이 전부 그 자리에서 죽은 것**이다.
--
-- ── 왜 정본이 더러워졌나 ────────────────────────────────────
--
-- 대상 5번이 `/home/lee/Funeralv2` 를 **「그냥 폴더」(kind=folder)** 로 등록하고
-- 있었다. 그러면 격리가 `inplace` 가 되고, 빠른지시로 보낸 것이 전부 **운영
-- 정본을 직접** 고친다. 거기서 push 게이트가 금지 경로(`deploy/`)에 걸려
-- 커밋을 못 하면 고친 파일이 정본에 그대로 남는다.
--
-- 같은 경로가 1번에는 `kind=repo` + `worktree` 로 **이미 제대로** 등록돼 있었다.
-- 5번은 push 를 켜려고 따로 만든 것으로 보이는데, 격리까지 같이 바뀐 것을
-- 아무도 보지 못했다. worktree 도 push 는 그대로 된다 — 따로 만들 이유가 없었다.
--
-- ── 바꾸면 무엇이 달라지나 ──────────────────────────────────
--
-- 실행마다 `ai/<task>-<run>` 가지의 worktree 에서 돌므로
--
--   · 정본이 더러워질 일이 없다 (그래서 뒤엣것이 막히지 않는다)
--   · 정본이 더러워도 막지 않고 경고만 한다
--   · 같은 대상 여러 건이 실제로 병렬로 돈다 (준비 몇 초만 줄을 선다)
--   · push 는 그대로 — 게이트가 그 가지를 HEAD:main 으로 민다
--
-- 코드 쪽에도 그물을 하나 더 뒀다: `Workspace.PrepareAsync` 가 원본 직접인데
-- 정본이 더러우면 이제 **막지 않고 stash 로 옮기고 지나간다.** 실행기가 중간에
-- 죽어 뒷정리가 안 도는 경우까지 받아 내기 위한 것이다.

BEGIN;

UPDATE projmng.ai_target
   SET target_kind    = 'repo',
       isolation_mode = 'worktree'
 WHERE target_key = 5;

COMMIT;

-- 확인 — 셋 다 repo + worktree 여야 한다.
--
--   SELECT target_key, target_nm, target_kind, isolation_mode, push_ref, allow_push
--     FROM projmng.ai_target
--    WHERE is_deleted = false AND target_path = '/home/lee/Funeralv2'
--    ORDER BY target_key;
--
-- 운영 반영: 2026-09-19 완료.
