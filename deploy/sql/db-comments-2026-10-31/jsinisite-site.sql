-- DB jsinisite · 스키마 site (회사 소개 사이트 SiteServer)
-- 코멘트가 비어 있던 표 0개 · 칸 64개에 코멘트를 단다. 이미 있던 코멘트는 건드리지 않는다.
BEGIN;

COMMENT ON COLUMN site.downloads.id IS '자료 아이디';
COMMENT ON COLUMN site.downloads.locale IS '언어: ko · en';
COMMENT ON COLUMN site.downloads.title IS '자료명';
COMMENT ON COLUMN site.downloads.description IS '설명 한두 줄';
COMMENT ON COLUMN site.downloads.category IS '분류(예: 회사소개 · 제품). 화면의 탭이 된다';
COMMENT ON COLUMN site.downloads.filename IS '목록에 보여 줄 파일명. FileServer 원본명과 달라도 된다';
COMMENT ON COLUMN site.downloads.filesize IS '파일 크기(바이트). 목록 표시용 복사본';
COMMENT ON COLUMN site.downloads.downloadcount IS '내려받은 횟수';
COMMENT ON COLUMN site.downloads.sortorder IS '목록 순서. 작은 값이 위';
COMMENT ON COLUMN site.downloads.ispublished IS '공개 여부. 끄면 목록과 내려받기 모두 막힌다';
COMMENT ON COLUMN site.downloads.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN site.downloads.createdby IS '생성자 아이디';
COMMENT ON COLUMN site.downloads.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN site.downloads.updatedby IS '수정자 아이디';
COMMENT ON COLUMN site.downloads.isdeleted IS '삭제 여부';

COMMENT ON COLUMN site.inquiries.id IS '문의 아이디';
COMMENT ON COLUMN site.inquiries.name IS '보낸 사람 이름';
COMMENT ON COLUMN site.inquiries.company IS '회사명. 선택';
COMMENT ON COLUMN site.inquiries.email IS '이메일. 답을 보낼 곳이라 필수';
COMMENT ON COLUMN site.inquiries.phone IS '전화번호. 선택';
COMMENT ON COLUMN site.inquiries.category IS '문의 분류(예: 영업 · 기술 · 채용)';
COMMENT ON COLUMN site.inquiries.subject IS '제목';
COMMENT ON COLUMN site.inquiries.message IS '본문';
COMMENT ON COLUMN site.inquiries.useragent IS '접수 당시 User-Agent(최대 400자)';
COMMENT ON COLUMN site.inquiries.locale IS '어느 쪽 사이트에서 왔는지: ko · en';
COMMENT ON COLUMN site.inquiries.internalnote IS '담당자 내부 메모·답장 기록. 방문자에게 안 보인다';
COMMENT ON COLUMN site.inquiries.createdat IS '접수 일시(UTC)';
COMMENT ON COLUMN site.inquiries.createdby IS '생성자. 소개 사이트 접수는 PublicSite';
COMMENT ON COLUMN site.inquiries.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN site.inquiries.updatedby IS '수정자 포털 계정 아이디';
COMMENT ON COLUMN site.inquiries.isdeleted IS '삭제 여부';

COMMENT ON COLUMN site.posts.id IS '글 아이디';
COMMENT ON COLUMN site.posts.locale IS '언어: ko · en';
COMMENT ON COLUMN site.posts.title IS '제목';
COMMENT ON COLUMN site.posts.summary IS '목록용 요약. 비우면 본문 앞부분을 쓴다';
COMMENT ON COLUMN site.posts.body IS '본문. 마크다운';
COMMENT ON COLUMN site.posts.publishedat IS '공개 일시(UTC). 이 시각이 지나야 공개 조회에 나온다';
COMMENT ON COLUMN site.posts.ispublished IS '공개 여부';
COMMENT ON COLUMN site.posts.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN site.posts.createdby IS '생성자 아이디';
COMMENT ON COLUMN site.posts.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN site.posts.updatedby IS '수정자 아이디';
COMMENT ON COLUMN site.posts.isdeleted IS '삭제 여부';

COMMENT ON COLUMN site.sections.id IS '문구 블록 아이디';
COMMENT ON COLUMN site.sections.locale IS '언어: ko · en';
COMMENT ON COLUMN site.sections.title IS '블록의 큰 제목';
COMMENT ON COLUMN site.sections.subtitle IS '제목 아래 짧은 한 줄';
COMMENT ON COLUMN site.sections.sortorder IS '같은 화면 안 순서. 작은 값이 위';
COMMENT ON COLUMN site.sections.ispublished IS '공개 여부. 끄면 공개 조회에서 빠진다';
COMMENT ON COLUMN site.sections.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN site.sections.createdby IS '생성자 아이디';
COMMENT ON COLUMN site.sections.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN site.sections.updatedby IS '수정자 아이디';
COMMENT ON COLUMN site.sections.isdeleted IS '삭제 여부';

COMMENT ON COLUMN site.visits.id IS '집계 행 아이디';
COMMENT ON COLUMN site.visits.visitdate IS '집계 날짜(UTC 기준)';
COMMENT ON COLUMN site.visits.path IS '경로(쿼리스트링 제외, 최대 200자. 예: /ko/about)';
COMMENT ON COLUMN site.visits.locale IS '언어: ko · en';
COMMENT ON COLUMN site.visits.viewcount IS '그 날 그 경로의 조회 수';
COMMENT ON COLUMN site.visits.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN site.visits.createdby IS '생성자 아이디';
COMMENT ON COLUMN site.visits.updatedat IS '수정 일시(UTC)';
COMMENT ON COLUMN site.visits.updatedby IS '수정자 아이디';
COMMENT ON COLUMN site.visits.isdeleted IS '삭제 여부';

COMMIT;
