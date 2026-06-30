-- 1. smfr.devices 테이블에 상위 소속 계층 지정을 위한 building_id 및 floor_id 컬럼 추가
-- 기존에 room_id만 매핑되어 있던 구조에서 계층형 매핑으로 확장하기 위함입니다.
ALTER TABLE smfr.devices ADD COLUMN IF NOT EXISTS building_id varchar(50) NULL;
ALTER TABLE smfr.devices ADD COLUMN IF NOT EXISTS floor_id varchar(50) NULL;

-- 2. 기존 데이터 마이그레이션 (역추적)
-- 기존에 room_id가 들어있던 장비들의 상위 층(floor)과 건물(building) 정보를 조인하여 복구합니다.
UPDATE smfr.devices d
SET 
    floor_id = r.floor_id,
    building_id = f.building_id
FROM smfr.rooms r
JOIN smfr.floors f ON r.floor_id = f.id
WHERE d.room_id = r.id
  AND (d.floor_id IS NULL OR d.building_id IS NULL);

-- 3. 외래키 제약조건 추가
-- 데이터의 참조 무결성을 데이터베이스 엔진(PostgreSQL) 레벨에서 강제합니다.
-- 부모 위치 데이터(건물, 층)가 삭제되었을 때의 무결성 충돌을 막기 위해 ON DELETE SET NULL을 적용합니다.

-- building_id 외래키 제약조건 추가
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'fk_devices_building' 
          AND table_schema = 'smfr'
    ) THEN
        ALTER TABLE smfr.devices 
        ADD CONSTRAINT fk_devices_building 
        FOREIGN KEY (building_id) REFERENCES smfr.buildings(id) 
        ON DELETE SET NULL;
    END IF;
END $$;

-- floor_id 외래키 제약조건 추가
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_name = 'fk_devices_floor' 
          AND table_schema = 'smfr'
    ) THEN
        ALTER TABLE smfr.devices 
        ADD CONSTRAINT fk_devices_floor 
        FOREIGN KEY (floor_id) REFERENCES smfr.floors(id) 
        ON DELETE SET NULL;
    END IF;
END $$;
