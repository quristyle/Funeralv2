-- ============================================================
-- 영상·음원에서 뽑아낸 썸네일 JPEG 에 is_image 표시를 되돌려 준다.
--
-- 대상 DB : jsiniportal (scom)
-- 실행    : psql -h <host> -p <port> -U funeralv2 -d jsiniportal -f docs/sql/file_video_thumbnail_isimage.sql
--
-- 여러 번 실행해도 안전하다 (이미 켜진 행은 건드리지 않는다).
--
-- ── 왜 ───────────────────────────────────────────────────────
-- FileServer 가 ffmpeg 으로 첫 장면을 뽑아 `funeralv2/Video/{guid}.jpg` 로 저장하면서
-- scom.filemetadatas 행은 만들어 주는데 `isimage` 를 켜지 않았다
-- (FileServer/Services/FileService.cs 의 썸네일 메타데이터 생성 다섯 곳).
--
-- 그 결과 축소본 경로 셋 — /api/file/thumbnail|medium|large/{id} — 이
-- GetPresetImageAsync 의 `if (!metadata.IsImage) throw` 에 걸려
-- "이미지 파일만 크기를 변환할 수 있습니다" 로 400 을 돌려줬다.
-- 그래서 영상 관리 화면은 축소본을 못 쓰고 64x40 칸에 원본 JPEG(최대 1.2MB)을
-- 그대로 내려받고 있었다.
--
-- 코드는 고쳤으므로 앞으로 만들어지는 썸네일은 표시가 켜진 채로 들어온다.
-- 이 파일은 그 전에 쌓인 행을 맞춰 주는 것이다.
--
-- ── 대상 ─────────────────────────────────────────────────────
-- Video · Audio 폴더에 있는 image/* 파일. 그 자리에 들어가는 이미지는
-- 추출 썸네일뿐이다 (영상 원본과 변환본은 video/*, 음원은 audio/*).
-- ============================================================

BEGIN;

UPDATE scom.filemetadatas
   SET isimage = true
 WHERE isimage = false
   AND contenttype LIKE 'image/%'
   AND (path LIKE '%/Video/%' OR path LIKE '%/Audio/%');

COMMIT;

-- 확인용
-- SELECT path, contenttype, isimage, count(*) OVER () AS total
--   FROM scom.filemetadatas
--  WHERE contenttype LIKE 'image/%'
--    AND (path LIKE '%/Video/%' OR path LIKE '%/Audio/%')
--  ORDER BY path;
