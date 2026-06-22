CREATE TABLE IF NOT EXISTS scom.biz_select_configs (
    id VARCHAR(50) PRIMARY KEY,
    biz_type VARCHAR(50) UNIQUE NOT NULL,
    api_url VARCHAR(255) NOT NULL,
    http_method VARCHAR(10) DEFAULT 'GET' NOT NULL,
    label_field VARCHAR(50) NOT NULL,
    value_field VARCHAR(50) NOT NULL,
    result_path VARCHAR(100),
    processor_type VARCHAR(50),
    remark VARCHAR(255),
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    created_by VARCHAR(50) NOT NULL,
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL,
    updated_by VARCHAR(50) NOT NULL
);

-- 초기 기초 데이터 주입 (company, dept)
INSERT INTO scom.biz_select_configs (id, biz_type, api_url, http_method, label_field, value_field, result_path, processor_type, remark, created_at, created_by, updated_at, updated_by)
VALUES 
('1', 'company', '/auth/system/companies', 'GET', 'name', 'id', 'result', NULL, '회사 목록', NOW(), 'System', NOW(), 'System'),
('2', 'dept', '/auth/system/dept/list', 'GET', 'name', 'id', NULL, 'FLATTEN', '부서 목록', NOW(), 'System', NOW(), 'System')
ON CONFLICT (biz_type) DO NOTHING;
