-- ==============================================================================
-- jsini-portal 사용자 계정 일괄 등록 스크립트
-- ==============================================================================

-- 1. 이전 실패로 인해 중단된 트랜잭션 상태 초기화
ROLLBACK;

-- 2. scom.accounts 테이블에 신규 계정 삽입 (기존 계정은 건너뜀)
INSERT INTO scom.accounts (
    id,
    user_id,
    user_name,
    real_name,
    password,
    is_deleted,
    created_at,
    created_by,
    updated_at,
    updated_by
)
SELECT
    gen_random_uuid()::text,
    v.user_id,
    v.user_name,
    v.real_name,
    '1234',
    false,
    now(),
    'system',
    now(),
    'system'
FROM (VALUES
    ('soquri', '개발자테스트', '개발자테스트'),
    ('whgdmsrkddks', '좋은강안', '좋은강안'),
    ('pnuh7161', '부산대장례식장', '부산대장례식장'),
    ('gosin', '고신대병원장례식장', '고신대병원장례식장'),
    ('goehd', '해동병원장례식장', '해동병원장례식장'),
    ('skacjs', '남천장례식장', '남천장례식장'),
    ('pinkles23', '부산장례식장', '부산장례식장'),
    ('jyh', '윤형이', '윤형이'),
    ('wnfPtkatjs', '주례삼선', '주례삼선'),
    ('administrator', '미르포토', '미르포토'),
    ('soofh', '수요양병원장례식장', '수요양병원장례식장'),
    ('ckrgkswkdfP', '착한장례식장', '착한장례식장'),
    ('tkgkrnals', '사하구민', '사하구민'),
    ('qntkswjsans1', '부산전문장례식장', '부산전문장례식장'),
    ('kms2784', '면선', '면선'),
    ('kim', '성민이', '성민이'),
    ('quristyle', '이순열', '이순열'),
    ('bkwgoodt', '좋은리버뷰', '좋은리버뷰'),
    ('admin', 'admin', 'admin'),
    ('xg7498', '금사', '금사'),
    ('ctufh', '중앙u병원장례식장', '중앙u병원장례식장'),
    ('dnftkstkawjd', '울산삼정', '울산삼정'),
    ('gkstj', '수영한서병원장례식장', '수영한서병원장례식장'),
    ('jys', '유승이', '유승이'),
    ('cinme', '우정이', '우정이')
) AS v(user_id, user_name, real_name)
WHERE NOT EXISTS (
    SELECT 1 FROM scom.accounts a WHERE a.user_id = v.user_id
);

-- 3. scom.account_profile_details 테이블에 활성 상태(Status=ACTIVE) 등록
INSERT INTO scom.account_profile_details (
    id,
    account_id,
    detail_type,
    content,
    is_primary,
    is_deleted,
    created_at,
    created_by,
    updated_at,
    updated_by
)
SELECT
    gen_random_uuid()::text,
    a.id,
    'Status',
    'ACTIVE',
    true,
    false,
    now(),
    'system',
    now(),
    'system'
FROM scom.accounts a
WHERE a.user_id IN (
    'soquri', 'whgdmsrkddks', 'pnuh7161', 'gosin', 'goehd', 
    'skacjs', 'pinkles23', 'jyh', 'wnfPtkatjs', 'administrator', 
    'soofh', 'ckrgkswkdfP', 'tkgkrnals', 'qntkswjsans1', 'kms2784', 
    'kim', 'quristyle', 'bkwgoodt', 'admin', 'xg7498', 
    'ctufh', 'dnftkstkawjd', 'gkstj', 'jys', 'cinme'
)
AND NOT EXISTS (
    SELECT 1 FROM scom.account_profile_details d 
    WHERE d.account_id = a.id AND d.detail_type = 'Status'
);

-- 4. scom.account_profile_details 테이블에 기본 홈경로(HomePath=/workspace) 등록
INSERT INTO scom.account_profile_details (
    id,
    account_id,
    detail_type,
    content,
    is_primary,
    is_deleted,
    created_at,
    created_by,
    updated_at,
    updated_by
)
SELECT
    gen_random_uuid()::text,
    a.id,
    'HomePath',
    '/workspace',
    true,
    false,
    now(),
    'system',
    now(),
    'system'
FROM scom.accounts a
WHERE a.user_id IN (
    'soquri', 'whgdmsrkddks', 'pnuh7161', 'gosin', 'goehd', 
    'skacjs', 'pinkles23', 'jyh', 'wnfPtkatjs', 'administrator', 
    'soofh', 'ckrgkswkdfP', 'tkgkrnals', 'qntkswjsans1', 'kms2784', 
    'kim', 'quristyle', 'bkwgoodt', 'admin', 'xg7498', 
    'ctufh', 'dnftkstkawjd', 'gkstj', 'jys', 'cinme'
)
AND NOT EXISTS (
    SELECT 1 FROM scom.account_profile_details d 
    WHERE d.account_id = a.id AND d.detail_type = 'HomePath'
);
