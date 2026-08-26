-- 공개 공지의 첨부파일을 익명 열람 허용으로 맞춘다 (결정 D-S10)
--
-- 어디에 쓰이나
--   AuthServer/Services/PublicFileSyncService.cs 가 공지를 저장할 때마다 같은 판정을 한다.
--   이 파일은 **이미 있던 공지들을 소급 반영**하려고 한 번 돌리는 것이고,
--   맞추기가 어긋났다고 의심될 때 다시 돌려도 된다.
--
-- 왜 필요한가
--   로그인 전 화면에도 뜨는 공개 공지(GET /api/auth/notices/popup/public)에는 첨부 링크가
--   함께 나간다. 그런데 파일 읽기는 FileServer 가 scom.filemetadatas.ispublic 으로 판정한다
--   (docs/analysis/27-jsini-site-brand.md 5절). 공지만 공개로 두면 그 첨부는 404 가 된다.
--
-- 판정 기준
--   '공개로 설정된 공지에 붙어 있는가' — is_public AND status = 1 AND NOT is_deleted.
--   게시 기간(start_at · end_at)은 보지 않는다. 기간은 아무도 저장을 누르지 않아도 지나가므로
--   반영하려면 주기 작업이 하나 더 필요한데, 얻는 것이 그만한 값이 아니다.
--
-- 남이 켜 둔 것은 끄지 않는다
--   끄는 것은 updated_by 가 'NoticeSync' 인 행뿐이다. 소개 사이트 자료실처럼 다른 이유로
--   공개된 파일을, 공지에 안 붙어 있다는 이유로 닫아 버리면 안 된다.
--
-- notice_files.file_id 는 text 다. uuid 로 파싱되지 않는 값이 있을 수 있어 걸러 낸다.
--
-- 반복 실행해도 안전하다 (바뀔 것이 없으면 0건).

BEGIN;

UPDATE scom.filemetadatas AS f
   SET ispublic  = s.should,
       updatedat = now(),
       updatedby = 'NoticeSync'
  FROM (
        SELECT (nf.file_id)::uuid AS file_id,
               bool_or(n.is_public
                       AND n.status = 1
                       AND NOT n.is_deleted
                       AND NOT nf.is_deleted) AS should
          FROM scom.notice_files nf
          JOIN scom.notices      n ON n.id = nf.notice_id
         WHERE nf.file_id ~* '^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$'
         GROUP BY (nf.file_id)::uuid
       ) AS s
 WHERE f.id = s.file_id
   AND f.ispublic IS DISTINCT FROM s.should
   AND (s.should OR f.updatedby = 'NoticeSync');

COMMIT;

-- 확인 — 공개 공지에 붙은 첨부와 실제 ispublic 이 맞는지
--   SELECT n.title, n.is_public, nf.file_name, f.ispublic, f.updatedby
--     FROM scom.notices n
--     JOIN scom.notice_files nf ON nf.notice_id = n.id AND NOT nf.is_deleted
--     LEFT JOIN scom.filemetadatas f ON f.id::text = nf.file_id
--    WHERE NOT n.is_deleted
--    ORDER BY n.is_public DESC, n.title;
--
--   SELECT count(*) FILTER (WHERE ispublic) AS 공개, count(*) AS 전체
--     FROM scom.filemetadatas WHERE NOT isdeleted;
