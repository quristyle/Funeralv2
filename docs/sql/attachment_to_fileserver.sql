-- ============================================================
-- 헬프데스크 첨부파일을 FileServer 로 옮긴다 (결정 D5-B)
-- ============================================================
--
-- ⚠ 이 파일은 **헬프데스크 DB(jinrecept)** 에 실행한다. 다른 SQL 과 DB 가 다르다.
--    docs/sql 의 나머지는 포털 DB(funeralv2/ scom) 대상이다.
--
-- ── 무엇이 문제였나 ────────────────────────────────────────
--
-- FileServer 라는 전용 서비스가 있는데도 헬프데스크가 첨부파일을 따로 관리하고 있었다.
--
--   FileServer      scom.filemetadatas   ·  /home/quri/goldb_storage  ·  /api/file/*
--   HelpDeskServer  jsini.attachment     ·  /home/lee/jinAttachment   ·  /api/attachments, /api/files
--
-- 같은 일을 두 곳에서 다르게 한다. 백업 대상도 둘, 용량 관리도 둘이다.
--
-- ── 실제 데이터 (2026-08-26 확인) ──────────────────────────
--
--   37건 · 합계 21.6MB · 최대 6.6MB
--   entitytype 은 전부 'ImprovementRequest' 하나다
--   xlsx 15 · pdf 6 · xls 5 · jpeg 5 · png 2 · ico 1 · pptx 1 · mp4 1
--
-- **저장 경로가 하나가 아니다.** 문서에는 /home/quri/jinAttachment 로 적혀 있었지만
-- 실제로는 둘로 갈려 있다.
--
--   /home/lee/jinAttachment    35건
--   /home/quri/jinAttachment    2건
--
-- 그래서 옮기는 도구는 디렉터리를 가정하지 않고 **행마다 filepath 를 읽는다.**
-- (`FileUploadEndpoints` 가 환경변수 FileStorage_BasePath 를 쓰고 기본값이
--  /home/lee/jinAttachment 였다. 장비마다 값이 달랐던 흔적이다.)
--
-- ── 이 파일이 하는 일 ──────────────────────────────────────
--
-- 컬럼 두 개를 더한다. 데이터를 옮기는 것은 파일 바이트를 읽어야 하므로
-- 배포 장비에서 도는 도구가 한다 → deploy/attachment-migration/
--
--   fileid      FileServer 가 발급한 파일 아이디. 채워지면 이 행은 옮겨진 것이다.
--   migratedat  옮긴 시각
--
-- 옮긴 뒤에도 filepath·storedfilename 은 **지우지 않는다.** 되돌릴 근거이고,
-- 원본 파일을 실제로 지우는 것은 사람이 확인한 뒤 할 일이다.
--
-- 반복 실행해도 안전하다.

BEGIN;

-- FileServer 가 발급한 파일 아이디 (uuid). 비어 있으면 아직 옮기지 않은 행이다.
ALTER TABLE jsini.attachment
  ADD COLUMN IF NOT EXISTS fileid text;

-- 옮긴 시각. 언제 무엇이 옮겨졌는지 남는다.
ALTER TABLE jsini.attachment
  ADD COLUMN IF NOT EXISTS migratedat timestamptz;

COMMENT ON COLUMN jsini.attachment.fileid IS
  'FileServer 가 발급한 파일 아이디. 채워져 있으면 내려받기는 FileServer 로 넘긴다 (결정 D5-B).';

COMMENT ON COLUMN jsini.attachment.migratedat IS
  'FileServer 로 옮긴 시각. filepath·storedfilename 은 되돌릴 근거로 남겨 둔다.';

-- 아직 안 옮긴 행을 찾는 것이 도구의 주 질의다.
CREATE INDEX IF NOT EXISTS "IX_attachment_unmigrated"
  ON jsini.attachment (id)
  WHERE fileid IS NULL;

COMMIT;

-- ── 진행 상황 확인 ─────────────────────────────────────────
SELECT
  count(*)                                   AS 전체,
  count(fileid)                              AS 옮김,
  count(*) - count(fileid)                   AS 남음,
  pg_size_pretty(sum(filesize))              AS 용량
FROM jsini.attachment;
