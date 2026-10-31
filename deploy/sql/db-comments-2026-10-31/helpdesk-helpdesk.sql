-- DB helpdesk · 스키마 helpdesk (HelpDeskServer)
-- 코멘트가 비어 있던 표 4개 · 칸 265개에 코멘트를 단다. 이미 있던 코멘트는 건드리지 않는다.
BEGIN;

COMMENT ON TABLE helpdesk."__EFMigrationsHistory" IS 'EF Core 마이그레이션 적용 이력';
COMMENT ON COLUMN helpdesk."__EFMigrationsHistory"."MigrationId" IS '적용된 마이그레이션 ID';
COMMENT ON COLUMN helpdesk."__EFMigrationsHistory"."ProductVersion" IS '적용한 EF Core 버전';

COMMENT ON COLUMN helpdesk.admin.id IS 'PK';
COMMENT ON COLUMN helpdesk.admin.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.admin.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.admin.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.admin.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.admin.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.admin.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.admin.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';
COMMENT ON COLUMN helpdesk.admin.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';

COMMENT ON COLUMN helpdesk.approle.id IS 'PK';
COMMENT ON COLUMN helpdesk.approle.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.approle.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.approle.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.approle.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.approle.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.approle.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.approle.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.approle.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';

COMMENT ON COLUMN helpdesk.appuserrole.id IS 'PK';
COMMENT ON COLUMN helpdesk.appuserrole.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.appuserrole.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.appuserrole.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.appuserrole.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.appuserrole.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.appuserrole.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.appuserrole.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.appuserrole.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';

COMMENT ON COLUMN helpdesk.attachment.id IS 'PK';
COMMENT ON COLUMN helpdesk.attachment.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.attachment.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.attachment.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.attachment.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.attachment.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.attachment.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.attachment.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';
COMMENT ON COLUMN helpdesk.attachment.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';

COMMENT ON COLUMN helpdesk.auth_user_links.id IS '매핑 식별자';
COMMENT ON COLUMN helpdesk.auth_user_links.authuserid IS 'AuthServer 계정 식별자 (scom.accounts.user_id, JWT NameIdentifier)';
COMMENT ON COLUMN helpdesk.auth_user_links.usertype IS '헬프데스크 계정 종류: admin·customer';
COMMENT ON COLUMN helpdesk.auth_user_links.helpdeskuserid IS '헬프데스크 내부 계정 ID (admin.id 또는 customer.id)';
COMMENT ON COLUMN helpdesk.auth_user_links.createdat IS '매핑 생성 일시(UTC)';
COMMENT ON COLUMN helpdesk.auth_user_links.createdby IS '매핑을 만든 주체';

COMMENT ON TABLE helpdesk.binarysample IS '장비 모델별 바이너리 전문 파서 샘플';
COMMENT ON COLUMN helpdesk.binarysample.id IS 'PK';
COMMENT ON COLUMN helpdesk.binarysample.mc_modelsid IS '장비 모델 ID (mc_models.id 참조)';
COMMENT ON COLUMN helpdesk.binarysample.title IS '샘플 제목';
COMMENT ON COLUMN helpdesk.binarysample.content IS '샘플 전문 내용';
COMMENT ON COLUMN helpdesk.binarysample.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.binarysample.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.binarysample.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.binarysample.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.binarysample.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.binarysample.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.binarysample.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.binarysample.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';

COMMENT ON COLUMN helpdesk.customer.id IS 'PK';
COMMENT ON COLUMN helpdesk.customer.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.customer.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.customer.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.customer.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.customer.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.customer.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.customer.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';
COMMENT ON COLUMN helpdesk.customer.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.customer.lockoutend IS '계정 잠금 종료 시간(UTC)';
COMMENT ON COLUMN helpdesk.customer.failedloginattempts IS '로그인실패횟수';

COMMENT ON COLUMN helpdesk.improvementcomment.id IS 'PK';
COMMENT ON COLUMN helpdesk.improvementcomment.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.improvementcomment.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.improvementcomment.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.improvementcomment.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.improvementcomment.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.improvementcomment.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.improvementcomment.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';
COMMENT ON COLUMN helpdesk.improvementcomment.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';

COMMENT ON COLUMN helpdesk.improvementrequest.id IS 'PK';
COMMENT ON COLUMN helpdesk.improvementrequest.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.improvementrequest.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.improvementrequest.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.improvementrequest.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.improvementrequest.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.improvementrequest.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.improvementrequest.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';
COMMENT ON COLUMN helpdesk.improvementrequest.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.improvementrequest.mainphoto IS '본문의 대표 사진';

COMMENT ON TABLE helpdesk.mc_ack_finds IS '장비 모델별 끊어진 ACK 전문을 찾는 시작·종료 조건';
COMMENT ON COLUMN helpdesk.mc_ack_finds.id IS 'PK';
COMMENT ON COLUMN helpdesk.mc_ack_finds.mc_modelsid IS '장비 모델 ID (mc_models.id 참조)';
COMMENT ON COLUMN helpdesk.mc_ack_finds.startcalcarrow IS '시작 조건 방향: up·down';
COMMENT ON COLUMN helpdesk.mc_ack_finds.startcalctarget IS '시작 조건 대상 전문: RX·TX';
COMMENT ON COLUMN helpdesk.mc_ack_finds.startcalcidx IS '시작 조건 비교 위치';
COMMENT ON COLUMN helpdesk.mc_ack_finds.startcalcvalue IS '시작 조건 비교 값';
COMMENT ON COLUMN helpdesk.mc_ack_finds.startcalcequals IS '시작 조건 비교 방식: equal·not';
COMMENT ON COLUMN helpdesk.mc_ack_finds.endcalcarrow IS '종료 조건 방향: up·down';
COMMENT ON COLUMN helpdesk.mc_ack_finds.endcalctarget IS '종료 조건 대상 전문: RX·TX';
COMMENT ON COLUMN helpdesk.mc_ack_finds.endcalcidx IS '종료 조건 비교 위치';
COMMENT ON COLUMN helpdesk.mc_ack_finds.endcalcvalue IS '종료 조건 비교 값';
COMMENT ON COLUMN helpdesk.mc_ack_finds.endcalcequals IS '종료 조건 비교 방식: equal·not';

COMMENT ON COLUMN helpdesk.mc_models.id IS 'PK';
COMMENT ON COLUMN helpdesk.mc_models.mc_name IS '장비 모델명';
COMMENT ON COLUMN helpdesk.mc_models.startkey IS '전문 시작 코드값 (예: AA 55)';

COMMENT ON COLUMN helpdesk.menu.id IS 'PK';
COMMENT ON COLUMN helpdesk.menu.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.menu.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.menu.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.menu.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.menu.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.menu.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.menu.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.menu.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';
COMMENT ON COLUMN helpdesk.menu.ext1name IS '확장 권한 1 이름';
COMMENT ON COLUMN helpdesk.menu.ext2name IS '확장 권한 2 이름';
COMMENT ON COLUMN helpdesk.menu.ext3name IS '확장 권한 3 이름';
COMMENT ON COLUMN helpdesk.menu.ext4name IS '확장 권한 4 이름';
COMMENT ON COLUMN helpdesk.menu.ext5name IS '확장 권한 5 이름';
COMMENT ON COLUMN helpdesk.menu.ext6name IS '확장 권한 6 이름';
COMMENT ON COLUMN helpdesk.menu.ext7name IS '확장 권한 7 이름';
COMMENT ON COLUMN helpdesk.menu.ext8name IS '확장 권한 8 이름';
COMMENT ON COLUMN helpdesk.menu.usecreate IS '생성 권한 사용 여부';
COMMENT ON COLUMN helpdesk.menu.usedelete IS '삭제 권한 사용 여부';
COMMENT ON COLUMN helpdesk.menu.useext1 IS '확장 권한 1 사용 여부';
COMMENT ON COLUMN helpdesk.menu.useext2 IS '확장 권한 2 사용 여부';
COMMENT ON COLUMN helpdesk.menu.useext3 IS '확장 권한 3 사용 여부';
COMMENT ON COLUMN helpdesk.menu.useext4 IS '확장 권한 4 사용 여부';
COMMENT ON COLUMN helpdesk.menu.useext5 IS '확장 권한 5 사용 여부';
COMMENT ON COLUMN helpdesk.menu.useext6 IS '확장 권한 6 사용 여부';
COMMENT ON COLUMN helpdesk.menu.useext7 IS '확장 권한 7 사용 여부';
COMMENT ON COLUMN helpdesk.menu.useext8 IS '확장 권한 8 사용 여부';
COMMENT ON COLUMN helpdesk.menu.useread IS '조회 권한 사용 여부';
COMMENT ON COLUMN helpdesk.menu.useupdate IS '수정 권한 사용 여부';

COMMENT ON COLUMN helpdesk.menurole.id IS 'PK';
COMMENT ON COLUMN helpdesk.menurole.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.menurole.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.menurole.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.menurole.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.menurole.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.menurole.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.menurole.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.menurole.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';

COMMENT ON COLUMN helpdesk.notice.id IS 'PK';
COMMENT ON COLUMN helpdesk.notice.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.notice.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.notice.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.notice.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.notice.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.notice.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.notice.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';
COMMENT ON COLUMN helpdesk.notice.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';

COMMENT ON COLUMN helpdesk.parse_items.id IS 'PK';
COMMENT ON COLUMN helpdesk.parse_items.mc_modelsid IS '장비 모델 ID (mc_models.id 참조)';
COMMENT ON COLUMN helpdesk.parse_items.desc IS '전문 설명';
COMMENT ON COLUMN helpdesk.parse_items.ptype IS '전문 구분: RX(수신)·TX(송신)';
COMMENT ON COLUMN helpdesk.parse_items.keyidx IS '키 바이트 시작 위치 (0부터)';
COMMENT ON COLUMN helpdesk.parse_items.keys IS '전문을 가려내는 키 바이트열';
COMMENT ON COLUMN helpdesk.parse_items.blocparsetype IS '블록 분석 방식: number·date';
COMMENT ON COLUMN helpdesk.parse_items.blocparselength IS '블록 분석 길이(바이트, 예: 8 또는 4,2,1,1)';

COMMENT ON COLUMN helpdesk.project.id IS 'PK';
COMMENT ON COLUMN helpdesk.project.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.project.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.project.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.project.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.project.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.project.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.project.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.project.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';

COMMENT ON COLUMN helpdesk.pushmessage.id IS 'PK';
COMMENT ON COLUMN helpdesk.pushmessage.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.pushmessage.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.pushmessage.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.pushmessage.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.pushmessage.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.pushmessage.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.pushmessage.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.pushmessage.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';

COMMENT ON COLUMN helpdesk.pushmessagerecipient.id IS 'PK';
COMMENT ON COLUMN helpdesk.pushmessagerecipient.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.pushmessagerecipient.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.pushmessagerecipient.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.pushmessagerecipient.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.pushmessagerecipient.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.pushmessagerecipient.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.pushmessagerecipient.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.pushmessagerecipient.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';

COMMENT ON COLUMN helpdesk.pushnotificationlog.id IS 'PK';
COMMENT ON COLUMN helpdesk.pushnotificationlog.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.pushnotificationlog.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.pushnotificationlog.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.pushnotificationlog.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.pushnotificationlog.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.pushnotificationlog.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.pushnotificationlog.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.pushnotificationlog.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';

COMMENT ON TABLE helpdesk.pushsubscriptions IS 'Web Push 구독 정보';

COMMENT ON COLUMN helpdesk.rolemenupermission.id IS 'PK';
COMMENT ON COLUMN helpdesk.rolemenupermission.roleid IS '권한 그룹 ID (approle.id 참조)';
COMMENT ON COLUMN helpdesk.rolemenupermission.menuid IS '메뉴 ID (menu.id 참조)';
COMMENT ON COLUMN helpdesk.rolemenupermission.cancreate IS '생성 권한 여부';
COMMENT ON COLUMN helpdesk.rolemenupermission.canread IS '조회 권한 여부';
COMMENT ON COLUMN helpdesk.rolemenupermission.canupdate IS '수정 권한 여부';
COMMENT ON COLUMN helpdesk.rolemenupermission.candelete IS '삭제 권한 여부';
COMMENT ON COLUMN helpdesk.rolemenupermission.ext1 IS '확장 권한 1 여부';
COMMENT ON COLUMN helpdesk.rolemenupermission.ext2 IS '확장 권한 2 여부';
COMMENT ON COLUMN helpdesk.rolemenupermission.ext3 IS '확장 권한 3 여부';
COMMENT ON COLUMN helpdesk.rolemenupermission.ext4 IS '확장 권한 4 여부';
COMMENT ON COLUMN helpdesk.rolemenupermission.ext5 IS '확장 권한 5 여부';
COMMENT ON COLUMN helpdesk.rolemenupermission.ext6 IS '확장 권한 6 여부';
COMMENT ON COLUMN helpdesk.rolemenupermission.ext7 IS '확장 권한 7 여부';
COMMENT ON COLUMN helpdesk.rolemenupermission.ext8 IS '확장 권한 8 여부';
COMMENT ON COLUMN helpdesk.rolemenupermission.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.rolemenupermission.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.rolemenupermission.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.rolemenupermission.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.rolemenupermission.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.rolemenupermission.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.rolemenupermission.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.rolemenupermission.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';

COMMENT ON COLUMN helpdesk.schedules.id IS '일정 식별자(UUID)';
COMMENT ON COLUMN helpdesk.schedules.title IS '일정 제목';
COMMENT ON COLUMN helpdesk.schedules.description IS '일정 설명';
COMMENT ON COLUMN helpdesk.schedules.startdate IS '시작 일시(UTC)';
COMMENT ON COLUMN helpdesk.schedules.enddate IS '종료 일시(UTC)';
COMMENT ON COLUMN helpdesk.schedules.createdat IS '생성 일시(UTC)';
COMMENT ON COLUMN helpdesk.schedules.updatedat IS '수정 일시(UTC)';

COMMENT ON COLUMN helpdesk.tag_items.id IS 'PK';
COMMENT ON COLUMN helpdesk.tag_items.parseitemid IS '전문 ID (parse_items.id 참조)';
COMMENT ON COLUMN helpdesk.tag_items.desc IS '태그 설명';
COMMENT ON COLUMN helpdesk.tag_items.tagidx IS '태그 시작 바이트 위치 (0부터)';
COMMENT ON COLUMN helpdesk.tag_items.taglength IS '태그 길이(바이트)';
COMMENT ON COLUMN helpdesk.tag_items.datatype IS '데이터 형식(DataTypeEnum 순번): 0 NUMBER·1 DATE·2 DATETIME·3 LENGTH·4 DESTINATION·5 SOURCE·6 CONTROL·7 SUB_APP·8 REQUEST_CODE·9 RESPONSE_CODE·10 APP_CODE·11 DATA·12 DATA_SINGLE·13 ENERGY_LIMIT';
COMMENT ON COLUMN helpdesk.tag_items.sortno IS '정렬 순서';

COMMENT ON COLUMN helpdesk.team.id IS 'PK';
COMMENT ON COLUMN helpdesk.team.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.team.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.team.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.team.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.team.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.team.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.team.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';
COMMENT ON COLUMN helpdesk.team.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';

COMMENT ON COLUMN helpdesk.wbs.id IS 'PK';
COMMENT ON COLUMN helpdesk.wbs.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.wbs.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.wbs.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.wbs.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.wbs.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.wbs.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.wbs.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.wbs.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';

COMMENT ON COLUMN helpdesk.wbsdiagram.id IS 'PK';
COMMENT ON COLUMN helpdesk.wbsdiagram.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.wbsdiagram.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.wbsdiagram.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.wbsdiagram.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.wbsdiagram.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.wbsdiagram.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.wbsdiagram.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.wbsdiagram.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';

COMMENT ON COLUMN helpdesk.wbslink.id IS 'PK';
COMMENT ON COLUMN helpdesk.wbslink.createdby IS '생성자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.wbslink.createdat IS '생성일시(UTC)';
COMMENT ON COLUMN helpdesk.wbslink.modifiedby IS '수정자 (로그인 ID)';
COMMENT ON COLUMN helpdesk.wbslink.modifiedat IS '수정일시(UTC)';
COMMENT ON COLUMN helpdesk.wbslink.actionservice IS '작업 서비스 (X-Service-Name 헤더)';
COMMENT ON COLUMN helpdesk.wbslink.menucontext IS '작업 메뉴/화면 정보 (X-Menu-Name 헤더, 없으면 사용자)';
COMMENT ON COLUMN helpdesk.wbslink.remoteaddr IS '접근아이피 (X-Forwarded-For 첫 값)';
COMMENT ON COLUMN helpdesk.wbslink.remotemchineinfo IS '접근 클라이언트 정보 (User-Agent 등)';

COMMIT;
