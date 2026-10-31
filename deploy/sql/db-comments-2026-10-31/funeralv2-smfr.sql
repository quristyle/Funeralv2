-- DB funeralv2 · 스키마 smfr (장례식장 funeralv2Api)
-- 코멘트가 비어 있던 표 1개 · 칸 68개에 코멘트를 단다. 이미 있던 코멘트는 건드리지 않는다.
BEGIN;

COMMENT ON TABLE smfr."__EFMigrationsHistory" IS 'EF Core 마이그레이션 적용 이력 (funeralv2Api)';
COMMENT ON COLUMN smfr."__EFMigrationsHistory"."MigrationId" IS '적용된 마이그레이션 ID (타임스탬프_이름)';
COMMENT ON COLUMN smfr."__EFMigrationsHistory"."ProductVersion" IS '마이그레이션을 적용한 EF Core 버전';

COMMENT ON COLUMN smfr.account_settings.id IS '설정 행 식별자 (GUID, 하이픈 없음)';
COMMENT ON COLUMN smfr.account_settings.created_at IS '생성 일시(UTC)';
COMMENT ON COLUMN smfr.account_settings.created_by IS '생성자 ID (X-User-Id)';
COMMENT ON COLUMN smfr.account_settings.updated_at IS '수정 일시(UTC)';
COMMENT ON COLUMN smfr.account_settings.updated_by IS '수정자 ID (X-User-Id)';
COMMENT ON COLUMN smfr.account_settings.is_deleted IS '삭제 여부 (소프트 삭제)';

COMMENT ON COLUMN smfr.building_music.id IS '배정 행 식별자 (GUID, 하이픈 없음)';
COMMENT ON COLUMN smfr.building_music.created_at IS '생성 일시(UTC)';
COMMENT ON COLUMN smfr.building_music.created_by IS '생성자 ID (X-User-Id)';
COMMENT ON COLUMN smfr.building_music.updated_at IS '수정 일시(UTC)';
COMMENT ON COLUMN smfr.building_music.updated_by IS '수정자 ID (X-User-Id)';
COMMENT ON COLUMN smfr.building_music.is_deleted IS '삭제 여부 (소프트 삭제)';

COMMENT ON COLUMN smfr.buildings.id IS '건물 식별자 (GUID)';
COMMENT ON COLUMN smfr.buildings.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN smfr.buildings.createdby IS '생성자 ID';
COMMENT ON COLUMN smfr.buildings.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN smfr.buildings.updatedby IS '수정자 ID';
COMMENT ON COLUMN smfr.buildings.isdeleted IS '삭제 여부 (소프트 삭제)';

COMMENT ON COLUMN smfr.device_attributes.id IS '장비 속성 식별자 (GUID)';
COMMENT ON COLUMN smfr.device_attributes.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN smfr.device_attributes.createdby IS '생성자 ID';
COMMENT ON COLUMN smfr.device_attributes.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN smfr.device_attributes.updatedby IS '수정자 ID';
COMMENT ON COLUMN smfr.device_attributes.isdeleted IS '삭제 여부 (소프트 삭제)';

COMMENT ON COLUMN smfr.device_configs.id IS '장비 설정 식별자 (GUID)';
COMMENT ON COLUMN smfr.device_configs.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN smfr.device_configs.createdby IS '생성자 ID';
COMMENT ON COLUMN smfr.device_configs.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN smfr.device_configs.updatedby IS '수정자 ID';
COMMENT ON COLUMN smfr.device_configs.isdeleted IS '삭제 여부 (소프트 삭제)';

COMMENT ON COLUMN smfr.device_ribbons.id IS '리본 설정 식별자 (GUID)';
COMMENT ON COLUMN smfr.device_ribbons.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN smfr.device_ribbons.createdby IS '생성자 ID';
COMMENT ON COLUMN smfr.device_ribbons.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN smfr.device_ribbons.updatedby IS '수정자 ID';
COMMENT ON COLUMN smfr.device_ribbons.isdeleted IS '삭제 여부 (소프트 삭제)';

COMMENT ON COLUMN smfr.device_text_overlays.id IS '텍스트 오버레이 식별자 (GUID)';
COMMENT ON COLUMN smfr.device_text_overlays.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN smfr.device_text_overlays.createdby IS '생성자 ID';
COMMENT ON COLUMN smfr.device_text_overlays.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN smfr.device_text_overlays.updatedby IS '수정자 ID';
COMMENT ON COLUMN smfr.device_text_overlays.isdeleted IS '삭제 여부 (소프트 삭제)';

COMMENT ON COLUMN smfr.devices.id IS '장비 식별자 (GUID)';
COMMENT ON COLUMN smfr.devices.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN smfr.devices.createdby IS '생성자 ID';
COMMENT ON COLUMN smfr.devices.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN smfr.devices.updatedby IS '수정자 ID';
COMMENT ON COLUMN smfr.devices.isdeleted IS '삭제 여부 (소프트 삭제)';

COMMENT ON COLUMN smfr.floors.id IS '층 식별자 (GUID)';
COMMENT ON COLUMN smfr.floors.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN smfr.floors.createdby IS '생성자 ID';
COMMENT ON COLUMN smfr.floors.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN smfr.floors.updatedby IS '수정자 ID';
COMMENT ON COLUMN smfr.floors.isdeleted IS '삭제 여부 (소프트 삭제)';

COMMENT ON COLUMN smfr.media_sources.id IS '미디어 소스 식별자 (GUID, 하이픈 없음)';
COMMENT ON COLUMN smfr.media_sources.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN smfr.media_sources.createdby IS '생성자 ID (서비스가 넣을 때는 System)';
COMMENT ON COLUMN smfr.media_sources.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN smfr.media_sources.updatedby IS '수정자 ID (서비스가 고칠 때는 System)';
COMMENT ON COLUMN smfr.media_sources.isdeleted IS '삭제 여부 (소프트 삭제)';

COMMENT ON COLUMN smfr.rooms.id IS '호실 식별자 (GUID)';
COMMENT ON COLUMN smfr.rooms.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN smfr.rooms.createdby IS '생성자 ID';
COMMENT ON COLUMN smfr.rooms.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN smfr.rooms.updatedby IS '수정자 ID';
COMMENT ON COLUMN smfr.rooms.isdeleted IS '삭제 여부 (소프트 삭제)';

COMMIT;
