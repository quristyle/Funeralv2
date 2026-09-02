/**
 * 응답 봉투를 벗기는 **단 하나의 기준.**
 *
 * 게이트웨이를 지나 오는 응답은 서비스가 달라도 모양이 같다.
 *
 * ```
 * { success: true, code: 'S000', message: 'Success',
 *   data: { result: [ … ], page: { total: 7 } } }
 * ```
 *
 * `requestClient` 는 `dataField: 'data'` 라 **`data` 까지만** 벗긴다
 * (`src/api/request.ts`). 그래서 API 함수가 손에 쥐는 값은 늘 `{ result, page }` 다.
 *
 * ## 왜 부르는 쪽이 정해야 하는가
 *
 * 봉투는 목록이든 객체 하나든 **똑같이** `result` 배열이다. 객체 하나도
 * `{ result: [obj], page: { total: 1 } }` 로 온다
 * (백엔드 `JSini.Shared.DTOs.ApiResponse.BuildSerializedData`).
 * 그래서 봉투만 보고는 '1건짜리 목록' 과 '객체 하나' 를 구분할 수 없다.
 * 자동으로 벗겨 주는 장치를 클라이언트에 두지 못하는 이유가 이것이다.
 *
 * 구분은 **엔드포인트를 아는 사람**만 할 수 있으므로, 그 선택을 이름으로
 * 드러내 두었다.
 *
 * | 쓸 곳 | 함수 | 결과 |
 * |---|---|---|
 * | 목록을 기대한다 | `unwrapList` | 항상 배열 |
 * | 객체 하나를 기대한다 | `unwrapOne` | 객체 또는 `undefined` |
 * | 총건수까지 필요하다 | `unwrapPage` | `{ items, total }` |
 *
 * ## 규칙
 *
 * - **API 모듈(`src/api/**`)에서만 부른다.** 화면은 이미 벗겨진 값을 받는다.
 *   화면에서 또 `res.result` 를 더듬는 코드가 생기면 이 경계가 무너진다.
 * - 세 함수 모두 봉투 · 맨배열 · 맨객체를 **모두** 받아 준다. 그래서 봉투를
 *   쓰지 않는 서비스(헬프데스크 · 프로젝트관리는 각자 전용 클라이언트가 있다)나
 *   아직 손으로 벗기던 옛 코드와 섞여도 터지지 않는다.
 * - **그리드(`proxyConfig.ajax.query`)에 넘길 값도 벗긴 배열로 준다.**
 *   `src/adapter/vxe-table.ts` 가 배열과 봉투를 모두 받도록 되어 있다.
 */

/** `{ result, page }` 봉투. `page` 는 없을 수도 있다. */
interface ResultEnvelope {
  page?: { total?: number };
  result?: unknown;
}

/** 봉투인가 — `result` 칸을 가진 객체인지로 판단한다. */
function isResultEnvelope(value: unknown): value is ResultEnvelope {
  return (
    !!value &&
    typeof value === 'object' &&
    !Array.isArray(value) &&
    'result' in (value as object)
  );
}

/**
 * 봉투가 한 겹 더 남아 있는가 (`{ success, code, data }` 통째).
 *
 * `responseReturn: 'raw'`/`'body'` 로 받은 값이나 다른 층을 거쳐 온 값이
 * 섞여 들어올 때가 있다. `data` 라는 칸을 가진 **평범한 DTO** 를 잘못
 * 파고들지 않도록, 봉투의 표식(`code` · `success`)이 함께 있을 때만 인정한다.
 */
function isFullResponse(value: unknown): value is { data?: unknown } {
  if (!value || typeof value !== 'object' || Array.isArray(value)) return false;
  const v = value as Record<string, unknown>;
  return 'data' in v && ('code' in v || 'success' in v);
}

/**
 * 목록을 꺼낸다. 무엇이 와도 배열이다.
 *
 * - 배열 → 그대로
 * - 봉투 → `result` (배열이 아니면 한 건짜리 배열로)
 * - `null` · `undefined` → `[]`
 * - 그 밖의 값 → 한 건짜리 배열
 */
export function unwrapList<T>(response: unknown): T[] {
  if (Array.isArray(response)) return response as T[];

  if (isResultEnvelope(response)) {
    const rows = response.result;
    if (Array.isArray(rows)) return rows as T[];
    return rows === null || rows === undefined ? [] : [rows as T];
  }

  if (isFullResponse(response)) return unwrapList<T>(response.data);

  return response === null || response === undefined ? [] : [response as T];
}

/**
 * 객체 하나를 꺼낸다. 없으면 `undefined`.
 *
 * 봉투는 객체 하나도 `result: [obj]` 로 싣기 때문에 첫 칸을 꺼낸다.
 */
export function unwrapOne<T>(response: unknown): T | undefined {
  if (Array.isArray(response)) return response[0] as T | undefined;

  if (isResultEnvelope(response)) {
    const rows = response.result;
    if (Array.isArray(rows)) return rows[0] as T | undefined;
    return (rows ?? undefined) as T | undefined;
  }

  if (isFullResponse(response)) return unwrapOne<T>(response.data);

  return (response ?? undefined) as T | undefined;
}

/**
 * 목록과 총건수를 함께 꺼낸다. 서버 쪽 페이징을 하는 화면이 쓴다.
 *
 * `page.total` 이 없으면 받은 건수를 총건수로 본다.
 */
export function unwrapPage<T>(response: unknown): { items: T[]; total: number } {
  const items = unwrapList<T>(response);

  const raw = isFullResponse(response)
    ? (response.data as ResultEnvelope | undefined)
    : (response as ResultEnvelope | undefined);
  const total = Number(raw?.page?.total);

  return { items, total: Number.isFinite(total) ? total : items.length };
}
