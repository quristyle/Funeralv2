-- ============================================================
-- RAG 문서 카테고리 회사별 관리 — scom.rag_categories.company_id 추가 (2026-10-18)
-- ============================================================
--
-- 화면: web/src/Apps/JSini.Web.Admin/Components/Pages/RagDocuments.razor
-- 서버: microservices/AIAgentServer/Services/Rag/RagLibrary.cs
--
-- 목적:
--   /admin/ai/rag-documents 화면에서 회사별로 카테고리를 분리 관리하고,
--   사용자가 본인 소속회사의 카테고리만 관리/제어할 수 있도록 회사 ID 컬럼 및 외래키 추가.
--
-- 실행:
--   psql -h 127.0.0.1 -p 31015 -U funeralv2 -d jsiniportal -f rag-categories-company-2026-10-18.sql
--
-- 멱등성: 여러 번 실행해도 안전하도록 작성됨.

-- 1. company_id 컬럼 추가
ALTER TABLE scom.rag_categories
    ADD COLUMN IF NOT EXISTS company_id text;

-- 2. FK 제약 조건 추가
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fk_rag_categories_company'
          AND conrelid = 'scom.rag_categories'::regclass
    ) THEN
        ALTER TABLE scom.rag_categories
            ADD CONSTRAINT fk_rag_categories_company
            FOREIGN KEY (company_id) REFERENCES scom.companies(id)
            ON DELETE SET NULL;
    END IF;
END $$;

-- 3. 검색 인덱스 추가
CREATE INDEX IF NOT EXISTS ix_rag_categories_company
    ON scom.rag_categories (company_id);

-- 4. 기존 카테고리 초기 데이터 매핑 (비어있는 경우에만 업데이트)
-- jinnets 카테고리 -> 진네트웍스 (8e032b13-8fa0-47ce-a0c6-55cfef3165ce)
UPDATE scom.rag_categories
SET company_id = '8e032b13-8fa0-47ce-a0c6-55cfef3165ce'
WHERE id = 'jinnets' AND (company_id IS NULL OR company_id = '');

-- hanju 카테고리 -> 한주유틸리티 (6de05d88-884b-4043-8485-b78fbefdc0a7)
UPDATE scom.rag_categories
SET company_id = '6de05d88-884b-4043-8485-b78fbefdc0a7'
WHERE id = 'hanju' AND (company_id IS NULL OR company_id = '');

-- default, hr-policy 카테고리 -> 준 시스템 (jsini)
UPDATE scom.rag_categories
SET company_id = 'jsini'
WHERE id IN ('default', 'hr-policy') AND (company_id IS NULL OR company_id = '');
