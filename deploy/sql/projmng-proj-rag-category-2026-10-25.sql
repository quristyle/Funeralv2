-- ============================================================
-- 프로젝트 ↔ RAG 카테고리 연결 — projmng.dev_proj_rag (2026-10-25)
-- ============================================================
--
-- 프로젝트 목록(/projmng/proj/manage)에서 그 프로젝트가 볼 RAG 카테고리를 맨다.
-- 한 프로젝트에 여러 카테고리를 걸 수 있고, 한 카테고리는 여러 프로젝트가 쓴다.
--
-- 열쇠를 외래키로 걸지 않는 까닭이 둘이다:
--   prj_rid  — projmng.dev_proj 에 기본키가 없다(옛 프로시저가 max+1 로 넣던 표).
--   rag_cate_id — 카테고리는 AIAgentServer 의 DB(ai 쪽)에 산다. 서비스가 다르다.
-- 그래서 없는 카테고리를 가리키는 줄이 남을 수 있다 — 화면은 이름을 못 찾은 열쇠를
-- 버리지 않고 열쇠 그대로 보여 준다(지워진 것인지 못 읽은 것인지 사람이 가린다).
--
-- 실행:
--   psql -h 127.0.0.1 -p 31015 -U projmng -d projmng -f deploy/sql/projmng-proj-rag-category-2026-10-25.sql
--

CREATE TABLE IF NOT EXISTS projmng.dev_proj_rag (
    prj_rid     integer      NOT NULL,
    rag_cate_id varchar(100) NOT NULL,
    cre_dt      timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT pk_dev_proj_rag PRIMARY KEY (prj_rid, rag_cate_id)
);

-- 「이 카테고리를 쓰는 프로젝트」로도 거슬러 묻는다.
CREATE INDEX IF NOT EXISTS ix_dev_proj_rag_cate
    ON projmng.dev_proj_rag (rag_cate_id);

COMMENT ON TABLE projmng.dev_proj_rag IS
    '프로젝트가 쓰는 RAG 카테고리. 카테고리 정본은 AIAgentServer 에 있고 여기는 열쇠만 든다.';
COMMENT ON COLUMN projmng.dev_proj_rag.prj_rid IS 'projmng.dev_proj.prj_rid';
COMMENT ON COLUMN projmng.dev_proj_rag.rag_cate_id IS 'AIAgentServer rag_categories.id';
