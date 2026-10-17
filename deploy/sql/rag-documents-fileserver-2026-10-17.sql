-- RAG 문서의 실물을 파일 서버로 옮긴다 (2026-10-17)
--
-- [왜]
--
-- 올린 파일을 AIAgentServer 가 제 볼륨(/srv/jsini/rag/docs)에 직접 쓰고 있었다.
-- 요건("문서는 운영시스템에 보관한다")은 채워졌지만 **파일은 파일 서버를
-- 거친다**는 이 시스템의 규칙을 어겼고, 값을 치렀다 —
--
--   · 볼륨이 둘로 갈려 백업·용량 관리가 둘이 되었다
--   · **내려받기가 아예 안 됐다.** 포털은 브라우저에 토큰을 안 내리므로(BFF)
--     첨부는 셸이 중계하는데, 그 중계는 파일 서버의 아이디를 받게 되어 있어
--     RAG 문서만 낄 자리가 없었다
--
-- [무엇을]
--
-- `file_id` 한 칸을 더한다. 옛 `stored_path` 는 **남겨 둔다** — 이사가 중간에
-- 멈춰도 그 줄은 옛 길로 읽히게 하려는 것이다(RagLibrary 가 file_id 를 먼저
-- 보고 없으면 stored_path 로 떨어진다). 이사가 끝나고 한동안 멀쩡하면 그때 뗀다.

ALTER TABLE scom.rag_documents
    ADD COLUMN IF NOT EXISTS file_id uuid;

COMMENT ON COLUMN scom.rag_documents.file_id IS
    '파일 서버가 붙인 아이디. 비어 있으면 아직 안 옮긴 줄이라 stored_path 로 읽는다.';

-- 옮긴 줄을 세어 보는 자리.
--   SELECT count(*) FILTER (WHERE file_id IS NOT NULL) AS 옮김,
--          count(*) FILTER (WHERE file_id IS NULL)     AS 남음
--   FROM scom.rag_documents WHERE NOT is_deleted;
