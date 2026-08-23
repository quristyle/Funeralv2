/**
 * 프로젝트관리(ProjMngServer) 요청·응답 타입.
 *
 * 이 서비스는 화면마다 엔드포인트가 있는 구조가 아니다.
 * **저장 프로시저 이름을 실어 보내면 그 결과를 그대로 돌려주는 범용 통로**다.
 * 업무 로직은 전부 DB(projmng 스키마)의 프로시저에 있다.
 *
 * 그래서 프론트가 알아야 할 것은 두 가지뿐이다.
 *   - 어떤 프로시저를 어떤 파라미터로 부르는가 (RequestDto)
 *   - 결과의 컬럼 메타(cols)와 행(data)을 어떻게 그리는가 (ProjMngResult)
 */

/** 프로시저 호출 종류. 프로시저는 이 값을 `req_type` 파라미터로 받는다. */
export type ProcType = 'delete' | 'save' | 'srch' | string;

/** ProjMngServer 로 보내는 요청 봉투 (서버의 `RequestDto` 와 1:1) */
export interface ProjMngRequest {
  /** 호출할 프로시저 이름. `sp_` 로 시작하면 업무 프로시저, `md_` 는 서버측 파일 스캔 */
  ProcName: string;
  /** srch / save / delete — 프로시저가 `req_type` 으로 받는다 */
  ProcType?: ProcType;
  /**
   * 로그인 사용자 아이디.
   *
   * 프론트에서 채워 보내도 서버가 게이트웨이 신원(`X-User-Id`)으로 덮어쓴다
   * (`Filters/UserIdentityActionFilter.cs`). 위조해도 의미가 없으므로 비워 보낸다.
   */
  SSUserId?: string;
  /** fast api 사용 여부. 현 서버 구현에서는 로그에만 쓰인다 */
  IsFast?: boolean;
  /** 프로젝트에 정의된 외부 DB 로 붙을지 여부 (개발 도구 화면들이 쓴다) */
  IsProjDb?: boolean;
  /** 호출 시각. 서버가 다시 채운다 */
  Start?: string;
  /** 프로시저 파라미터. 값은 모두 문자열로 넘긴다 */
  MainParam?: Record<string, string>;
  /** 다건 저장용 변경 행 목록. 비어 있지 않으면 서버가 다건 처리 경로로 간다 */
  MultyData?: Record<string, unknown>[];
}

/**
 * 응답 봉투 (서버의 `ResultInfo<T>` 와 1:1).
 *
 * `code` 가 0 이상이면 성공, 음수면 실패다.
 * 다른 MSA 의 `{ code: 'S000' }` 규약과 다르므로 전용 클라이언트에서 흡수한다.
 */
export interface ProjMngResult<T = ProjMngRow> {
  code: number;
  message: string;
  /** 실행 시간·파라미터 등 부가 정보 */
  res?: Record<string, unknown> | null;
  /**
   * 컬럼 메타. `{ 컬럼명: .NET 타입명 }` 형태다.
   * 예: `{ cm_cd: 'System.String', cm_srt: 'System.Int32', cre_dt: 'System.DateTime' }`
   *
   * 화면이 컬럼을 미리 알지 못해도 이 메타로 그리드를 만들 수 있다.
   */
  cols?: null | Record<string, string>;
  data?: null | T[];
}

/** 그리드 한 행. 컬럼이 고정되어 있지 않아 이름으로 접근한다. */
export type ProjMngRow = Record<string, unknown>;

/** 그리드가 편집 중인 행에 붙이는 표시. 저장할 때 이 행만 골라 보낸다. */
export const CHANGE_FLAG = 'quri_ischange';

/** 공통코드 한 건 (`sp_projCommon` 결과) */
export interface CommonCodeItem {
  code: string;
  name: string;
  desc?: string;
  /** 프로시저가 함께 돌려주는 나머지 컬럼 전체 (db_type, db_nick, db_schema 등) */
  others: Record<string, string>;
}
