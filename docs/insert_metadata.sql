-- 1. device_attributes 테이블에 동영상 및 음악 ID 저장을 위한 컬럼 추가 DDL
ALTER TABLE smfr.device_attributes ADD COLUMN IF NOT EXISTS video_id varchar(50) NULL;
ALTER TABLE smfr.device_attributes ADD COLUMN IF NOT EXISTS music_id varchar(50) NULL;

-- 2. biz_select_configs 테이블에 동영상(video) 및 음악(music) BizSelect 설정 등록
-- ID는 임의의 UUID를 정적으로 생성하여 등록합니다.
INSERT INTO scom.biz_select_configs (
    id, 
    biz_type, 
    api_url, 
    http_method, 
    label_field, 
    value_field, 
    result_path, 
    processor_type, 
    remark, 
    created_at, 
    is_deleted
)
VALUES
(
    'c2f37c35-1823-4c92-bf3d-9f4a0c8d1976', 
    'video', 
    '/funeral/building/source/list?type=VIDEO', 
    'GET', 
    'name', 
    'id', 
    'result', 
    NULL, 
    '장비 속성 - 동영상 목록', 
    NOW(), 
    false
),
(
    'a7b4f2c8-9314-41d6-b5e1-0c5d3a9f8264', 
    'music', 
    '/funeral/building/source/list?type=AUDIO', 
    'GET', 
    'name', 
    'id', 
    'result', 
    NULL, 
    '장비 속성 - 음악 목록', 
    NOW(), 
    false
)
ON CONFLICT (id) DO UPDATE 
SET biz_type = EXCLUDED.biz_type,
    api_url = EXCLUDED.api_url,
    label_field = EXCLUDED.label_field,
    value_field = EXCLUDED.value_field,
    remark = EXCLUDED.remark;

-- 3. media_sources 테이블에 동영상(VIDEO) 및 음악(AUDIO) 샘플 데이터 등록
INSERT INTO smfr.media_sources (
    id,
    name,
    short_name,
    source_type,
    url,
    file_size,
    sort_order,
    remark,
    is_deleted,
    created_at
)
VALUES
(
    'sample-video-001',
    '기본 홍보 동영상',
    '홍보영상',
    'VIDEO',
    '/media/videos/sample-video-001.mp4',
    15728640,
    1,
    '기본 재생용 동영상 샘플',
    false,
    NOW()
),
(
    'sample-video-002',
    '장례 절차 안내 동영상',
    '안내영상',
    'VIDEO',
    '/media/videos/sample-video-002.mp4',
    20971520,
    2,
    '상례 및 절차 안내용 동영상 샘플',
    false,
    NOW()
),
(
    'sample-music-001',
    '추모 연주곡 - 엘리제를 위하여',
    '추모곡1',
    'AUDIO',
    '/media/musics/sample-music-001.mp3',
    4194304,
    1,
    '추모 배경음악 샘플 1',
    false,
    NOW()
),
(
    'sample-music-002',
    '고요한 피아노 선율',
    '추모곡2',
    'AUDIO',
    '/media/musics/sample-music-002.mp3',
    5242880,
    2,
    '추모 배경음악 샘플 2',
    false,
    NOW()
)
ON CONFLICT (id) DO UPDATE 
SET name = EXCLUDED.name,
    short_name = EXCLUDED.short_name,
    source_type = EXCLUDED.source_type,
    url = EXCLUDED.url,
    file_size = EXCLUDED.file_size,
    sort_order = EXCLUDED.sort_order,
    remark = EXCLUDED.remark;
