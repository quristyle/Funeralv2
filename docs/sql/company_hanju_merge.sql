-- ============================================================
-- 「한주」(헬프데스크 이관) 를 「한주유틸리티」(포털) 로 병합 (Q16 · 2026-09-04)
--
-- 대상 DB: jsiniportal (scom)
-- 반복 실행 안전.
--
-- 배경(19번 문서 Q16): 헬프데스크에서 옮긴 `한주`(remark helpdesk:company:1)와
-- 포털 원주민 `한주유틸리티` 가 나란히 있었다. 이름이 달라 자동 병합하지 않고
-- 결정을 기다렸고, 2026-09-04 사용자 확인: **같은 회사다.**
--
-- 정본 = 한주유틸리티. 근거:
--   · 포털 참조가 전부 이쪽이다 (accounts 10 · departments 3, 이관 행은 0)
--   · 헬프데스크 매핑(remark)은 코드 소비자가 없는 출처 표식이라 옮겨도 안전
--     (전수 검색 — 주석·문서에만 등장)
--
-- 하는 일:
--   1) 남아 있을 수 있는 이관 행 참조를 정본으로 돌린다 (현재 0건이지만 안전하게)
--   2) 헬프데스크 매핑을 정본의 remark 로 옮긴다 (요청 데이터가 참조하는
--      헬프데스크 회사 ID=1 과의 연결 고리를 보존)
--   3) 이관 행을 소프트 삭제하고 병합 기록을 남긴다
-- ============================================================

DO $$
DECLARE
  canonical uuid := '6de05d88-884b-4043-8485-b78fbefdc0a7';  -- 한주유틸리티
  merged    uuid := '491706c5-e009-4e5f-b46f-35280928d9b5';  -- 한주 (helpdesk:company:1)
BEGIN
  -- 1) 참조 재지향 (반복 실행 시 0건 갱신)
  UPDATE scom.accounts                SET company_id = canonical::text WHERE company_id = merged::text;
  UPDATE scom.departments             SET company_id = canonical::text WHERE company_id = merged::text;
  UPDATE scom.company_usage_locations SET company_id = canonical::text WHERE company_id = merged::text;
  -- role_companies 는 (role, company) 짝이 이미 정본에 있으면 중복이 되므로 없는 것만 옮긴다
  UPDATE scom.role_companies rc
     SET company_id = canonical::text
   WHERE rc.company_id = merged::text
     AND NOT EXISTS (
       SELECT 1 FROM scom.role_companies dup
        WHERE dup.role_id = rc.role_id AND dup.company_id = canonical::text);
  DELETE FROM scom.role_companies WHERE company_id = merged::text;

  -- 2) 헬프데스크 매핑을 정본으로 (요청·팀 데이터의 헬프데스크 회사 ID=1 연결 보존)
  UPDATE scom.companies
     SET remark = 'helpdesk:company:1',
         updated_at = now(), updated_by = 'q16-hanju-merge'
   WHERE id = canonical::text
     AND (remark IS NULL OR remark <> 'helpdesk:company:1');

  -- 3) 이관 행 소프트 삭제 + 병합 기록
  UPDATE scom.companies
     SET is_deleted = true,
         remark = 'merged-into:' || canonical || ' (Q16: 한주 = 한주유틸리티, 2026-09-04)',
         updated_at = now(), updated_by = 'q16-hanju-merge'
   WHERE id = merged::text
     AND is_deleted = false;
END $$;

-- 확인:
--   SELECT id, name, remark, is_deleted FROM scom.companies WHERE name LIKE '%한주%';
