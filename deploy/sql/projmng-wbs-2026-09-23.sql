-- ============================================================
-- WBS 대시보드를 projmng 스키마로 들인다 (표 15 · 뷰 4)
-- ============================================================
--
-- 원본은 사내망에서 따로 돌던 **WBS 대시보드**(`move_re` 꾸러미)다.
-- `db/install/wbs_install.sql`(public 스키마, 표 17 · 뷰 4)을 옮긴 것이고,
-- 그 파일은 사내 개발 PC 의 실제 스키마를 `pg_dump --schema-only` 로 뜬 것이라
-- 손으로 적은 DDL 이 아니다. 여기서 바꾼 것만 아래에 적는다.
--
-- 두 번 돌려도 안전하다.
--
-- ── 표 이름 ──────────────────────────────────────────────────
--
-- `hhip_` 는 **한 고객사 이름**이다. 이 표들은 이제 프로젝트를 가리지 않으므로
-- (아래 `prj_rid`) 그 접두사를 떼고 `wbs_` 로 모았다. `if_` 는 원래 중립이라
-- 그대로 둔다. 들여온 무리가 `wbs_*` 와 `if_*` 둘뿐이라, 나중에 이 이관이
-- 어디까지였는지 표 이름만 보고 안다.
--
--   public.hhip_wbs_wrk2     →  projmng.wbs_work       (원장. 1행 = 1화면)
--   public.hhip_wbs_task     →  projmng.wbs_task       (화면별 일감)
--   public.hhip_wbs_pv       →  projmng.wbs_pv         (ProjectView 캐시)
--   public.hhip_wbs_pv_task  →  projmng.wbs_pv_task
--   public.hhip_wbs_pv_node  →  projmng.wbs_pv_node
--   public.dev_user          →  projmng.wbs_user       (개발자 마스터)
--   public.dev_docs          →  projmng.wbs_docs       (팀 공유 문서)
--   public.dev_user_pref     →  projmng.wbs_user_pref  (화면 설정)
--   public.if_*              →  projmng.if_*           (7표 · 뷰 4, 이름 그대로)
--
-- ── 안 옮기는 표 둘 ──────────────────────────────────────────
--
--   public.dev_menu       대시보드 메뉴 목록
--   public.dev_user_menu  개발자별 메뉴 권한 (행이 없으면 허용)
--
-- 포털에 이미 `scom.system_menus` 와 `scom.role_menus` 가 있고 사이드바가
-- 그것으로 거른다. 같은 일을 하는 표를 둘 두면 **한쪽만 고치는 날이 온다** —
-- 그리고 어긋나는 방향이 *권한이 없는데 메뉴가 보이는* 쪽이라 특히 나쁘다.
-- 원본의 [메뉴 권한] 화면도 같은 이유로 옮기지 않는다.
--
-- ── 프로젝트 칸(`prj_rid`) ───────────────────────────────────
--
-- 원본은 **프로젝트 하나 전용**이라 그 칸이 없었다. projmng 는 여러 프로젝트를
-- 다루므로 뿌리 표마다 넣는다. 딸린 표(`if_step`·`if_note`·`if_attr`·
-- `wbs_pv_node`)는 부모를 타고 가므로 넣지 않는다 — 같은 값을 두 군데 두면
-- 언젠가 서로 달라진다.
--
-- **외래키는 걸지 않는다.** `projmng.dev_proj` 에 기본키도 유일 인덱스도
-- 없어서 걸 대상이 없다(그 표를 손보는 것은 이 이관의 일이 아니다).
--
-- `if_code` 만 프로젝트를 안 가린다. 방향·상태·주기 같은 **말 자체**라
-- 프로젝트마다 다르게 둘 이유가 없다.
--
-- ── 개발자와 포털 계정(`wbs_user.login_id`) ──────────────────
--
-- 원장의 담당자 칸(`user_bp_id`)은 사번을 가리키고, 사번-성명은 `wbs_user` 가
-- 들고 있다. 포털 계정은 그것과 별개라 **잇는 칸을 하나 둔다.**
-- 값이 있으면 화면이 포털에서 이름과 얼굴을 가져오고, 없으면 `wbs_user.name`
-- 을 그대로 쓴다. 표를 통째로 계정 쪽에 넘기지 않은 까닭은 이 표가 장비번호 ·
-- MAC · 비상연락처처럼 **포털 계정에 없는 칸을 서른 개** 들고 있어서다.
--
-- ── 걷어낸 칸 ───────────────────────────────────────────────
--
-- `use_ip` 는 원본의 **인증 전부**였다(`IpGate.cs` — 접속 IP 를 이 칸과 대조).
-- 포털 안에서는 JWT 와 역할이 그 일을 하므로 접근 제어로 쓰지 않는다.
-- 다만 칸은 남긴다 — 사내에서 **장비 대장**으로도 쓰고 있어서
-- (MAC · 장비번호와 같은 줄에 있다) 지우면 그 기록이 사라진다.
-- `super_yn`·`block_yn` 도 같은 이유로 남기되 **권한 판정에 쓰지 않는다.**
--
-- ── 자료 ────────────────────────────────────────────────────
--
-- 코드성 자료는 `if_code` 43건만 넣는다(프로젝트를 안 가리는 표다).
-- 원본 꾸러미의 `if_system` 3 · `if_attr_def` 3 은 **그 프로젝트의 자료**라
-- 여기서 넣지 않는다 — 맨 아래에 옮기는 본보기를 적어 두었다.
-- 원장(`wbs_work`) · 일감 · 인터페이스 내용 · 개발자 명부는 업무자료라
-- 이 파일에 없다. 옮길 것이면 표별로 따로 뽑아 `prj_rid` 를 채워 넣는다.

BEGIN;

-- ── 갱신시각 트리거 함수 ─────────────────────────────────────
--
-- `if_*` 일곱 표의 `updated_at` 을 채운다. 원본에서는 `public` 에 있었다.
CREATE OR REPLACE FUNCTION projmng.fn_set_updated_at() RETURNS trigger
    LANGUAGE plpgsql
    AS $$
BEGIN
    NEW.updated_at := now();
    RETURN NEW;
END;
$$;


-- ════════════════════════════════════════════════════════════
--  WBS
-- ════════════════════════════════════════════════════════════

-- ── 원장 ────────────────────────────────────────────────────
--
-- 원본에는 **기본키가 없었다.** `activity_id` 가 엑셀 WBS 와 맞추는 열쇠라
-- 사실상 그 노릇을 하고 있었고, 일감 표도 그 값으로 붙는다. 프로젝트 칸이
-- 생겼으니 둘을 묶어 기본키로 세운다 — 일감의 외래키가 걸릴 자리가 필요하다.
--
-- 옮겨 오는 자료에 `activity_id` 가 빈 줄이 있으면 **그 줄부터 채우고** 넣는다.
CREATE TABLE IF NOT EXISTS projmng.wbs_work (
    prj_rid              integer NOT NULL,
    activity_id          character varying(50) NOT NULL,
    module               character varying(50),
    module_name          text,
    systemcode           text,
    system_nm            text,
    program_id           text,
    menu_nm              character varying(50),
    comment              text,
    plan_sdt             date,
    plan_edt             date,
    priority_order       character varying(50),
    plan_sdt_c           date,
    plan_edt_c           date,
    prog_type            character varying(1000),
    prog_type_desc       character varying(1000),
    trg_exsit_chk        character varying(1000),
    trg_use_chk          character varying(1000),
    trg_evt_rel          character varying(1000),
    etc_desc             character varying(1000),
    new_dev2             character varying(1000),
    asis_cs              character varying(1000),
    report_use           character varying(1000),
    sheet_use            character varying,
    dwg_view_use         character varying(100),
    por_view_use         character varying(100),
    menu_desc            character varying(2000),
    mon_req_comp         character varying(100),
    user_bp_id           character varying(100),
    complate_yn          character varying(100),
    comp_desc            character varying(2000),
    user_real_id         character varying,
    complate_real_yn     character varying,
    recheck_yn           character varying(1),
    complate_big_yn      character varying(1),
    complate_real_big_yn character varying(1),
    recheck_big_yn       character varying(1),
    db_ready_big_yn      character varying(1),
    CONSTRAINT wbs_work_pkey PRIMARY KEY (prj_rid, activity_id)
);

CREATE INDEX IF NOT EXISTS ix_wbs_work_user   ON projmng.wbs_work (prj_rid, user_bp_id);
CREATE INDEX IF NOT EXISTS ix_wbs_work_module ON projmng.wbs_work (prj_rid, systemcode);
CREATE INDEX IF NOT EXISTS ix_wbs_work_edt    ON projmng.wbs_work (prj_rid, plan_edt);


-- ── 화면별 일감 ─────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS projmng.wbs_task (
    task_id     integer GENERATED BY DEFAULT AS IDENTITY,
    prj_rid     integer NOT NULL,
    activity_id character varying(50) NOT NULL,
    task_div    character varying(50),
    memo        text,
    done_yn     character varying(1),
    sort_order  integer DEFAULT 0 NOT NULL,
    created_at  timestamp with time zone DEFAULT now() NOT NULL,
    updated_at  timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT wbs_task_pkey PRIMARY KEY (task_id),
    CONSTRAINT wbs_task_work_fkey FOREIGN KEY (prj_rid, activity_id)
        REFERENCES projmng.wbs_work (prj_rid, activity_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_wbs_task_activity ON projmng.wbs_task (prj_rid, activity_id);
CREATE INDEX IF NOT EXISTS ix_wbs_task_done     ON projmng.wbs_task (prj_rid, activity_id, done_yn);


-- ── ProjectView 캐시 ────────────────────────────────────────
--
-- **원본이 아니다.** 언제든 비우고 다시 모을 수 있는 자료라 원장과 엮지 않는다
-- (수집이 원장보다 앞설 수 있어서 외래키를 걸면 그 줄이 버려진다).
CREATE TABLE IF NOT EXISTS projmng.wbs_pv (
    prj_rid         integer NOT NULL,
    activity_id     character varying(50) NOT NULL,
    pv_project_id   character varying(60),
    pv_work_id      character varying(60),
    pv_work_title   text,
    pv_seen_at      timestamp without time zone,
    pv_finish_rate  numeric(6,2),
    pv_actual_rate  numeric(6,2),
    pv_plan_sdt     date,
    pv_plan_edt     date,
    pv_actual_sdt   date,
    pv_actual_edt   date,
    pv_snapshot_at  timestamp without time zone,
    updated_at      timestamp without time zone DEFAULT now(),
    CONSTRAINT wbs_pv_pkey PRIMARY KEY (prj_rid, activity_id)
);

CREATE INDEX IF NOT EXISTS ix_wbs_pv_work ON projmng.wbs_pv (prj_rid, pv_work_id);


CREATE TABLE IF NOT EXISTS projmng.wbs_pv_task (
    prj_rid       integer NOT NULL,
    pv_task_id    character varying(60) NOT NULL,
    activity_id   character varying(50),
    pv_work_id    character varying(60),
    pv_task_code  character varying(50),
    pv_task_title text,
    pv_plan_sdt   date,
    pv_plan_edt   date,
    pv_node_cnt   integer,
    pv_node_empty integer,
    pv_seen_at    timestamp without time zone,
    updated_at    timestamp without time zone DEFAULT now(),
    pv_charger_id character varying(40),
    pv_charger_nm text,
    pv_status     text,
    pv_status_at  date,
    CONSTRAINT wbs_pv_task_pkey PRIMARY KEY (prj_rid, pv_task_id)
);

CREATE INDEX IF NOT EXISTS ix_wbs_pv_task_act  ON projmng.wbs_pv_task (prj_rid, activity_id);
CREATE INDEX IF NOT EXISTS ix_wbs_pv_task_code ON projmng.wbs_pv_task (prj_rid, pv_task_code);


CREATE TABLE IF NOT EXISTS projmng.wbs_pv_node (
    prj_rid    integer NOT NULL,
    pv_task_id character varying(60) NOT NULL,
    node_no    integer NOT NULL,
    node_id    character varying(60),
    stage_nm   text,
    node_dt    date,
    worker_id  character varying(40),
    worker_nm  text,
    updated_at timestamp without time zone DEFAULT now(),
    CONSTRAINT wbs_pv_node_pkey PRIMARY KEY (prj_rid, pv_task_id, node_no),
    CONSTRAINT wbs_pv_node_task_fkey FOREIGN KEY (prj_rid, pv_task_id)
        REFERENCES projmng.wbs_pv_task (prj_rid, pv_task_id) ON DELETE CASCADE
);


-- ── 개발자 마스터 ───────────────────────────────────────────
--
-- 원본은 `bp_id` 를 `varchar(2000)` 으로 두고 기본키가 없었다. 사번이라
-- 40 자면 넉넉하고, 원장의 담당자 칸(`varchar(100)`)이 이 값을 가리키므로
-- 열쇠가 있어야 한다.
CREATE TABLE IF NOT EXISTS projmng.wbs_user (
    prj_rid         integer NOT NULL,
    bp_id           character varying(40) NOT NULL,
    login_id        character varying(100),
    name            character varying(200),
    email           character varying(200),
    position_nm     character varying(50),
    tel_no          character varying(40),
    emerg_tel_no    character varying(40),
    birth_dt        date,
    git             character varying(2000),
    startkit        character varying(2000),
    dxb             character varying(2000),
    vm_conn         character varying(2000),
    aipro           character varying(2000),
    claudecode      character varying(2000),
    dev_db          character varying(2000),
    wiki            character varying(2000),
    projectview     character varying(2000),
    svn             character varying(10),
    pv_user_id      character varying(40),
    notebook        character varying(100),
    hub_hdmi        character varying(100),
    summer_size     character varying(20),
    winter_size     character varying(20),
    use_ip          character varying(60),
    mac_addr        character varying(60),
    notebook_no     character varying(60),
    notebook_chk_no character varying(60),
    monitor1_no     character varying(60),
    monitor1_chk_no character varying(60),
    monitor2_no     character varying(60),
    monitor2_chk_no character varying(60),
    monitor3_no     character varying(60),
    monitor3_chk_no character varying(60),
    block_yn        character varying(1),
    super_yn        character varying(1),
    CONSTRAINT wbs_user_pkey PRIMARY KEY (prj_rid, bp_id)
);

-- 한 사람이 한 프로젝트에 두 사번으로 앉을 수는 없다. 비어 있는 줄은 여럿이어도
-- 된다(아직 계정을 안 이은 사람) — 부분 인덱스라 NULL 은 걸리지 않는다.
CREATE UNIQUE INDEX IF NOT EXISTS uk_wbs_user_login
    ON projmng.wbs_user (prj_rid, login_id) WHERE login_id IS NOT NULL;


-- ── 팀 공유 문서 ────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS projmng.wbs_docs (
    id         integer GENERATED BY DEFAULT AS IDENTITY,
    prj_rid    integer NOT NULL,
    title      character varying(200) NOT NULL,
    content    text DEFAULT ''::text NOT NULL,
    sort_order integer DEFAULT 0 NOT NULL,
    updated_by character varying(100),
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT wbs_docs_pkey PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS ix_wbs_docs_prj ON projmng.wbs_docs (prj_rid, sort_order);


-- ── 사용자별 화면 설정 ──────────────────────────────────────
--
-- 원본은 접속 IP 로 사람을 가려내 `bp_id` 를 열쇠로 썼다. 포털 안에서는
-- 로그인 계정을 안다 — 그래도 열쇠는 `bp_id` 그대로 둔다. 같은 사람이
-- 계정을 갈아도 사번은 그대로이고, 설정은 **그 사람의 것**이기 때문이다.
CREATE TABLE IF NOT EXISTS projmng.wbs_user_pref (
    prj_rid    integer NOT NULL,
    bp_id      text NOT NULL,
    pref_key   text NOT NULL,
    pref_val   text,
    updated_at timestamp without time zone DEFAULT now(),
    CONSTRAINT wbs_user_pref_pkey PRIMARY KEY (prj_rid, bp_id, pref_key)
);


-- ════════════════════════════════════════════════════════════
--  인터페이스 카탈로그
-- ════════════════════════════════════════════════════════════

-- ── 공통코드 — 프로젝트를 안 가린다 ─────────────────────────
CREATE TABLE IF NOT EXISTS projmng.if_code (
    code_id    integer GENERATED BY DEFAULT AS IDENTITY,
    code_grp   character varying(30) NOT NULL,
    code       character varying(30) NOT NULL,
    code_nm    character varying(100) NOT NULL,
    code_desc  text,
    sort_order integer DEFAULT 0 NOT NULL,
    use_yn     character varying(1) DEFAULT 'Y'::character varying NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    updated_at timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT if_code_pkey PRIMARY KEY (code_id),
    CONSTRAINT uk_if_code UNIQUE (code_grp, code),
    CONSTRAINT if_code_use_yn_check CHECK (use_yn IN ('Y', 'N'))
);


-- ── 연계 대상 시스템 ────────────────────────────────────────
CREATE TABLE IF NOT EXISTS projmng.if_system (
    system_id   integer GENERATED BY DEFAULT AS IDENTITY,
    prj_rid     integer NOT NULL,
    system_cd   character varying(30) NOT NULL,
    system_nm   character varying(100) NOT NULL,
    system_kind character varying(30),
    host        character varying(100),
    port        integer,
    db_nm       character varying(100),
    schema_nm   character varying(100),
    system_desc text,
    ext         jsonb DEFAULT '{}'::jsonb NOT NULL,
    sort_order  integer DEFAULT 0 NOT NULL,
    use_yn      character varying(1) DEFAULT 'Y'::character varying NOT NULL,
    created_at  timestamp with time zone DEFAULT now() NOT NULL,
    created_by  character varying(50),
    updated_at  timestamp with time zone DEFAULT now() NOT NULL,
    updated_by  character varying(50),
    CONSTRAINT if_system_pkey PRIMARY KEY (system_id),
    CONSTRAINT uk_if_system UNIQUE (prj_rid, system_cd),
    CONSTRAINT if_system_use_yn_check CHECK (use_yn IN ('Y', 'N'))
);


-- ── 인터페이스 기본정보 ─────────────────────────────────────
CREATE TABLE IF NOT EXISTS projmng.if_master (
    if_id         integer GENERATED BY DEFAULT AS IDENTITY,
    prj_rid       integer NOT NULL,
    if_cd         character varying(30) NOT NULL,
    if_nm         character varying(200) NOT NULL,
    if_desc       text,
    direction_cd  character varying(30),
    src_system_id integer,
    tgt_system_id integer,
    domain_cd     character varying(30),
    trigger_cd    character varying(30),
    cycle_cd      character varying(30),
    status_cd     character varying(30) DEFAULT 'DESIGN'::character varying NOT NULL,
    owner_nm      character varying(50),
    owner_bp_id   character varying(50),
    plan_sdt      date,
    plan_edt      date,
    open_dt       date,
    remark        text,
    ext           jsonb DEFAULT '{}'::jsonb NOT NULL,
    sort_order    integer DEFAULT 0 NOT NULL,
    use_yn        character varying(1) DEFAULT 'Y'::character varying NOT NULL,
    created_at    timestamp with time zone DEFAULT now() NOT NULL,
    created_by    character varying(50),
    updated_at    timestamp with time zone DEFAULT now() NOT NULL,
    updated_by    character varying(50),
    CONSTRAINT if_master_pkey PRIMARY KEY (if_id),
    CONSTRAINT uk_if_master UNIQUE (prj_rid, if_cd),
    CONSTRAINT if_master_src_system_id_fkey FOREIGN KEY (src_system_id)
        REFERENCES projmng.if_system (system_id),
    CONSTRAINT if_master_tgt_system_id_fkey FOREIGN KEY (tgt_system_id)
        REFERENCES projmng.if_system (system_id),
    CONSTRAINT if_master_use_yn_check CHECK (use_yn IN ('Y', 'N'))
);

CREATE INDEX IF NOT EXISTS ix_if_master_01 ON projmng.if_master (prj_rid, status_cd);
CREATE INDEX IF NOT EXISTS ix_if_master_02 ON projmng.if_master (prj_rid, domain_cd);
CREATE INDEX IF NOT EXISTS ix_if_master_03 ON projmng.if_master USING gin (ext);


-- ── 처리 단계 ───────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS projmng.if_step (
    step_id      integer GENERATED BY DEFAULT AS IDENTITY,
    if_id        integer NOT NULL,
    step_no      integer NOT NULL,
    step_nm      character varying(200),
    step_type_cd character varying(30),
    system_id    integer,
    object_owner character varying(50),
    object_nm    character varying(200),
    object_type  character varying(30),
    step_desc    text,
    params       jsonb DEFAULT '{}'::jsonb NOT NULL,
    ext          jsonb DEFAULT '{}'::jsonb NOT NULL,
    use_yn       character varying(1) DEFAULT 'Y'::character varying NOT NULL,
    created_at   timestamp with time zone DEFAULT now() NOT NULL,
    created_by   character varying(50),
    updated_at   timestamp with time zone DEFAULT now() NOT NULL,
    updated_by   character varying(50),
    CONSTRAINT if_step_pkey PRIMARY KEY (step_id),
    CONSTRAINT uk_if_step UNIQUE (if_id, step_no),
    CONSTRAINT if_step_if_id_fkey FOREIGN KEY (if_id)
        REFERENCES projmng.if_master (if_id) ON DELETE CASCADE,
    CONSTRAINT if_step_system_id_fkey FOREIGN KEY (system_id)
        REFERENCES projmng.if_system (system_id),
    CONSTRAINT if_step_use_yn_check CHECK (use_yn IN ('Y', 'N'))
);

CREATE INDEX IF NOT EXISTS ix_if_step_01 ON projmng.if_step (if_id, step_no);
CREATE INDEX IF NOT EXISTS ix_if_step_02 ON projmng.if_step (object_owner, object_nm);


-- ── 메모 · 이슈 · 변경이력 ──────────────────────────────────
CREATE TABLE IF NOT EXISTS projmng.if_note (
    note_id      integer GENERATED BY DEFAULT AS IDENTITY,
    if_id        integer NOT NULL,
    step_id      integer,
    note_type_cd character varying(30) DEFAULT 'MEMO'::character varying NOT NULL,
    title        character varying(200),
    content      text DEFAULT ''::text NOT NULL,
    writer_nm    character varying(50),
    note_dt      date DEFAULT CURRENT_DATE NOT NULL,
    done_yn      character varying(1) DEFAULT 'N'::character varying NOT NULL,
    sort_order   integer DEFAULT 0 NOT NULL,
    ext          jsonb DEFAULT '{}'::jsonb NOT NULL,
    created_at   timestamp with time zone DEFAULT now() NOT NULL,
    created_by   character varying(50),
    updated_at   timestamp with time zone DEFAULT now() NOT NULL,
    updated_by   character varying(50),
    CONSTRAINT if_note_pkey PRIMARY KEY (note_id),
    CONSTRAINT if_note_if_id_fkey FOREIGN KEY (if_id)
        REFERENCES projmng.if_master (if_id) ON DELETE CASCADE,
    CONSTRAINT if_note_step_id_fkey FOREIGN KEY (step_id)
        REFERENCES projmng.if_step (step_id) ON DELETE SET NULL,
    CONSTRAINT if_note_done_yn_check CHECK (done_yn IN ('Y', 'N'))
);

CREATE INDEX IF NOT EXISTS ix_if_note_01 ON projmng.if_note (if_id, note_dt DESC);


-- ── 추가 관리항목 — 정의와 값 ───────────────────────────────
CREATE TABLE IF NOT EXISTS projmng.if_attr_def (
    attr_def_id integer GENERATED BY DEFAULT AS IDENTITY,
    prj_rid     integer NOT NULL,
    attr_cd     character varying(50) NOT NULL,
    attr_nm     character varying(100) NOT NULL,
    attr_type   character varying(20) DEFAULT 'TEXT'::character varying NOT NULL,
    code_grp    character varying(30),
    required_yn character varying(1) DEFAULT 'N'::character varying NOT NULL,
    default_val text,
    attr_desc   text,
    sort_order  integer DEFAULT 0 NOT NULL,
    use_yn      character varying(1) DEFAULT 'Y'::character varying NOT NULL,
    created_at  timestamp with time zone DEFAULT now() NOT NULL,
    updated_at  timestamp with time zone DEFAULT now() NOT NULL,
    CONSTRAINT if_attr_def_pkey PRIMARY KEY (attr_def_id),
    CONSTRAINT uk_if_attr_def UNIQUE (prj_rid, attr_cd),
    CONSTRAINT if_attr_def_attr_type_check
        CHECK (attr_type IN ('TEXT', 'NUMBER', 'DATE', 'YN', 'CODE', 'JSON')),
    CONSTRAINT if_attr_def_required_yn_check CHECK (required_yn IN ('Y', 'N')),
    CONSTRAINT if_attr_def_use_yn_check CHECK (use_yn IN ('Y', 'N'))
);

CREATE TABLE IF NOT EXISTS projmng.if_attr (
    attr_id     integer GENERATED BY DEFAULT AS IDENTITY,
    if_id       integer NOT NULL,
    attr_def_id integer NOT NULL,
    attr_val    text,
    created_at  timestamp with time zone DEFAULT now() NOT NULL,
    created_by  character varying(50),
    updated_at  timestamp with time zone DEFAULT now() NOT NULL,
    updated_by  character varying(50),
    CONSTRAINT if_attr_pkey PRIMARY KEY (attr_id),
    CONSTRAINT uk_if_attr UNIQUE (if_id, attr_def_id),
    CONSTRAINT if_attr_if_id_fkey FOREIGN KEY (if_id)
        REFERENCES projmng.if_master (if_id) ON DELETE CASCADE,
    CONSTRAINT if_attr_attr_def_id_fkey FOREIGN KEY (attr_def_id)
        REFERENCES projmng.if_attr_def (attr_def_id) ON DELETE CASCADE
);


-- ── 갱신시각 트리거 일곱 ────────────────────────────────────
DROP TRIGGER IF EXISTS tg_if_code_upd     ON projmng.if_code;
DROP TRIGGER IF EXISTS tg_if_system_upd   ON projmng.if_system;
DROP TRIGGER IF EXISTS tg_if_master_upd   ON projmng.if_master;
DROP TRIGGER IF EXISTS tg_if_step_upd     ON projmng.if_step;
DROP TRIGGER IF EXISTS tg_if_note_upd     ON projmng.if_note;
DROP TRIGGER IF EXISTS tg_if_attr_def_upd ON projmng.if_attr_def;
DROP TRIGGER IF EXISTS tg_if_attr_upd     ON projmng.if_attr;

CREATE TRIGGER tg_if_code_upd     BEFORE UPDATE ON projmng.if_code
    FOR EACH ROW EXECUTE FUNCTION projmng.fn_set_updated_at();
CREATE TRIGGER tg_if_system_upd   BEFORE UPDATE ON projmng.if_system
    FOR EACH ROW EXECUTE FUNCTION projmng.fn_set_updated_at();
CREATE TRIGGER tg_if_master_upd   BEFORE UPDATE ON projmng.if_master
    FOR EACH ROW EXECUTE FUNCTION projmng.fn_set_updated_at();
CREATE TRIGGER tg_if_step_upd     BEFORE UPDATE ON projmng.if_step
    FOR EACH ROW EXECUTE FUNCTION projmng.fn_set_updated_at();
CREATE TRIGGER tg_if_note_upd     BEFORE UPDATE ON projmng.if_note
    FOR EACH ROW EXECUTE FUNCTION projmng.fn_set_updated_at();
CREATE TRIGGER tg_if_attr_def_upd BEFORE UPDATE ON projmng.if_attr_def
    FOR EACH ROW EXECUTE FUNCTION projmng.fn_set_updated_at();
CREATE TRIGGER tg_if_attr_upd     BEFORE UPDATE ON projmng.if_attr
    FOR EACH ROW EXECUTE FUNCTION projmng.fn_set_updated_at();


-- ── 뷰 넷 ───────────────────────────────────────────────────
--
-- 원본 그대로이되 `prj_rid` 를 함께 내보낸다 — 화면이 프로젝트로 좁혀 읽는다.
-- 넷 다 `if_master` 에서 출발하므로 그 칸 하나면 된다.

CREATE OR REPLACE VIEW projmng.v_if_master AS
 SELECT m.if_id,
        m.prj_rid,
        m.if_cd,
        m.if_nm,
        m.domain_cd,
        m.direction_cd,
        cd.code_nm AS direction_nm,
        ss.system_nm AS src_system_nm,
        ts.system_nm AS tgt_system_nm,
        m.status_cd,
        cs.code_nm AS status_nm,
        m.cycle_cd,
        cc.code_nm AS cycle_nm,
        m.owner_nm,
        m.plan_sdt,
        m.plan_edt,
        m.open_dt,
        ( SELECT count(*) FROM projmng.if_step s
           WHERE s.if_id = m.if_id AND s.use_yn = 'Y' ) AS step_cnt,
        ( SELECT count(*) FROM projmng.if_note n
           WHERE n.if_id = m.if_id ) AS note_cnt,
        ( SELECT count(*) FROM projmng.if_note n
           WHERE n.if_id = m.if_id
             AND n.note_type_cd = 'ISSUE'
             AND n.done_yn = 'N' ) AS open_issue_cnt,
        m.remark,
        m.ext,
        m.use_yn,
        m.sort_order,
        m.updated_at,
        m.updated_by
   FROM projmng.if_master m
   LEFT JOIN projmng.if_system ss ON ss.system_id = m.src_system_id
   LEFT JOIN projmng.if_system ts ON ts.system_id = m.tgt_system_id
   LEFT JOIN projmng.if_code cd ON cd.code_grp = 'IF_DIRECTION' AND cd.code = m.direction_cd
   LEFT JOIN projmng.if_code cs ON cs.code_grp = 'IF_STATUS'    AND cs.code = m.status_cd
   LEFT JOIN projmng.if_code cc ON cc.code_grp = 'IF_CYCLE'     AND cc.code = m.cycle_cd;


CREATE OR REPLACE VIEW projmng.v_if_step AS
 SELECT s.step_id,
        s.if_id,
        m.prj_rid,
        m.if_cd,
        m.if_nm,
        s.step_no,
        s.step_nm,
        s.step_type_cd,
        ct.code_nm AS step_type_nm,
        sy.system_nm,
        s.object_owner,
        s.object_nm,
        CASE WHEN s.object_owner IS NULL THEN s.object_nm::text
             ELSE s.object_owner::text || '.' || s.object_nm::text
        END AS object_full_nm,
        s.object_type,
        s.step_desc,
        s.params,
        s.ext,
        s.use_yn,
        s.updated_at
   FROM projmng.if_step s
   JOIN projmng.if_master m ON m.if_id = s.if_id
   LEFT JOIN projmng.if_system sy ON sy.system_id = s.system_id
   LEFT JOIN projmng.if_code ct ON ct.code_grp = 'STEP_TYPE' AND ct.code = s.step_type_cd;


-- 관리항목은 **정의 × 인터페이스**를 모두 펼친다(CROSS JOIN) — 값이 없는
-- 항목도 화면에 칸으로 나와야 하기 때문이다. 정의에도 프로젝트가 붙었으므로
-- 짝을 프로젝트 안에서만 맺는다. 안 그러면 남의 프로젝트 항목까지 칸이 뜬다.
CREATE OR REPLACE VIEW projmng.v_if_attr AS
 SELECT m.if_id,
        m.prj_rid,
        m.if_cd,
        d.attr_def_id,
        d.attr_cd,
        d.attr_nm,
        d.attr_type,
        d.code_grp,
        d.required_yn,
        COALESCE(a.attr_val, d.default_val) AS attr_val,
        d.sort_order,
        a.updated_at,
        a.updated_by
   FROM projmng.if_master m
   CROSS JOIN projmng.if_attr_def d
   LEFT JOIN projmng.if_attr a ON a.if_id = m.if_id AND a.attr_def_id = d.attr_def_id
  WHERE d.use_yn = 'Y'
    AND d.prj_rid = m.prj_rid;


CREATE OR REPLACE VIEW projmng.v_if_flow AS
 SELECT m.if_id,
        m.prj_rid,
        m.if_cd,
        m.if_nm,
        string_agg(s.step_no || '. ' ||
            CASE WHEN s.object_owner IS NULL
                 THEN COALESCE(s.object_nm, s.step_nm, '')::text
                 ELSE s.object_owner::text || '.' || s.object_nm::text
            END, '  →  ' ORDER BY s.step_no) AS flow
   FROM projmng.if_master m
   LEFT JOIN projmng.if_step s ON s.if_id = m.if_id AND s.use_yn = 'Y'
  GROUP BY m.if_id, m.prj_rid, m.if_cd, m.if_nm;


-- ── 공통코드 43건 ───────────────────────────────────────────
--
-- 이것이 없으면 인터페이스 화면의 선택목록(방향·상태·주기·단계유형 …)이
-- 전부 빈다. 표만 있어서는 화면이 돌지 않는다.
--
-- `code_id` 는 적지 않는다 — 이 표에서 뜻을 가진 열쇠는 `(code_grp, code)` 이고
-- 번호는 그냥 줄번호다. 두 번 돌려도 안전하려면 뜻 있는 쪽으로 겹침을 본다.
INSERT INTO projmng.if_code (code_grp, code, code_nm, code_desc, sort_order)
VALUES
    ('IF_DIRECTION', 'IN',            '수신',           '외부 → 우리 시스템',              1),
    ('IF_DIRECTION', 'OUT',           '송신',           '우리 시스템 → 외부',              2),
    ('IF_DIRECTION', 'BIDIR',         '양방향',         NULL,                              3),
    ('IF_STATUS',    'DESIGN',        '설계',           '규격 협의 단계',                  1),
    ('IF_STATUS',    'DEV',           '개발',           NULL,                              2),
    ('IF_STATUS',    'TEST',          '테스트',         NULL,                              3),
    ('IF_STATUS',    'PROD',          '운영',           '운영 반영 완료',                  4),
    ('IF_STATUS',    'HOLD',          '보류',           NULL,                              5),
    ('IF_STATUS',    'DROP',          '폐기',           NULL,                              6),
    ('IF_CYCLE',     'REALTIME',      '실시간',         '발생 즉시',                       1),
    ('IF_CYCLE',     'HOURLY',        '시간별',         NULL,                              2),
    ('IF_CYCLE',     'DAILY',         '일배치',         NULL,                              3),
    ('IF_CYCLE',     'WEEKLY',        '주배치',         NULL,                              4),
    ('IF_CYCLE',     'MONTHLY',       '월배치',         NULL,                              5),
    ('IF_CYCLE',     'ONDEMAND',      '수시',           NULL,                              6),
    ('TRIGGER_TYPE', 'EAI_CALL',      'EAI 호출',       'EAI 가 처리 후 직접 호출',        1),
    ('TRIGGER_TYPE', 'SCHEDULE',      '스케줄',         'cron / 배치 스케줄러',            2),
    ('TRIGGER_TYPE', 'EVENT',         '이벤트',         '트리거 / 큐 수신',                3),
    ('TRIGGER_TYPE', 'MANUAL',        '수동',           '담당자가 직접 실행',              4),
    ('STEP_TYPE',    'TABLE_INSERT',  '테이블 적재',    '대상 테이블에 데이터를 넣는다',   1),
    ('STEP_TYPE',    'TABLE_UPDATE',  '테이블 갱신',    NULL,                              2),
    ('STEP_TYPE',    'PROC_CALL',     '프로시저 호출',  '패키지/프로시저 호출',            3),
    ('STEP_TYPE',    'API_CALL',      'API 호출',       NULL,                              4),
    ('STEP_TYPE',    'FILE_TRANSFER', '파일 전송',      NULL,                              5),
    ('STEP_TYPE',    'QUEUE',         '큐 발행/수신',   NULL,                              6),
    ('STEP_TYPE',    'BATCH',         '배치 실행',      NULL,                              7),
    ('STEP_TYPE',    'VALIDATE',      '검증',           '적재 결과 확인',                  8),
    ('OBJECT_TYPE',  'TABLE',         '테이블',         NULL,                              1),
    ('OBJECT_TYPE',  'VIEW',          '뷰',             NULL,                              2),
    ('OBJECT_TYPE',  'PACKAGE',       '패키지',         NULL,                              3),
    ('OBJECT_TYPE',  'PROCEDURE',     '프로시저',       NULL,                              4),
    ('OBJECT_TYPE',  'FUNCTION',      '함수',           NULL,                              5),
    ('OBJECT_TYPE',  'API',           'API',            NULL,                              6),
    ('OBJECT_TYPE',  'FILE',          '파일',           NULL,                              7),
    ('SYSTEM_KIND',  'EAI',           'EAI',            NULL,                              1),
    ('SYSTEM_KIND',  'ORACLE',        'Oracle DB',      NULL,                              2),
    ('SYSTEM_KIND',  'POSTGRESQL',    'PostgreSQL',     NULL,                              3),
    ('SYSTEM_KIND',  'WEB',           '웹 애플리케이션', NULL,                             4),
    ('SYSTEM_KIND',  'EXTERNAL',      '외부 시스템',    NULL,                              5),
    ('NOTE_TYPE',    'MEMO',          '메모',           NULL,                              1),
    ('NOTE_TYPE',    'ISSUE',         '이슈',           'done_yn=N 이면 미해결로 집계된다', 2),
    ('NOTE_TYPE',    'CHANGE',        '변경이력',       NULL,                              3),
    ('NOTE_TYPE',    'TEST',          '테스트기록',     NULL,                              4)
ON CONFLICT (code_grp, code) DO NOTHING;

-- ── 표·칸 설명 ──────────────────────────────────────────────
--
-- 원본이 달고 있던 것을 그대로 옮긴다. 이 표들은 칸 이름만 봐서는 뜻을 알 수
-- 없는 것이 많다(`new_dev2`·`mon_req_comp`·`complate_real_big_yn`). 설명이
-- 빠지면 그 값을 다시 알아내는 데 사람을 찾아가야 한다.

COMMENT ON TABLE projmng.wbs_docs IS '개발자 정보 공유 문서 (WBS 대시보드 · 개발자 정보 메뉴)';
COMMENT ON COLUMN projmng.wbs_docs.id IS '문서 섹션 식별자 (serial)';
COMMENT ON COLUMN projmng.wbs_docs.title IS '섹션 제목. 화면 좌측 목차에 표시 (예: 계정정보)';
COMMENT ON COLUMN projmng.wbs_docs.content IS '섹션 본문 HTML. 화면의 리치텍스트 편집기에서 작성한 결과';
COMMENT ON COLUMN projmng.wbs_docs.sort_order IS '섹션 표시 순서 (작은 값 우선, 10 단위 증가)';
COMMENT ON COLUMN projmng.wbs_docs.updated_by IS '최종 수정자 (미사용 — 화면에 인증이 없어 기록하지 않음)';
COMMENT ON COLUMN projmng.wbs_docs.updated_at IS '최종 수정 시각. 섹션 머리글에 표시';
COMMENT ON TABLE projmng.wbs_user IS '개발자 마스터. 성명·직급·계정 발급 현황과 접근 허용 IP 를 관리 (WBS 대시보드 #/devusers)';
COMMENT ON COLUMN projmng.wbs_user.bp_id IS '사번(BP ID). 키. hhip_wbs_wrk2.user_bp_id / user_real_id 와 매칭';
COMMENT ON COLUMN projmng.wbs_user.name IS '성명. 대시보드의 담당자·개발자 표기에 사용';
COMMENT ON COLUMN projmng.wbs_user.email IS '사내 메일 주소';
COMMENT ON COLUMN projmng.wbs_user.git IS 'GitLab(code.hd.com) 계정 발급 여부 (O/X)';
COMMENT ON COLUMN projmng.wbs_user.startkit IS 'StartKit 제공 여부 (O/X)';
COMMENT ON COLUMN projmng.wbs_user.dxb IS 'DX Builder 사용 환경 준비 여부 (O/X)';
COMMENT ON COLUMN projmng.wbs_user.vm_conn IS '개발 VM 접속 가능 여부 (O/X)';
COMMENT ON COLUMN projmng.wbs_user.aipro IS 'AI Pro 계정 발급 여부 (O/X)';
COMMENT ON COLUMN projmng.wbs_user.claudecode IS 'Claude Code 사용 가능 여부 (O/X)';
COMMENT ON COLUMN projmng.wbs_user.dev_db IS '개발 DB 접속 계정 발급 여부 (O/X)';
COMMENT ON COLUMN projmng.wbs_user.wiki IS '차세대 Wiki 계정 발급 여부 (O/X)';
COMMENT ON COLUMN projmng.wbs_user.projectview IS 'ProjectView(dev-wbs.hd.com) 계정 발급 여부 (O/X)';
COMMENT ON COLUMN projmng.wbs_user.svn IS 'SVN 계정 발급 여부 (O/X)';
COMMENT ON COLUMN projmng.wbs_user.notebook IS '개발용 노트북 지급 여부 (O/X)';
COMMENT ON COLUMN projmng.wbs_user.hub_hdmi IS 'USB 허브 / HDMI 케이블 지급 여부 (O/X)';
COMMENT ON COLUMN projmng.wbs_user.summer_size IS '하복 사이즈';
COMMENT ON COLUMN projmng.wbs_user.winter_size IS '동복 사이즈';
COMMENT ON COLUMN projmng.wbs_user.position_nm IS '직급';
COMMENT ON COLUMN projmng.wbs_user.use_ip IS '사용 IP';
COMMENT ON COLUMN projmng.wbs_user.mac_addr IS 'MAC 주소 (노트북 유선/무선)';
COMMENT ON COLUMN projmng.wbs_user.notebook_no IS '노트북 장비번호';
COMMENT ON COLUMN projmng.wbs_user.notebook_chk_no IS '노트북 확인번호';
COMMENT ON COLUMN projmng.wbs_user.tel_no IS '전화번호 (휴대폰/내선)';
COMMENT ON COLUMN projmng.wbs_user.birth_dt IS '생년월일';
COMMENT ON COLUMN projmng.wbs_user.pv_user_id IS 'ProjectView user id (USR-...), used as the workflow charger.';
COMMENT ON COLUMN projmng.wbs_user.block_yn IS '차단여부 — o 면 등록 IP 라도 접근 차단 (루프백 제외). 빈 값/x = 허용';
COMMENT ON COLUMN projmng.wbs_user.monitor1_no IS '모니터1 장비번호';
COMMENT ON COLUMN projmng.wbs_user.monitor1_chk_no IS '모니터1 확인번호';
COMMENT ON COLUMN projmng.wbs_user.monitor2_no IS '모니터2 장비번호';
COMMENT ON COLUMN projmng.wbs_user.monitor2_chk_no IS '모니터2 확인번호';
COMMENT ON COLUMN projmng.wbs_user.monitor3_no IS '모니터3 장비번호';
COMMENT ON COLUMN projmng.wbs_user.monitor3_chk_no IS '모니터3 확인번호';
COMMENT ON COLUMN projmng.wbs_user.super_yn IS 'Super 권한 — o 면 최고 관리자 (appsettings 의 Admin:BpIds 와 합집합)';
COMMENT ON COLUMN projmng.wbs_user.emerg_tel_no IS '비상연락처 (가족·동거인 등 본인 외 연락처)';
COMMENT ON TABLE projmng.wbs_user_pref IS 'Per-user UI preferences, keyed by dev_user.bp_id. JSON text in pref_val.';
COMMENT ON TABLE projmng.wbs_pv IS 'ProjectView work cache - identity, snapshot, send history. Cache only, never authoritative.';
COMMENT ON TABLE projmng.wbs_pv_node IS 'ProjectView workflow nodes (stage / date / charger) per task. Cache only.';
COMMENT ON TABLE projmng.wbs_pv_task IS 'ProjectView workflow task cache for the "fill empty date/charger" screen.';
COMMENT ON COLUMN projmng.wbs_pv_task.pv_charger_id IS 'ProjectView user id (USR-...) found on the task itself.';
COMMENT ON COLUMN projmng.wbs_pv_task.pv_status IS 'Current workflow stage name of the task.';
COMMENT ON COLUMN projmng.wbs_pv_task.pv_status_at IS 'When the task entered pv_status (from task-history).';
COMMENT ON TABLE projmng.wbs_task IS '화면(메뉴)별 일감 관리. hhip_wbs_wrk2 와 activity_id 로 1:N 연결. WBS 대시보드 상세목록의 일감 컬럼에서 등록·수정·완료·삭제';
COMMENT ON COLUMN projmng.wbs_task.task_id IS '일감 식별자 (serial, PK)';
COMMENT ON COLUMN projmng.wbs_task.activity_id IS 'hhip_wbs_wrk2.activity_id (부모 화면). 1:N 연결 키';
COMMENT ON COLUMN projmng.wbs_task.task_div IS '일감 구분 (개발/확인/문의/버그/기타 — 자유 입력 가능)';
COMMENT ON COLUMN projmng.wbs_task.memo IS '일감 내용 메모';
COMMENT ON COLUMN projmng.wbs_task.done_yn IS '일감 완료 여부 (''o'' = 완료, NULL = 미완료)';
COMMENT ON COLUMN projmng.wbs_task.sort_order IS '같은 화면 안에서의 표시 순서 (작은 값 우선)';
COMMENT ON COLUMN projmng.wbs_task.created_at IS '등록 시각';
COMMENT ON COLUMN projmng.wbs_task.updated_at IS '최종 수정 시각';
COMMENT ON TABLE projmng.wbs_work IS '자재관리(MM) 화면 단위 WBS 작업표. 1행 = 1화면(메뉴). 엑셀 WBS(activity_id 기준)와 동기화되며 WBS 대시보드(#/rows)의 원본 테이블';
COMMENT ON COLUMN projmng.wbs_work.module IS '업무 대분류 코드 (전 행 MM = 자재관리)';
COMMENT ON COLUMN projmng.wbs_work.module_name IS '업무 대분류명 (자재관리)';
COMMENT ON COLUMN projmng.wbs_work.systemcode IS '서브시스템(모듈) 코드 — PMM001~PMM018. 소스 리포지토리/폴더명과 일치';
COMMENT ON COLUMN projmng.wbs_work.system_nm IS '서브시스템(모듈)명 — 예: 입고 관리, PO(발주서 관리)';
COMMENT ON COLUMN projmng.wbs_work.program_id IS '프로그램 ID — {모듈}.{화면그룹} 형식. 예: PMM004.A010001';
COMMENT ON COLUMN projmng.wbs_work.menu_nm IS '화면(메뉴)명 — [화면코드]명칭 형식. 예: [4190]국내자재 입고. 대시보드 목록의 주 표시 항목';
COMMENT ON COLUMN projmng.wbs_work.comment IS '개발 준비 상태 메모 — backend/frontend 존재 여부와 재작업 범위';
COMMENT ON COLUMN projmng.wbs_work.activity_id IS 'WBS Activity ID (AAC-Annn). 엑셀 WBS와 데이터를 맞추는 매칭 키';
COMMENT ON COLUMN projmng.wbs_work.plan_sdt IS '계획시작일. 엑셀 WBS가 원본이며 화면에서는 읽기 전용';
COMMENT ON COLUMN projmng.wbs_work.plan_edt IS '계획종료일. 대시보드 월/주 집계의 기본 기준일(basis=edt). 엑셀 WBS가 원본, 화면 읽기 전용';
COMMENT ON COLUMN projmng.wbs_work.priority_order IS '개발 우선순위 (1=선행, 2=후행). 대시보드에서 수정 가능';
COMMENT ON COLUMN projmng.wbs_work.plan_sdt_c IS '실적시작일. 비어 있고 계획시작일이 도래하면 착수지연(#/delay)으로 집계. 화면 읽기 전용';
COMMENT ON COLUMN projmng.wbs_work.plan_edt_c IS '실적종료일. 비어 있고 계획종료일이 도래하면 종료지연(#/delay)으로 집계. 화면 읽기 전용';
COMMENT ON COLUMN projmng.wbs_work.prog_type IS '화면 유형 — 단일그리드(CRUD) / 마스터 디테일 / 조회·리포트 전용 등';
COMMENT ON COLUMN projmng.wbs_work.prog_type_desc IS '화면 기능 요약 (조회·입고, 분류코드 CRUD 등)';
COMMENT ON COLUMN projmng.wbs_work.trg_exsit_chk IS 'AS-IS 대상 테이블의 트리거 존재 여부 확인 결과';
COMMENT ON COLUMN projmng.wbs_work.trg_use_chk IS '해당 화면에서 실제로 발화하는 트리거명과 발화 시점. 없음/미확인/확인필요 값도 사용';
COMMENT ON COLUMN projmng.wbs_work.trg_evt_rel IS '트리거와 연계된 이벤트(자동 후처리 시점) 설명';
COMMENT ON COLUMN projmng.wbs_work.etc_desc IS '기타 특이사항 (특수 기능·주의점). 없으면 — 표기';
COMMENT ON COLUMN projmng.wbs_work.new_dev2 IS '실작업대상 여부';
COMMENT ON COLUMN projmng.wbs_work.asis_cs IS 'AS-IS 화면의 C# 소스 파일명 (예: P_30D1030_9.cs). 이관 대상 원본 추적용';
COMMENT ON COLUMN projmng.wbs_work.report_use IS '리포트 사용';
COMMENT ON COLUMN projmng.wbs_work.sheet_use IS '엑셀 사용 여부';
COMMENT ON COLUMN projmng.wbs_work.dwg_view_use IS '도면 조회 사용 여부';
COMMENT ON COLUMN projmng.wbs_work.por_view_use IS 'POR 조회 사용 여부';
COMMENT ON COLUMN projmng.wbs_work.menu_desc IS '화면 설명';
COMMENT ON COLUMN projmng.wbs_work.mon_req_comp IS '9월까지 완료 요청본';
COMMENT ON COLUMN projmng.wbs_work.user_bp_id IS '담당자 사번(BP ID). projmng.wbs_user.bp_id 와 매칭해 성명을 표시하며, dev_user 에 없는 값은 화면에서 미할당으로 처리. 대시보드에서 수정 가능';
COMMENT ON COLUMN projmng.wbs_work.complate_yn IS '완료여부';
COMMENT ON COLUMN projmng.wbs_work.comp_desc IS '완료에따른코멘트';
COMMENT ON COLUMN projmng.wbs_work.user_real_id IS '실제우리들의계획사용자';
COMMENT ON COLUMN projmng.wbs_work.complate_real_yn IS '우리들의 진짜 yn';
COMMENT ON COLUMN projmng.wbs_work.recheck_yn IS '재확인 요청 여부 (''o'' = 개발자에게 재확인 요청)';
COMMENT ON COLUMN projmng.wbs_work.db_ready_big_yn IS '대형 DB 준비여부 - o = ready';
COMMENT ON TABLE projmng.if_attr IS '추가 관리항목의 값';
COMMENT ON TABLE projmng.if_attr_def IS '추가 관리항목의 정의. 대시보드가 이걸 읽어 입력폼을 만든다';
COMMENT ON TABLE projmng.if_code IS '인터페이스 카탈로그 공통코드';
COMMENT ON COLUMN projmng.if_code.code_grp IS '코드 그룹 (IF_STATUS/IF_DIRECTION/STEP_TYPE/OBJECT_TYPE/IF_CYCLE/SYSTEM_KIND/NOTE_TYPE/ATTR_TYPE)';
COMMENT ON TABLE projmng.if_master IS '인터페이스 기본정보. 처리 단계는 if_step 에 둔다';
COMMENT ON COLUMN projmng.if_master.if_cd IS '인터페이스 코드. 프로그램이 남기는 IF_ID 와 같은 값을 쓴다 (예: HHIP-IF-01)';
COMMENT ON COLUMN projmng.if_master.ext IS '정형화되지 않은 추가 값. 항목을 정식으로 관리하려면 if_attr_def 를 쓴다';
COMMENT ON TABLE projmng.if_note IS '인터페이스별 메모/이슈/변경이력. step_id 를 채우면 특정 단계에 붙는다';
COMMENT ON TABLE projmng.if_step IS '인터페이스 처리 단계. 순서는 step_no 가 정한다';
COMMENT ON COLUMN projmng.if_step.params IS '파라미터 규격. 예: {"in":[{"name":"P_IFSEQ",...}],"out":[...]}';
COMMENT ON TABLE projmng.if_system IS '연계 대상 시스템/DB. 비밀번호는 보관하지 않는다';

-- 새로 생긴 칸 셋.
COMMENT ON COLUMN projmng.wbs_work.prj_rid  IS '프로젝트 번호 (projmng.dev_proj.prj_rid). 원본에는 없던 칸 — 프로젝트 하나 전용이었다';
COMMENT ON COLUMN projmng.wbs_user.prj_rid  IS '프로젝트 번호 (projmng.dev_proj.prj_rid)';
COMMENT ON COLUMN projmng.wbs_user.login_id IS '포털 계정(로그인 아이디). 채우면 화면이 포털에서 이름과 얼굴을 가져온다. 비면 name 칸을 쓴다';

COMMENT ON COLUMN projmng.wbs_user.use_ip   IS '사용 IP. 원본에서는 접근 허가 목록이었으나 포털 안에서는 장비 대장으로만 쓴다 — 권한 판정에 쓰지 않는다';
COMMENT ON COLUMN projmng.wbs_user.super_yn IS '원본의 최고 관리자 표시. 포털 역할이 그 일을 하므로 권한 판정에 쓰지 않는다';
COMMENT ON COLUMN projmng.wbs_user.block_yn IS '원본의 차단 표시. 위와 같은 이유로 권한 판정에 쓰지 않는다';


COMMIT;


-- ════════════════════════════════════════════════════════════
--  자료를 옮길 때 (여기는 돌지 않는다)
-- ════════════════════════════════════════════════════════════
--
-- 원본 꾸러미가 담아 온 프로젝트 자료 여섯 줄이다. 어느 프로젝트의 것인지는
-- 옮기는 사람이 정한다 — `:prj` 를 실제 `dev_proj.prj_rid` 로 바꾸고 돌린다.
--
--   INSERT INTO projmng.if_system
--       (prj_rid, system_cd, system_nm, system_kind, host, port, db_nm, schema_nm, system_desc, sort_order)
--   VALUES
--       (:prj, 'EAI',      'EAI 시스템',          'EAI',        NULL,           NULL, NULL,       NULL,     '인터페이스 중계. 적재 후 후행 프로시저를 호출한다', 1),
--       (:prj, 'SUBIC_ORA','수빅 DB (ORAHHIP)',   'ORACLE',     '10.103.13.10', 1521, 'ORAHHIP',  NULL,     '수빅 업무 DB. Oracle 19c',                          2),
--       (:prj, 'LOCAL_PG', '개발업무 관리 DB',    'POSTGRESQL', 'localhost',    5432, 'postgres', 'public', 'wbs 대시보드가 쓰던 로컬 DB',                       3)
--   ON CONFLICT (prj_rid, system_cd) DO NOTHING;
--
--   INSERT INTO projmng.if_attr_def (prj_rid, attr_cd, attr_nm, attr_type, attr_desc, sort_order)
--   VALUES
--       (:prj, 'LOG_TABLE', '호출이력 테이블',      'TEXT', '인터페이스 호출 이력이 쌓이는 테이블',    1),
--       (:prj, 'EAI_IF_NO', 'EAI 인터페이스 번호',  'TEXT', 'EAI 쪽에서 부여한 번호',                  2),
--       (:prj, 'RETRY_YN',  '재처리 가능 여부',     'YN',   '실패 시 같은 IFSEQ 로 재호출 가능한가',   3)
--   ON CONFLICT (prj_rid, attr_cd) DO NOTHING;
--
-- 원장·일감·개발자 명부는 사내 DB 에서 표별로 뽑아 `prj_rid` 를 채워 넣는다.
--
--   INSERT INTO projmng.wbs_work (prj_rid, activity_id, module, …)
--   SELECT :prj, activity_id, module, … FROM 옮겨온_hhip_wbs_wrk2
--    WHERE activity_id IS NOT NULL AND activity_id <> '';
--
-- **`activity_id` 가 빈 줄은 안 들어간다.** 원본에는 기본키가 없어서 그런 줄이
-- 있을 수 있는데, 여기서는 그것이 열쇠다 — 조용히 버려지지 않도록 위 조건을
-- 먼저 돌려 몇 줄이 걸리는지 세어 보고 옮긴다.
