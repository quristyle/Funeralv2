-- 외부 DB funeralfr · 스키마 index_watch (인덱스 부풀림 감시 도구 pg_index_watch 의 표 — 우리 코드가 쓰지 않는다)
-- 코멘트가 비어 있던 표 5개 · 칸 41개에 코멘트를 단다. 이미 있던 코멘트는 건드리지 않는다.
BEGIN;

COMMENT ON TABLE index_watch.config IS 'pg_index_watch 설정값(전역·DB·스키마·표·인덱스 단위로 덮어씀)';
COMMENT ON COLUMN index_watch.config.id IS '식별자(자동 증가)';
COMMENT ON COLUMN index_watch.config.datname IS '적용 대상 DB 이름(NULL 이면 전체)';
COMMENT ON COLUMN index_watch.config.schemaname IS '적용 대상 스키마 이름(NULL 이면 전체)';
COMMENT ON COLUMN index_watch.config.relname IS '적용 대상 표 이름(NULL 이면 전체)';
COMMENT ON COLUMN index_watch.config.indexrelname IS '적용 대상 인덱스 이름(NULL 이면 전체)';
COMMENT ON COLUMN index_watch.config.key IS '설정 키(예: index_size_threshold, index_rebuild_scale_factor, skip)';
COMMENT ON COLUMN index_watch.config.value IS '설정 값';
COMMENT ON COLUMN index_watch.config.comment IS '설정 설명';

COMMENT ON TABLE index_watch.current_processed_index IS '지금 재색인 중인 인덱스(중단 시 남은 무효 인덱스 정리용)';
COMMENT ON COLUMN index_watch.current_processed_index.id IS '식별자(자동 증가)';
COMMENT ON COLUMN index_watch.current_processed_index.mtime IS '처리 시작 일시';
COMMENT ON COLUMN index_watch.current_processed_index.datname IS 'DB 이름';
COMMENT ON COLUMN index_watch.current_processed_index.schemaname IS '스키마 이름';
COMMENT ON COLUMN index_watch.current_processed_index.relname IS '표 이름';
COMMENT ON COLUMN index_watch.current_processed_index.indexrelname IS '인덱스 이름';

COMMENT ON TABLE index_watch.index_current_state IS '인덱스별 최근 크기·추정 행 수와 최적 비율(부풀림 판정 기준)';
COMMENT ON COLUMN index_watch.index_current_state.id IS '식별자(자동 증가)';
COMMENT ON COLUMN index_watch.index_current_state.mtime IS '기록 일시';
COMMENT ON COLUMN index_watch.index_current_state.indexrelid IS '인덱스 OID';
COMMENT ON COLUMN index_watch.index_current_state.datid IS 'DB OID';
COMMENT ON COLUMN index_watch.index_current_state.datname IS 'DB 이름';
COMMENT ON COLUMN index_watch.index_current_state.schemaname IS '스키마 이름';
COMMENT ON COLUMN index_watch.index_current_state.relname IS '표 이름';
COMMENT ON COLUMN index_watch.index_current_state.indexrelname IS '인덱스 이름';
COMMENT ON COLUMN index_watch.index_current_state.indexsize IS '인덱스 크기(바이트)';
COMMENT ON COLUMN index_watch.index_current_state.indisvalid IS '인덱스 유효 여부';
COMMENT ON COLUMN index_watch.index_current_state.estimated_tuples IS '추정 행 수';
COMMENT ON COLUMN index_watch.index_current_state.best_ratio IS '재색인 직후의 크기/행 비율(부풀림 비교 기준)';

COMMENT ON TABLE index_watch.reindex_history IS 'pg_index_watch 가 수행한 재색인 이력';
COMMENT ON COLUMN index_watch.reindex_history.id IS '식별자(자동 증가)';
COMMENT ON COLUMN index_watch.reindex_history.entry_timestamp IS '기록 일시';
COMMENT ON COLUMN index_watch.reindex_history.indexrelid IS '인덱스 OID';
COMMENT ON COLUMN index_watch.reindex_history.datid IS 'DB OID';
COMMENT ON COLUMN index_watch.reindex_history.datname IS 'DB 이름';
COMMENT ON COLUMN index_watch.reindex_history.schemaname IS '스키마 이름';
COMMENT ON COLUMN index_watch.reindex_history.relname IS '표 이름';
COMMENT ON COLUMN index_watch.reindex_history.indexrelname IS '인덱스 이름';
COMMENT ON COLUMN index_watch.reindex_history.server_version_num IS '재색인 당시 PostgreSQL 판 번호';
COMMENT ON COLUMN index_watch.reindex_history.indexsize_before IS '재색인 전 크기(바이트)';
COMMENT ON COLUMN index_watch.reindex_history.indexsize_after IS '재색인 후 크기(바이트)';
COMMENT ON COLUMN index_watch.reindex_history.estimated_tuples IS '추정 행 수';
COMMENT ON COLUMN index_watch.reindex_history.reindex_duration IS '재색인 걸린 시간';
COMMENT ON COLUMN index_watch.reindex_history.analyze_duration IS 'ANALYZE 걸린 시간';

COMMENT ON TABLE index_watch.tables_version IS 'pg_index_watch 표 구조 판 번호(업그레이드 판정용)';
COMMENT ON COLUMN index_watch.tables_version.version IS '표 구조 판 번호';

COMMIT;
