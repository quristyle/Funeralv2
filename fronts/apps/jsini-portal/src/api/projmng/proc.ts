/**
 * 프로시저 호출 래퍼.
 *
 * 이식 전 Blazor 의 `BaseComponent` 가 들고 있던 헬퍼들을 그대로 옮긴 것이다.
 *   DbCont   → dbCont    업무 프로시저 조회        (POST /Proj)
 *   DbSave   → dbSave    변경 행 다건 저장         (POST /Proj, ProcType=save)
 *   DbDelete → dbDelete  삭제                      (POST /Proj, ProcType=delete)
 *   JsCont   → jsCont    개발 도구 · DB 메타 조회  (POST /Dev)
 *   MdCont   → mdCont    서버측 파일 스캔          (POST /Media)
 *   SysCont  → sysCont   시스템 조회(캐시 우회)    (POST /Proj/sys)
 *   GetCommon→ getCommon 공통코드                  (sp_projCommon)
 *
 * 이식하면서 달라진 점이 둘 있다.
 *   - `SSUserId` 를 보내지 않는다. 서버가 게이트웨이 신원으로 채운다.
 *   - 프로시저 이름 규칙 위반(`sp_` / `md_` 로 시작하지 않음)은 호출 전에 걸러 낸다.
 *     이식 전과 같은 방어인데, 오타가 그대로 DB 로 나가는 것을 막는다.
 */
import type {
  CommonCodeItem,
  ProcType,
  ProjMngResult,
  ProjMngRow,
} from './types';

import { message } from 'ant-design-vue';

import { fetchBizOptions } from '#/api/biz-select';

import {
  emptyResult,
  PROJMNG_URL,
  projmngPost,
  projmngPostPlain,
  toParam,
} from './request';
import { CHANGE_FLAG } from './types';

// `toParam` 은 요청 규약(MainParam 은 문자열 사전)의 일부라 request.ts 로 옮겼다.
// 예전부터 여기서 가져다 쓰던 자리를 지키려고 그대로 다시 내보낸다.
export { toParam };

function invalidName(procName: string, prefix: string) {
  message.warning(`프로시저 이름 규칙 위반: ${procName}`);
  return emptyResult(`'${prefix}' 로 시작하는 이름이어야 합니다: ${procName}`);
}

// ============================================================
// 업무 프로시저 (sp_*)
// ============================================================

/**
 * 조회. 이식 전 `DbCont<T>(procName, dic, procType)` 와 같다.
 *
 * @param isServerFix true 면 `/Proj/sys` 로 보낸다. 서버 캐시를 타지 않아야 하는 조회다.
 */
export async function dbCont<T = ProjMngRow>(
  procName: string,
  dic?: Record<string, unknown> | null,
  procType: ProcType = 'srch',
  options?: { isFast?: boolean; isProjDb?: boolean; isServerFix?: boolean },
): Promise<ProjMngResult<T>> {
  if (!procName?.startsWith('sp_') || procName.length < 6) {
    return invalidName(procName, 'sp_') as ProjMngResult<T>;
  }

  return projmngPost<T>(
    options?.isServerFix ? PROJMNG_URL.projSys : PROJMNG_URL.proj,
    {
      ProcName: procName,
      ProcType: procType,
      IsFast: options?.isFast ?? false,
      IsProjDb: options?.isProjDb ?? false,
      MainParam: toParam(dic),
    },
  );
}

/**
 * 그리드에서 변경된 행만 골라 다건 저장한다.
 *
 * 그리드는 편집한 행에 `quri_ischange` 를 붙여 둔다. 여기서 그 행만 추려
 * 표시를 떼고 `MultyData` 로 보낸다 — 이식 전 `GetChangeData` 와 같은 규약이다.
 * 저장 후에는 원본 행의 표시도 지워 준다(재저장 방지).
 */
export async function dbSave<T = ProjMngRow>(
  procName: string,
  dic: Record<string, unknown> | null | undefined,
  rows: ProjMngRow[] | null | undefined,
  options?: { isFast?: boolean; procType?: ProcType },
): Promise<ProjMngResult<T>> {
  if (!procName?.startsWith('sp_') || procName.length < 6) {
    return invalidName(procName, 'sp_') as ProjMngResult<T>;
  }

  const changed = (rows ?? []).filter((row) => isChanged(row));

  if (changed.length === 0) {
    message.warning('수정대상이 존재하지 않습니다.');
    return { code: -77, message: '수정대상이 존재하지 않습니다.', data: [] };
  }

  const multyData = changed.map((row) => {
    const copy = { ...row };
    delete copy[CHANGE_FLAG];
    return copy;
  });

  const result = await projmngPost<T>(PROJMNG_URL.proj, {
    ProcName: procName,
    ProcType: options?.procType ?? 'save',
    IsFast: options?.isFast ?? false,
    MainParam: toParam(dic),
    MultyData: multyData,
  });

  if (result.code >= 0) {
    // 저장이 끝난 행의 변경 표시를 지운다.
    changed.forEach((row) => delete row[CHANGE_FLAG]);
    message.success(result.message || '저장했습니다.');
  }

  return result;
}

/** 단건 삭제. 이식 전 `DbDelete` 와 같다. */
export async function dbDelete<T = ProjMngRow>(
  procName: string,
  dic?: Record<string, unknown> | null,
  options?: { isFast?: boolean },
): Promise<ProjMngResult<T>> {
  const result = await dbCont<T>(procName, dic, 'delete', options);
  if (result.code >= 0) {
    message.success(result.message || '삭제했습니다.');
  }
  return result;
}

/** 행이 편집되었는지. `quri_ischange` 는 서버가 아니라 그리드가 붙이는 표시다. */
export function isChanged(row: ProjMngRow | null | undefined): boolean {
  if (!row) return false;
  const flag = row[CHANGE_FLAG];
  return flag === true || flag === 'true';
}

// ============================================================
// 개발 도구 (/Dev)
// ============================================================

/**
 * 개발 도구 조회. 프로시저 이름 규칙을 따르지 않는 액션 이름을 쓴다
 * (`tablelist`, `proclist`, `columnsOftable`, `tableCommentUpdate` …).
 *
 * `isProjDb` 를 켜지 않는 것이 기본이다. 서버는 이 플래그로 쿼리를 어디서 찾을지
 * 가른다.
 *   꺼짐(기본) → `projmng.devsqlresp` — DB 종류별로 등록된 시스템 쿼리
 *                (`tablelist` 등이 여기 있다. [DB 로직] 화면에서 편집한다)
 *   켜짐       → `projmng.dev_db_prop` — 그 DB 한 개에만 등록된 쿼리
 *                (`code_master` 처럼 프로젝트마다 다른 것)
 *
 * 켜야 할 곳에서 끄면(또는 그 반대로 하면) 쿼리를 못 찾아 실패한다.
 * 원본에서 켜는 곳은 `JsProcDbReturn` 하나뿐이고, `jsProcDb()` 가 그 자리다.
 */
export async function jsCont<T = ProjMngRow>(
  actionName: string,
  dic?: Record<string, unknown> | null,
  options?: { isProjDb?: boolean },
): Promise<ProjMngResult<T>> {
  return projmngPost<T>(PROJMNG_URL.dev, {
    ProcName: actionName,
    IsProjDb: options?.isProjDb ?? false,
    MainParam: toParam(dic),
  });
}

/**
 * 프로젝트에 등록된 외부 DB 로 붙어 조회한다.
 * 이식 전 `JsProcDbReturn` — DB 선택 드롭다운의 값이 파라미터로 함께 간다.
 */
export async function jsProcDb<T = ProjMngRow>(
  actionName: string,
  dbCode: ProjDbParam | null,
  dic?: Record<string, unknown> | null,
): Promise<ProjMngResult<T>> {
  return jsCont<T>(actionName, { ...projDbParams(dbCode), ...dic }, {
    isProjDb: true,
  });
}

/** DB 선택 드롭다운에서 고른 항목을 프로시저 파라미터로 바꾼다. */
export interface ProjDbParam {
  code?: string;
  others?: Record<string, string>;
}

export function projDbParams(db?: null | ProjDbParam): Record<string, string> {
  return {
    db: db?.others?.db_type ?? '',
    dbnick: db?.others?.db_nick ?? '',
    schema: db?.others?.db_schema ?? '',
    db_rid: db?.code ?? '',
  };
}

/**
 * 직접 쿼리 실행.
 *
 * 서버가 역할을 한 번 더 확인한다(`DevTools:RawSqlRoles`, 기본값은 관리자 역할만).
 * 권한이 없으면 403 과 함께 실패 메시지가 온다.
 */
export async function rawSql<T = ProjMngRow>(
  dbNick: string,
  query: string,
  isBreakCnt = false,
): Promise<ProjMngResult<T>> {
  return projmngPostPlain<T>(PROJMNG_URL.devSql, {
    db_nick: dbNick,
    query,
    isBreakCnt: isBreakCnt ? 'true' : '',
  });
}

// ============================================================
// 서버측 파일 스캔 (/Media, md_*)
// ============================================================

/** 서버 파일시스템을 훑어 소스·서비스 정의를 읽어 온다. */
export async function mdCont<T = ProjMngRow>(
  mdName: string,
  dic?: Record<string, unknown> | null,
): Promise<ProjMngResult<T>> {
  if (!mdName?.startsWith('md_') || mdName.length < 6) {
    return invalidName(mdName, 'md_') as ProjMngResult<T>;
  }
  return projmngPost<T>(PROJMNG_URL.media, {
    ProcName: mdName,
    MainParam: toParam(dic),
  });
}

// ============================================================
// 시스템 (/Sys)
// ============================================================

/** 서버가 들고 있는 DB 접속 정보 캐시를 비운다. */
export async function sysClearCache(
  name: string,
  dic?: Record<string, unknown> | null,
): Promise<ProjMngResult> {
  return projmngPost(PROJMNG_URL.sys, {
    ProcName: name,
    MainParam: toParam({ req_cname: name, ...dic }),
  });
}

// ============================================================
// 공통코드
// ============================================================

/**
 * 공통코드 조회. 이식 전 `GetCommon(codeId, key)` 와 같다.
 *
 * **어디를 부르는지는 여기 적혀 있지 않다.** 포털·장례식장 셀렉트와 마찬가지로
 * DB 메타데이터(`scom.biz_select_configs` 의 `projmng_common` 행)가 정한다.
 *   MSA=projmng · POST /Proj · 고정 파라미터 `{ProcName:'sp_projCommon', ProcType:'srch'}`
 *   · 파라미터 경로 `MainParam` · 결과 경로 `data`
 * 프로젝트관리의 드롭다운은 전부 이 프로시저 하나를 `code_id` 만 바꿔 부르므로
 * 코드 종류마다 메타데이터를 만들지 않고 한 행에 다 태운다.
 *
 * 화면 여러 곳이 같은 코드를 부르므로 한 번 읽은 것은 캐시한다
 * (이식 전 Blazor 에서는 컴포넌트마다 매번 호출했다).
 */
const commonCache = new Map<string, CommonCodeItem[]>();

export async function getCommon(
  codeId: string,
  key = '',
  options?: { force?: boolean },
): Promise<CommonCodeItem[]> {
  const cacheKey = `${codeId} ${key}`;
  const cached = commonCache.get(cacheKey);
  if (!options?.force && cached) return cached;

  // `options` 는 이 함수의 파라미터 이름이라 가려지지 않게 다른 이름으로 받는다.
  const { items: rows, options: mapped } = await fetchBizOptions(
    'projmng_common',
    { code_id: codeId, etc0: key },
  );

  // 코드·이름은 메타데이터가 지정한 필드(labelField/valueField)를 따르고,
  // 나머지 컬럼(db_nick·db_schema 등)은 원본 행을 그대로 others 로 넘긴다.
  // 값을 문자열로 바꾸지 않는다. code 가 숫자로 오는 코드(projlist·projdb)가 있고,
  // 이식 전부터 화면들이 그 타입 그대로 v-model 에 담아 프로시저로 되돌려 보낸다.
  const items: CommonCodeItem[] = rows.map((row, index) => ({
    code: mapped[index]?.value ?? row.code ?? '',
    name: mapped[index]?.label ?? row.name ?? '',
    desc: row.desc ?? '',
    others: row,
  }));

  commonCache.set(cacheKey, items);
  return items;
}

/** 공통코드 캐시를 비운다. 코드를 편집하는 화면이 저장 후에 부른다. */
export function clearCommonCache() {
  commonCache.clear();
}
