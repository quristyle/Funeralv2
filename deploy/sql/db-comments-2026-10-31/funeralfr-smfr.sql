-- 외부 DB funeralfr.jsini.co.kr:15432/funeral · 스키마 smfr (옛 장례식장 시스템, devdbinfo 별칭 funeralfr)
-- 코멘트가 비어 있던 표 30개 · 칸 248개에 코멘트를 단다. 이미 있던 코멘트는 건드리지 않는다.
BEGIN;

COMMENT ON TABLE smfr.t_account IS '옛 시스템 사용자 계정 (현 scom.accounts 로 대체)';
COMMENT ON COLUMN smfr.t_account.a_key IS '계정 키';
COMMENT ON COLUMN smfr.t_account.user_id IS '로그인 아이디';
COMMENT ON COLUMN smfr.t_account.user_nm IS '사용자 이름';
COMMENT ON COLUMN smfr.t_account.pwd IS '비밀번호';
COMMENT ON COLUMN smfr.t_account.login_dt IS '로그인 일시';
COMMENT ON COLUMN smfr.t_account.del_yn IS '삭제 여부 (Y/N)';
COMMENT ON COLUMN smfr.t_account.isadmin IS '관리자 여부';
COMMENT ON COLUMN smfr.t_account.user_email IS '이메일 주소';
COMMENT ON COLUMN smfr.t_account.mail_certification IS '메일 인증 값';
COMMENT ON COLUMN smfr.t_account.c_key IS '소속 회사 (t_company.c_key)';
COMMENT ON COLUMN smfr.t_account.b_key IS '소속 건물 (t_build.b_key)';
COMMENT ON COLUMN smfr.t_account.mail_cert_yn IS '메일 인증 완료 여부';
COMMENT ON COLUMN smfr.t_account.fg_seq IS '첨부 파일 그룹 (t_filegrp.fg_seq)';

COMMENT ON TABLE smfr.t_account_conf IS '옛 계정별 UI·업무 설정 (현 smfr.account_settings 로 대체)';
COMMENT ON COLUMN smfr.t_account_conf.a_key IS '계정 (t_account.a_key)';
COMMENT ON COLUMN smfr.t_account_conf.conf_cd IS '설정 코드 (multy_room_use·auto_goin_name 등, 이름은 t_code)';
COMMENT ON COLUMN smfr.t_account_conf.conf_val IS '설정 값 (Y/N)';
COMMENT ON COLUMN smfr.t_account_conf.cre_dt IS '생성 일시';
COMMENT ON COLUMN smfr.t_account_conf.mod_dt IS '수정 일시';

COMMENT ON TABLE smfr.t_account_favorites IS '옛 계정별 즐겨찾기 메뉴';
COMMENT ON COLUMN smfr.t_account_favorites.a_key IS '계정 (t_account.a_key)';
COMMENT ON COLUMN smfr.t_account_favorites.menu_cd IS '즐겨찾기한 메뉴 코드';
COMMENT ON COLUMN smfr.t_account_favorites.bigo IS '비고';
COMMENT ON COLUMN smfr.t_account_favorites.cre_dt IS '생성 일시';
COMMENT ON COLUMN smfr.t_account_favorites.mod_dt IS '수정 일시';

COMMENT ON TABLE smfr.t_build IS '옛 건물 정보 (현 smfr.buildings 로 대체)';
COMMENT ON COLUMN smfr.t_build.b_key IS '건물 키';
COMMENT ON COLUMN smfr.t_build.nm IS '건물명';
COMMENT ON COLUMN smfr.t_build.addr IS '건물 주소';
COMMENT ON COLUMN smfr.t_build.s_nm IS '건물 약칭';
COMMENT ON COLUMN smfr.t_build.del_yn IS '삭제 여부 (Y/N)';
COMMENT ON COLUMN smfr.t_build.c_key IS '소속 회사 (t_company.c_key)';
COMMENT ON COLUMN smfr.t_build.mo_group IS '영상 그룹';

COMMENT ON TABLE smfr.t_code IS '옛 공통코드 (현 scom.common_codes 로 대체)';
COMMENT ON COLUMN smfr.t_code.cd_seq IS '코드 키';
COMMENT ON COLUMN smfr.t_code.cd_cd IS '코드 값';
COMMENT ON COLUMN smfr.t_code.cd_nm IS '코드명';
COMMENT ON COLUMN smfr.t_code.cd_type IS '코드 유형';
COMMENT ON COLUMN smfr.t_code.pcd_seq IS '상위 코드 (t_code.cd_seq)';
COMMENT ON COLUMN smfr.t_code.cd_desc IS '코드 설명';
COMMENT ON COLUMN smfr.t_code.expired IS '사용 중지 여부';
COMMENT ON COLUMN smfr.t_code.srt_no IS '정렬 순서';

COMMENT ON TABLE smfr.t_company IS '옛 회사 정보 (현 scom.companies 로 대체)';
COMMENT ON COLUMN smfr.t_company.c_key IS '회사 키';
COMMENT ON COLUMN smfr.t_company.nm IS '회사명';
COMMENT ON COLUMN smfr.t_company.s_nm IS '회사 약칭';
COMMENT ON COLUMN smfr.t_company.del_yn IS '삭제 여부 (Y/N)';
COMMENT ON COLUMN smfr.t_company.addr IS '회사 주소';

COMMENT ON TABLE smfr.t_file IS '옛 업로드 파일 (현 FileServer 로 대체)';
COMMENT ON COLUMN smfr.t_file.f_seq IS '파일 키';
COMMENT ON COLUMN smfr.t_file.f_orginal IS '원본 파일명';
COMMENT ON COLUMN smfr.t_file.f_name IS '저장 파일명';
COMMENT ON COLUMN smfr.t_file.f_path IS '저장 경로';
COMMENT ON COLUMN smfr.t_file.fg_seq IS '파일 그룹 (t_filegrp.fg_seq)';
COMMENT ON COLUMN smfr.t_file.f_size IS '파일 크기';
COMMENT ON COLUMN smfr.t_file.isdown IS '다운로드 표시';
COMMENT ON COLUMN smfr.t_file.cre_dt IS '업로드 일시';
COMMENT ON COLUMN smfr.t_file.ismain IS '대표 파일 표시';
COMMENT ON COLUMN smfr.t_file.f_sort IS '정렬 순서';
COMMENT ON COLUMN smfr.t_file.del_flag IS '삭제 여부 (Y/N, 기본 N)';

COMMENT ON TABLE smfr.t_filegrp IS '옛 파일 그룹 — 여러 파일을 한 번호로 묶는다';
COMMENT ON COLUMN smfr.t_filegrp.fg_seq IS '파일 그룹 번호';
COMMENT ON COLUMN smfr.t_filegrp.fg_name IS '파일 그룹 이름';
COMMENT ON COLUMN smfr.t_filegrp.fg_grp_cd IS '파일 그룹 구분 코드';

COMMENT ON TABLE smfr.t_fileremove2_que IS '옛 파일 삭제 대기열 (파일 그룹 번호 문자열)';
COMMENT ON COLUMN smfr.t_fileremove2_que.fg_seq IS '삭제할 파일 그룹 번호 (문자열)';

COMMENT ON TABLE smfr.t_fileremove_que IS '옛 파일 삭제 대기열';
COMMENT ON COLUMN smfr.t_fileremove_que.fg_seq IS '삭제할 파일 그룹 (t_filegrp.fg_seq)';
COMMENT ON COLUMN smfr.t_fileremove_que.f_seq IS '삭제할 파일 (t_file.f_seq)';

COMMENT ON TABLE smfr.t_floor IS '옛 건물 내 층 정보 (현 smfr.floors 로 대체)';
COMMENT ON COLUMN smfr.t_floor.f_key IS '층 키';
COMMENT ON COLUMN smfr.t_floor.nm IS '층 명칭';
COMMENT ON COLUMN smfr.t_floor.b_key IS '소속 건물 (t_build.b_key)';

COMMENT ON TABLE smfr.t_goin IS '옛 고인 정보 (현 smfr.deceaseds 로 대체)';
COMMENT ON COLUMN smfr.t_goin.gi_key IS '고인 키';
COMMENT ON COLUMN smfr.t_goin.nm IS '고인 성명 (호실 등록 시 「자동생성」으로 채워짐)';
COMMENT ON COLUMN smfr.t_goin.sex IS '성별';
COMMENT ON COLUMN smfr.t_goin.age IS '나이';
COMMENT ON COLUMN smfr.t_goin.layout_corpse_dt IS '입실(입관 준비) 일자 (문자열)';
COMMENT ON COLUMN smfr.t_goin.layout_corpse_tm IS '입실(입관 준비) 시각 (문자열)';
COMMENT ON COLUMN smfr.t_goin.borne_out_dt IS '발인 일자 (문자열)';
COMMENT ON COLUMN smfr.t_goin.borne_out_tm IS '발인 시각 (문자열)';
COMMENT ON COLUMN smfr.t_goin.jangji IS '장지';
COMMENT ON COLUMN smfr.t_goin.chulsang IS '출상 여부';
COMMENT ON COLUMN smfr.t_goin.gi_img IS '영정 사진 파일 그룹 번호';
COMMENT ON COLUMN smfr.t_goin.gi_video IS '재생 영상 (t_movie.mo_seq)';
COMMENT ON COLUMN smfr.t_goin.gi_audio IS '재생 음원 (t_music.ms_seq)';
COMMENT ON COLUMN smfr.t_goin.sex_show IS '성별 표시 여부';
COMMENT ON COLUMN smfr.t_goin.bongan IS '봉안';
COMMENT ON COLUMN smfr.t_goin.religion IS '종교';
COMMENT ON COLUMN smfr.t_goin.obituary IS '부고';
COMMENT ON COLUMN smfr.t_goin.jumin IS '주민등록번호';
COMMENT ON COLUMN smfr.t_goin.marriage IS '혼인';
COMMENT ON COLUMN smfr.t_goin.nationanl_merit IS '국가유공자';
COMMENT ON COLUMN smfr.t_goin.recipients_basic IS '기초생활수급';
COMMENT ON COLUMN smfr.t_goin.age_show IS '나이 표시 여부';
COMMENT ON COLUMN smfr.t_goin.addr1 IS '주소';
COMMENT ON COLUMN smfr.t_goin.addr2 IS '상세 주소';
COMMENT ON COLUMN smfr.t_goin.death_dt IS '사망 일시 (문자열)';
COMMENT ON COLUMN smfr.t_goin.death_dt_show IS '사망 일시 표시 여부';
COMMENT ON COLUMN smfr.t_goin.death_pos IS '사망 장소 (1단계)';
COMMENT ON COLUMN smfr.t_goin.death_pos2 IS '사망 장소 (2단계)';
COMMENT ON COLUMN smfr.t_goin.death_pos3 IS '사망 장소 (3단계)';
COMMENT ON COLUMN smfr.t_goin.death_pos4 IS '사망 장소 (4단계)';
COMMENT ON COLUMN smfr.t_goin.death_tp IS '사망 종류';
COMMENT ON COLUMN smfr.t_goin.medical_yn IS '의료 여부';
COMMENT ON COLUMN smfr.t_goin.inspection_yn IS '검안 여부';
COMMENT ON COLUMN smfr.t_goin.paper_grp IS '서류 파일 그룹 번호';
COMMENT ON COLUMN smfr.t_goin.reservation_yn IS '예약(대기) 고인 여부';

COMMENT ON TABLE smfr.t_goin_acct IS '옛 고인 관련 인물(가족 등) 정보';
COMMENT ON COLUMN smfr.t_goin_acct.ga_key IS '관련 인물 키';
COMMENT ON COLUMN smfr.t_goin_acct.gi_key IS '고인 (t_goin.gi_key)';
COMMENT ON COLUMN smfr.t_goin_acct.nm IS '성명';
COMMENT ON COLUMN smfr.t_goin_acct.family_type IS '고인과의 관계';
COMMENT ON COLUMN smfr.t_goin_acct.ga_birth IS '생년월일';
COMMENT ON COLUMN smfr.t_goin_acct.ga_sex IS '성별';
COMMENT ON COLUMN smfr.t_goin_acct.ga_age IS '나이';
COMMENT ON COLUMN smfr.t_goin_acct.ga_tel IS '연락처';
COMMENT ON COLUMN smfr.t_goin_acct.ga_addr1 IS '주소';
COMMENT ON COLUMN smfr.t_goin_acct.ga_addr2 IS '상세 주소';
COMMENT ON COLUMN smfr.t_goin_acct.cre_dt IS '생성 일시';
COMMENT ON COLUMN smfr.t_goin_acct.mod_dt IS '수정 일시';
COMMENT ON COLUMN smfr.t_goin_acct.mod_id IS '수정자 ID';
COMMENT ON COLUMN smfr.t_goin_acct.cre_id IS '생성자 ID';

COMMENT ON TABLE smfr.t_goin_pay IS '옛 고인별 과금 항목 (현 smfr.deceased_facilities 로 대체)';
COMMENT ON COLUMN smfr.t_goin_pay.gp_seq IS '과금 항목 키';
COMMENT ON COLUMN smfr.t_goin_pay.gp_title IS '항목명 (기본료·환경부담금·시설관리비)';
COMMENT ON COLUMN smfr.t_goin_pay.gp_pay IS '단가';
COMMENT ON COLUMN smfr.t_goin_pay.gp_day_apply IS '사용일수를 곱하는 항목인지';
COMMENT ON COLUMN smfr.t_goin_pay.gp_comment IS '비고';
COMMENT ON COLUMN smfr.t_goin_pay.gi_key IS '고인 (t_goin.gi_key)';
COMMENT ON COLUMN smfr.t_goin_pay.r_key IS '호실 (t_room.r_key)';

COMMENT ON TABLE smfr.t_machine IS '옛 재생 장비 (현 smfr.devices 로 대체)';
COMMENT ON COLUMN smfr.t_machine.m_key IS '장비 키';
COMMENT ON COLUMN smfr.t_machine.auth_key IS '장비 인증 키 (미리보기 주소 /client_machine/?key= 에 쓰임)';
COMMENT ON COLUMN smfr.t_machine.m_nm IS '장비명';
COMMENT ON COLUMN smfr.t_machine.m_desc IS '장비 설명';
COMMENT ON COLUMN smfr.t_machine.del_flag IS '삭제 여부';

COMMENT ON TABLE smfr.t_machine_info IS '옛 장비별 속성 키-값 (현 smfr.device_attributes 로 대체)';
COMMENT ON COLUMN smfr.t_machine_info.m_key IS '장비 (t_machine.m_key)';
COMMENT ON COLUMN smfr.t_machine_info.info_gb IS '속성 구분 (ROOM·CPU·SHUTDOWN·VIEW_TYPE·DISP 등)';
COMMENT ON COLUMN smfr.t_machine_info.info_desc IS '속성 설명';
COMMENT ON COLUMN smfr.t_machine_info.info_value IS '속성 값';
COMMENT ON COLUMN smfr.t_machine_info.info_value2 IS '속성 값 2';
COMMENT ON COLUMN smfr.t_machine_info.info_value3 IS '속성 값 3';
COMMENT ON COLUMN smfr.t_machine_info.mi_seq IS '속성 행 키';

COMMENT ON TABLE smfr.t_manager IS '옛 고인별 장례 담당자 (현 smfr.deceased_managers 로 대체)';
COMMENT ON COLUMN smfr.t_manager.tm_key IS '담당자 키';
COMMENT ON COLUMN smfr.t_manager.gi_key IS '고인 (t_goin.gi_key)';
COMMENT ON COLUMN smfr.t_manager.tm_nm IS '담당자명';
COMMENT ON COLUMN smfr.t_manager.tm_men IS '담당 인원';
COMMENT ON COLUMN smfr.t_manager.tm_tel IS '전화번호';
COMMENT ON COLUMN smfr.t_manager.tm_call IS '연락처';
COMMENT ON COLUMN smfr.t_manager.tm_move IS '이동 정보';
COMMENT ON COLUMN smfr.t_manager.tm_bigo IS '비고';
COMMENT ON COLUMN smfr.t_manager.cre_dt IS '생성 일시';
COMMENT ON COLUMN smfr.t_manager.greade IS '직급';
COMMENT ON COLUMN smfr.t_manager.duty IS '직무';
COMMENT ON COLUMN smfr.t_manager.dispatch_tel IS '출동 연락처';

COMMENT ON TABLE smfr.t_menu IS '옛 메뉴 정의 (현 scom.system_menus 로 대체)';
COMMENT ON COLUMN smfr.t_menu.mn_seq IS '메뉴 키';
COMMENT ON COLUMN smfr.t_menu.mn_nm IS '메뉴명';
COMMENT ON COLUMN smfr.t_menu.mn_cd IS '메뉴 코드';
COMMENT ON COLUMN smfr.t_menu.mn_desc IS '메뉴 설명';
COMMENT ON COLUMN smfr.t_menu.cre_dt IS '생성 일시';
COMMENT ON COLUMN smfr.t_menu.mn_name_e IS '메뉴 영문명';
COMMENT ON COLUMN smfr.t_menu.mn_icon IS '메뉴 아이콘';
COMMENT ON COLUMN smfr.t_menu.mn_path IS '화면 경로';
COMMENT ON COLUMN smfr.t_menu.mn_type IS '메뉴 유형';
COMMENT ON COLUMN smfr.t_menu.mn_sort IS '정렬 순서';
COMMENT ON COLUMN smfr.t_menu.pmn_seq IS '상위 메뉴 (t_menu.mn_seq)';
COMMENT ON COLUMN smfr.t_menu.del_flag IS '삭제 여부 (Y/N, 기본 N)';
COMMENT ON COLUMN smfr.t_menu.view_type IS '화면 표시 방식';
COMMENT ON COLUMN smfr.t_menu.param1 IS '화면 인자 1';
COMMENT ON COLUMN smfr.t_menu.param2 IS '화면 인자 2';
COMMENT ON COLUMN smfr.t_menu.param3 IS '화면 인자 3';
COMMENT ON COLUMN smfr.t_menu.use_yn IS '사용 여부';

COMMENT ON TABLE smfr.t_movie IS '옛 영상 자원 (현 smfr.media_sources 로 대체)';
COMMENT ON COLUMN smfr.t_movie.mo_seq IS '영상 키';
COMMENT ON COLUMN smfr.t_movie.mo_nm IS '영상명';
COMMENT ON COLUMN smfr.t_movie.mo_path IS '파일 경로';
COMMENT ON COLUMN smfr.t_movie.mo_size IS '파일 크기';
COMMENT ON COLUMN smfr.t_movie.mo_time IS '재생 시간';
COMMENT ON COLUMN smfr.t_movie.mo_desc IS '영상 설명';
COMMENT ON COLUMN smfr.t_movie.del_flag IS '삭제 여부';
COMMENT ON COLUMN smfr.t_movie.cre_dt IS '생성 일시';
COMMENT ON COLUMN smfr.t_movie.fg_seq IS '파일 그룹 (t_filegrp.fg_seq)';
COMMENT ON COLUMN smfr.t_movie.mo_group IS '영상 그룹';
COMMENT ON COLUMN smfr.t_movie.upt_dt IS '수정 시각 (time 형)';
COMMENT ON COLUMN smfr.t_movie.bottom_gap IS '화면 아래 여백 (기본 0%)';

COMMENT ON TABLE smfr.t_music IS '옛 음원 자원 (현 smfr.media_sources 로 대체)';
COMMENT ON COLUMN smfr.t_music.ms_seq IS '음원 키';
COMMENT ON COLUMN smfr.t_music.ms_nm IS '음원명';
COMMENT ON COLUMN smfr.t_music.ms_size IS '파일 크기';
COMMENT ON COLUMN smfr.t_music.ms_time IS '재생 시간';
COMMENT ON COLUMN smfr.t_music.ms_path IS '파일 경로';
COMMENT ON COLUMN smfr.t_music.ms_desc IS '음원 설명';
COMMENT ON COLUMN smfr.t_music.cre_dt IS '생성 일시';
COMMENT ON COLUMN smfr.t_music.del_flag IS '삭제 여부';
COMMENT ON COLUMN smfr.t_music.is_f_leave IS '퇴실 관련 표시 (용도 미확인)';
COMMENT ON COLUMN smfr.t_music.fg_seq IS '파일 그룹 (t_filegrp.fg_seq)';

COMMENT ON TABLE smfr.t_music_build IS '옛 건물별 음원 배정 (현 smfr.building_music 로 대체)';
COMMENT ON COLUMN smfr.t_music_build.ms_seq IS '음원 (t_music.ms_seq)';
COMMENT ON COLUMN smfr.t_music_build.b_key IS '건물 (t_build.b_key)';

COMMENT ON TABLE smfr.t_notification IS '옛 사용자 알림 (실데이터 0건, 이식하지 않음)';
COMMENT ON COLUMN smfr.t_notification.n_seq IS '알림 키';
COMMENT ON COLUMN smfr.t_notification.n_msg IS '알림 내용';
COMMENT ON COLUMN smfr.t_notification.n_nofi_user IS '받는 사용자';
COMMENT ON COLUMN smfr.t_notification.cre_dt IS '생성 일시';
COMMENT ON COLUMN smfr.t_notification.cre_id IS '생성자 ID';
COMMENT ON COLUMN smfr.t_notification.isread IS '읽음 여부';
COMMENT ON COLUMN smfr.t_notification.readtime IS '읽은 일시';
COMMENT ON COLUMN smfr.t_notification.target_page IS '눌렀을 때 열 화면';
COMMENT ON COLUMN smfr.t_notification.target_param IS '열 화면에 넘길 인자';
COMMENT ON COLUMN smfr.t_notification.msg_type IS '알림 유형';

COMMENT ON TABLE smfr.t_qna IS '옛 문의(Q&A) 글 (현 헬프데스크로 대체)';
COMMENT ON COLUMN smfr.t_qna.q_seq IS '문의 글 키';
COMMENT ON COLUMN smfr.t_qna.q_type IS '문의 유형';
COMMENT ON COLUMN smfr.t_qna.q_title IS '제목';
COMMENT ON COLUMN smfr.t_qna.fg_seq IS '첨부 파일 그룹';
COMMENT ON COLUMN smfr.t_qna.cre_dt IS '작성 일시';
COMMENT ON COLUMN smfr.t_qna.cre_id IS '작성자 ID';
COMMENT ON COLUMN smfr.t_qna.q_cont IS '본문';
COMMENT ON COLUMN smfr.t_qna.pq_seq IS '상위 글 (답글일 때, t_qna.q_seq)';
COMMENT ON COLUMN smfr.t_qna.q_status IS '처리 상태';
COMMENT ON COLUMN smfr.t_qna.q_email IS '회신 이메일';
COMMENT ON COLUMN smfr.t_qna.q_phone IS '회신 전화번호';
COMMENT ON COLUMN smfr.t_qna.isread IS '읽음 여부';
COMMENT ON COLUMN smfr.t_qna.readtime IS '읽은 일시';
COMMENT ON COLUMN smfr.t_qna.bigo IS '비고';

COMMENT ON TABLE smfr.t_rol IS '옛 권한 역할';
COMMENT ON COLUMN smfr.t_rol.r_seq IS '역할 키';
COMMENT ON COLUMN smfr.t_rol.r_nm IS '역할명';
COMMENT ON COLUMN smfr.t_rol.r_desc IS '역할 설명';
COMMENT ON COLUMN smfr.t_rol.cre_dt IS '생성 일시';
COMMENT ON COLUMN smfr.t_rol.r_type IS '역할 유형';
COMMENT ON COLUMN smfr.t_rol.r_sort IS '정렬 순서';
COMMENT ON COLUMN smfr.t_rol.del_flag IS '삭제 여부';
COMMENT ON COLUMN smfr.t_rol.machine_mng IS '장비 관리 권한 여부';

COMMENT ON TABLE smfr.t_rol_account IS '옛 역할-계정 연결';
COMMENT ON COLUMN smfr.t_rol_account.r_seq IS '역할 (t_rol.r_seq)';
COMMENT ON COLUMN smfr.t_rol_account.a_key IS '계정 (t_account.a_key)';

COMMENT ON TABLE smfr.t_rol_menu IS '옛 역할-메뉴 연결';
COMMENT ON COLUMN smfr.t_rol_menu.r_seq IS '역할 (t_rol.r_seq)';
COMMENT ON COLUMN smfr.t_rol_menu.mn_seq IS '메뉴 (t_menu.mn_seq)';

COMMENT ON TABLE smfr.t_room IS '옛 호실(빈소) 정보 (현 smfr.rooms 로 대체)';
COMMENT ON COLUMN smfr.t_room.r_key IS '호실 키';
COMMENT ON COLUMN smfr.t_room.f_key IS '소속 층 (t_floor.f_key)';
COMMENT ON COLUMN smfr.t_room.etc_price IS '기타 요금';
COMMENT ON COLUMN smfr.t_room.base_price IS '기본료';
COMMENT ON COLUMN smfr.t_room.mng_price IS '관리비';
COMMENT ON COLUMN smfr.t_room.cre_dt IS '생성 일시';
COMMENT ON COLUMN smfr.t_room.mod_dt IS '수정 일시';
COMMENT ON COLUMN smfr.t_room.mo_group IS '영상 그룹';
COMMENT ON COLUMN smfr.t_room.srt_no IS '정렬 순서 (기본 999)';

COMMENT ON TABLE smfr.t_room_goin IS '옛 호실-고인 배정 (현 smfr.deceased_rooms 로 대체)';
COMMENT ON COLUMN smfr.t_room_goin.gi_key IS '고인 (t_goin.gi_key)';
COMMENT ON COLUMN smfr.t_room_goin.r_key IS '호실 (t_room.r_key)';
COMMENT ON COLUMN smfr.t_room_goin.s_dt IS '사용 시작 일자 (문자열, 실데이터 비어 있음)';
COMMENT ON COLUMN smfr.t_room_goin.d_dt IS '사용 종료 일자 (문자열, 실데이터 비어 있음)';
COMMENT ON COLUMN smfr.t_room_goin.expired IS '배정 종료 여부 (false 면 사용 중)';
COMMENT ON COLUMN smfr.t_room_goin.rg_key IS '배정 키';
COMMENT ON COLUMN smfr.t_room_goin.use_days IS '사용 일수 (기본 1)';

COMMENT ON TABLE smfr.t_room_goin_bbk IS 't_room_goin 과 같은 모양의 사본 (백업으로 보임)';
COMMENT ON COLUMN smfr.t_room_goin_bbk.gi_key IS '고인 (t_goin.gi_key)';
COMMENT ON COLUMN smfr.t_room_goin_bbk.r_key IS '호실 (t_room.r_key)';
COMMENT ON COLUMN smfr.t_room_goin_bbk.s_dt IS '사용 시작 일자 (문자열)';
COMMENT ON COLUMN smfr.t_room_goin_bbk.d_dt IS '사용 종료 일자 (문자열)';
COMMENT ON COLUMN smfr.t_room_goin_bbk.expired IS '배정 종료 여부';
COMMENT ON COLUMN smfr.t_room_goin_bbk.rg_key IS '배정 키';
COMMENT ON COLUMN smfr.t_room_goin_bbk.use_days IS '사용 일수';

COMMENT ON TABLE smfr.t_sangju IS '옛 고인별 상주 (현 smfr.deceased_mourners 로 대체)';
COMMENT ON COLUMN smfr.t_sangju.gi_key IS '고인 (t_goin.gi_key)';
COMMENT ON COLUMN smfr.t_sangju.family_type IS '고인과의 관계';
COMMENT ON COLUMN smfr.t_sangju.nm IS '상주 성명';
COMMENT ON COLUMN smfr.t_sangju.sj_key IS '상주 키';
COMMENT ON COLUMN smfr.t_sangju.sj_desc IS '비고';

COMMIT;
