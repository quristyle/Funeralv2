/**
 * 메타데이터로 셀렉트 목록을 읽어 오는 단일 통로.
 *
 * 설정은 전부 DB(`scom.biz_select_configs`)에 있다. 화면은 `bizType` 만 알면 되고
 * "어느 MSA 의 어느 API 를 어떤 모양으로 부르는가" 는 여기서 정한다.
 *
 * **MSA 를 왜 따로 들고 있나 — 프리픽스 때문이 아니다.**
 * 서비스마다 응답 봉투가 다르고, 그 차이는 각 요청 클라이언트가 흡수하고 있다.
 *   auth·funeral·file·ai : `{ code:'S000', data }`      → requestClient  (data 를 벗겨 준다)
 *   helpdesk             : `{ success:true, data }`     → helpdeskClient (data 를 벗겨 준다)
 *   projmng              : `{ code:0, cols, data }`     → projmngClient  (봉투째 준다)
 * 그래서 `serviceCode` 는 **봉투를 벗길 클라이언트를 고르는 키**다. URL 만 조립해서는
 * 헬프데스크·프로젝트관리를 태울 수 없다.
 *
 * `resultPath` 는 그 클라이언트가 이미 벗기고 남은 것에서 목록을 찾는 경로다.
 *   auth/funeral → 'result'   helpdesk → (비움, 이미 배열)   projmng → 'data'
 */
import type { BizSelectConfigApi } from '#/api/portal/system/biz-select-config';

import { helpdeskClient } from '#/api/helpdesk/request';
import { projmngRequest } from '#/api/projmng/request';
import { requestClient } from '#/api/request';
import { useBizSelectStore } from '#/store/biz-select-config';

export interface BizOption {
  label: string;
  value: any;
}

export interface BizOptionsResult {
  /** 서버가 준 원본 행. 라벨·값 말고 다른 컬럼이 필요한 화면이 쓴다. */
  items: any[];
  options: BizOption[];
}

const EMPTY: BizOptionsResult = { items: [], options: [] };

function parseStaticParams(raw?: string): Record<string, any> {
  if (!raw?.trim()) return {};
  try {
    const parsed = JSON.parse(raw);
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    console.warn('[BizSelect] static_params 가 올바른 JSON 이 아닙니다:', raw);
    return {};
  }
}

/**
 * 고정 파라미터와 화면이 넘긴 런타임 파라미터를 합친다.
 *
 * `paramPath` 가 있으면 런타임 파라미터를 그 자리에 넣는다. 프로젝트관리처럼
 * 프로시저 이름은 본문 최상위에, 조회 조건은 `MainParam` 안에 넣어야 하는 규약을
 * 코드가 아니라 메타데이터로 표현하기 위한 것이다.
 */
function buildParams(
  config: BizSelectConfigApi.BizSelectConfig,
  runtime?: Record<string, any>,
): Record<string, any> {
  const body: Record<string, any> = parseStaticParams(config.staticParams);
  const extra = runtime ?? {};

  if (!config.paramPath) return { ...body, ...extra };

  const parts = config.paramPath.split('.').filter(Boolean);
  let cursor = body;
  for (const part of parts.slice(0, -1)) {
    cursor[part] = { ...(cursor[part] ?? {}) };
    cursor = cursor[part];
  }
  const leaf = parts.at(-1) as string;
  cursor[leaf] = { ...(cursor[leaf] ?? {}), ...extra };
  return body;
}

function getValueByPath(obj: any, path?: null | string): any {
  if (!path) return obj;
  return path.split('.').reduce((acc, part) => {
    return acc && acc[part] !== undefined ? acc[part] : undefined;
  }, obj);
}

/** 트리 응답을 한 줄짜리 목록으로 편다 (부서처럼 계층이 있는 데이터). */
function flattenTree(list: any[]): any[] {
  const result: any[] = [];
  const recurse = (nodes: any[]) => {
    if (!Array.isArray(nodes)) return;
    for (const node of nodes) {
      result.push(node);
      if (node?.children?.length) recurse(node.children);
    }
  };
  recurse(list);
  return result;
}

/** 설정이 가리키는 MSA 의 클라이언트로 호출한다. */
async function call(
  config: BizSelectConfigApi.BizSelectConfig,
  params: Record<string, any>,
): Promise<any> {
  const method = (config.httpMethod || 'GET').toUpperCase();
  const service = config.serviceCode?.trim();

  if (service === 'helpdesk') {
    // 베이스 URL 에 /helpdesk 가 이미 붙어 있다.
    return method === 'GET'
      ? helpdeskClient.get<any>(config.apiUrl, { params })
      : helpdeskClient.post<any>(config.apiUrl, params);
  }

  if (service === 'projmng') {
    return projmngRequest(config.apiUrl, method, params);
  }

  // 포털 계열(auth·funeral·file·ai). serviceCode 가 비어 있으면 예전처럼
  // apiUrl 이 프리픽스를 이미 품고 있는 것으로 본다.
  const url = service ? `/${service}${config.apiUrl}` : config.apiUrl;

  return method === 'GET'
    ? requestClient.get<any>(url, { params })
    : requestClient.post<any>(url, params);
}

/**
 * `bizType` 의 목록을 읽어 원본 행과 셀렉트 옵션을 함께 돌려준다.
 *
 * 설정이 없으면 경고만 남기고 빈 결과를 준다 — 메타데이터를 아직 안 넣은 화면이
 * 통째로 죽지 않게 한다.
 */
export async function fetchBizOptions(
  bizType: string,
  params?: Record<string, any>,
): Promise<BizOptionsResult> {
  if (!bizType) return EMPTY;

  const config = await useBizSelectStore().getConfigByType(bizType);
  if (!config) {
    console.warn(`[BizSelect] 메타데이터에 없는 타입입니다: ${bizType}`);
    return EMPTY;
  }

  const response = await call(config, buildParams(config, params));

  // resultPath 가 비어 있어도 응답이 포털 공통 봉투면 result 를 본다 (기존 폴백 유지).
  let path = config.resultPath;
  if (!path && response && response.result !== undefined) path = 'result';

  const raw = getValueByPath(response, path);
  let items = Array.isArray(raw) ? raw : [];

  if (config.processorType === 'FLATTEN') items = flattenTree(items);

  const labelField = config.labelField || 'name';
  const valueField = config.valueField || 'id';

  return {
    items,
    options: items.map((item: any) => ({
      label: item?.[labelField] ?? '',
      value: item?.[valueField],
    })),
  };
}
