-- projmng.devdbinfo 에 정렬 순서 칸(db_srt)을 더한다
--
-- 뽑은 곳 · 건 곳: jin114.co.kr:31015/projmng · 스키마 projmng
-- 건 날: 2026-09-13
--
-- ─────────────────────────────────────────────────────────────
-- [왜 필요한가]
--
-- DB 접속 목록에는 **사람이 정하는 차례가 없었다.** 목록 화면은
-- db_rid(만들어진 순서)로, 고르개(projdb · projdb2)는 db_nick(가나다)로
-- 각자 다르게 늘어놓았다. 그래서 같은 자료를 보는 두 자리의 차례가
-- 서로 달랐고, **자주 쓰는 접속을 위로 올릴 방법이 아예 없었다.**
--
-- ─────────────────────────────────────────────────────────────
-- [첫 값은 「프로젝트 → 이름」으로 매긴다]
--
-- 고르개가 지금 쓰는 차례(db_nick)를 프로젝트 안에서 그대로 지킨다 —
-- 이 칸이 생겼다고 **고르개의 차례가 오늘 바뀌지는 않는다.** 목록 화면만
-- 「만든 순서」에서 「프로젝트끼리 모인 순서」로 바뀌고, 그쪽이 더 읽기 쉽다.
--
-- 값이 **13줄 모두 서로 다르다.** 그것이 뜻을 갖는다 — 끌어 옮기기는
-- 화면에 보이는 줄들이 지금 차지한 값들을 모아 다시 나눠 주는 방식이라
-- (ProjectDbService.ReorderAsync), 값이 겹치면 프로젝트로 좁혀 본 화면에서
-- 옮긴 것이 다른 프로젝트의 차례까지 흔든다.
--
-- ─────────────────────────────────────────────────────────────
-- [되돌리기]
--
--   ALTER TABLE projmng.devdbinfo DROP COLUMN db_srt;
--
-- 칸만 지우면 된다. 읽는 쪽(ProjectDbService · ProjCodeService)은
-- 값이 없으면 맨 뒤로 보내고 이름으로 줄 세우므로 옛 동작으로 돌아간다.

BEGIN;

ALTER TABLE projmng.devdbinfo
    ADD COLUMN IF NOT EXISTS db_srt integer;

COMMENT ON COLUMN projmng.devdbinfo.db_srt IS
    '보여 줄 차례. 작을수록 먼저. 목록 화면에서 끌어 옮겨 정한다.';

-- 프로젝트가 없는 줄(prj_rid IS NULL)은 맨 뒤로 보낸다.
WITH ordered AS (
    SELECT db_rid,
           row_number() OVER (ORDER BY prj_rid NULLS LAST, db_nick, db_rid) AS srt
      FROM projmng.devdbinfo
)
UPDATE projmng.devdbinfo d
   SET db_srt = o.srt
  FROM ordered o
 WHERE o.db_rid = d.db_rid
   AND d.db_srt IS NULL;

COMMIT;

-- 확인용
-- SELECT db_srt, prj_rid, db_nick FROM projmng.devdbinfo ORDER BY db_srt;
