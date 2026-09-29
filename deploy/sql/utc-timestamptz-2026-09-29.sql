-- ============================================================================
-- 시각 칸을 전부 UTC 로 — timestamp → timestamptz (2026-09-29)
-- ============================================================================
--
-- [무엇이 문제였나]
--
-- 이 시스템의 시각 칸은 두 가지가 섞여 있었다.
--
--   * `timestamp with time zone`  — 진짜 순간(instant)을 담는다. 안에는 UTC 로
--     적히고 읽을 때 세션 시간대로 옮겨진다. 대부분이 이것이다.
--   * `timestamp without time zone` — 시간대가 없는 「벽시계 숫자」다.
--     DB 서버의 timezone 이 Asia/Seoul 이라 `now()` 를 넣으면 **한국 시각의
--     숫자가 그대로 박힌다.** projmng 45칸 · helpdesk 7칸이 이랬다.
--
-- 그래서 화면마다 「이 칸은 UTC 인가 KST 인가」를 따로 따져야 했다. 같은 표
-- 안에서도 칸마다 답이 달라(`ai_task.requested_at` 는 KST 숫자,
-- `improvementrequest.requestedat` 는 UTC) 아홉 시간 어긋난 계산이 반복해서
-- 나왔다.
--
-- [무엇을 하나]
--
-- 시간대 없는 시각 칸을 전부 `timestamptz` 로 바꾼다. 기존 값은 **한국 벽시계
-- 숫자**이므로 `AT TIME ZONE 'Asia/Seoul'` 로 읽어 옳은 순간으로 옮긴다.
-- (`timestamp AT TIME ZONE 'Asia/Seoul'` 은 「이 벽시계 숫자를 서울 시각으로
-- 해석해서 timestamptz 로」라는 뜻이다. 반대 방향이 아니다.)
--
-- 예외가 하나 있다 — `projmng.home_todo.target_day` 는 시각이 아니라
-- **달력 날짜**다(할 일의 목표일, C# 에서 DateOnly, SQL 에서 `current_date` 와
-- 비교한다). 순간이 아니므로 시간대를 붙이면 안 된다. `date` 로 바꾼다.
--
-- 그리고 DB 기본 시간대를 UTC 로 고정한다. 이제 모든 시각 칸이 timestamptz 라
-- 저장값은 시간대 설정과 무관하지만, `psql`·로그·`::text` 가 UTC 로 보여야
-- 「시스템은 UTC 로 돈다」가 눈으로도 맞는다.
--
-- 달력 날짜를 다루는 SQL 의 「오늘」은 세션 시간대에 끌려다니면 안 된다.
-- `current_date` 는 세션 시간대를 따르므로 UTC 로 고정한 순간부터 한국의
-- 오전 9시 전 아홉 시간 동안 **어제**를 가리킨다. 일정의 지연 일수가 하루씩
-- 줄고, 새 WBS 의 기본 계획일이 어제로 잡힌다. 그래서 아래에 함수를 하나
-- 두고 projmng 의 SQL 은 `current_date` 대신 그것을 부른다.
--
--   순간(instant) = UTC · 달력 날짜(date) = 한국 달력. 둘을 섞지 않는다.
--
-- [쓰는 법]
--
-- 이 시스템이 쓰는 DB **일곱 곳 전부**에 돌린다. 칸을 바꿀 것이 있는 곳은
-- projmng(45) 와 helpdesk(7) 뿐이지만, 나머지도 기본 시간대를 UTC 로 맞춰
-- 둬야 `psql`·로그·`::text` 가 한곳만 KST 로 보이는 일이 없다.
--
--   for db in projmng helpdesk cargotrust funeralv2 ghub jsiniportal jsinisite; do
--     psql -h <호스트> -p <포트> -U <슈퍼유저> -d $db -v ON_ERROR_STOP=1 -f 이파일
--   done
--
-- 두 번 돌려도 안전하다 — 이미 timestamptz 인 칸은 건너뛴다.
--
-- [일부러 손대지 않은 DB]
--
--   jinrecept  옛 헬프데스크 자료(스키마 `jsini`). 시간대 없는 칸이 8개 남아
--              있지만 **이 저장소의 어느 서비스도 읽지 않는다**(운영
--              HelpDeskServer 가 보는 것은 빈 `helpdesk` 쪽이다). 바깥의 옛
--              시스템이 아직 그것을 naive KST 로 읽고 있을 수 있어 놔둔다.
--   goldb      다른 제품의 DB 다. 96칸이 시간대 없는 값이지만 이 시스템이 아니다.
-- ============================================================================

BEGIN;

-- 1) 달력 날짜 칸은 date 로. (없는 DB 에서는 조용히 넘어간다)
DO $$
BEGIN
  IF EXISTS (
      SELECT 1 FROM information_schema.columns
       WHERE table_schema = 'projmng' AND table_name = 'home_todo'
         AND column_name = 'target_day' AND data_type = 'timestamp without time zone')
  THEN
    EXECUTE 'ALTER TABLE projmng.home_todo '
         || 'ALTER COLUMN target_day TYPE date USING target_day::date';
    RAISE NOTICE 'projmng.home_todo.target_day → date';
  END IF;
END $$;

-- 2) 나머지 시간대 없는 시각 칸은 전부 timestamptz 로.
--    기존 값은 한국 벽시계 숫자라 Asia/Seoul 로 해석한다.
DO $$
DECLARE
  r      record;
  n      int := 0;
BEGIN
  FOR r IN
      SELECT n.nspname AS sch, c.relname AS tbl, a.attname AS col
        FROM pg_attribute a
        JOIN pg_class     c ON c.oid = a.attrelid
        JOIN pg_namespace n ON n.oid = c.relnamespace
       WHERE a.atttypid = 'timestamp'::regtype
         AND a.attnum > 0
         AND NOT a.attisdropped
         AND c.relkind IN ('r', 'p')          -- 표와 파티션 부모만. 뷰는 따라 바뀐다.
         AND n.nspname NOT IN ('pg_catalog', 'information_schema')
       ORDER BY 1, 2, 3
  LOOP
    EXECUTE format(
        'ALTER TABLE %I.%I ALTER COLUMN %I TYPE timestamptz '
        || 'USING %I AT TIME ZONE ''Asia/Seoul''',
        r.sch, r.tbl, r.col, r.col);
    n := n + 1;
    RAISE NOTICE '%.%.% → timestamptz', r.sch, r.tbl, r.col;
  END LOOP;

  RAISE NOTICE '바꾼 칸: %', n;
END $$;

-- 3) 달력 날짜의 「오늘」. `current_date` 를 이것으로 바꿔 부른다.
--    시간대를 함수 안에 못박아, 세션이 UTC 여도 한국 달력의 오늘을 준다.
--    (projmng 전용이다 — 다른 DB 에는 date 칸을 오늘과 견주는 곳이 없다)
DO $$
BEGIN
  IF current_database() = 'projmng' THEN
    EXECUTE $fn$
      CREATE OR REPLACE FUNCTION projmng.today_kst() RETURNS date
      LANGUAGE sql STABLE PARALLEL SAFE AS
      $body$ SELECT (now() AT TIME ZONE 'Asia/Seoul')::date $body$
    $fn$;
    RAISE NOTICE 'projmng.today_kst() 준비됨';
  END IF;
END $$;

COMMIT;

-- 4) DB 기본 시간대를 UTC 로. (다음 접속부터 듣는다)
--    ALTER DATABASE 는 트랜잭션 안에서 못 돈다.
DO $$
BEGIN
  EXECUTE format('ALTER DATABASE %I SET timezone TO ''UTC''', current_database());
END $$;

-- 5) 확인 — 남은 시간대 없는 시각 칸은 0 이어야 한다.
SELECT count(*) AS "남은 timestamp 칸"
  FROM pg_attribute a
  JOIN pg_class     c ON c.oid = a.attrelid
  JOIN pg_namespace n ON n.oid = c.relnamespace
 WHERE a.atttypid = 'timestamp'::regtype
   AND a.attnum > 0 AND NOT a.attisdropped
   AND c.relkind IN ('r', 'p')
   AND n.nspname NOT IN ('pg_catalog', 'information_schema');
