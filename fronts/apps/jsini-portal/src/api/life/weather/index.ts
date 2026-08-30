import { requestClient } from '#/api/request';

/**
 * 생활과환경 — 기상 API (LifeEnvServer).
 *
 * 게이트웨이 경로는 /api/ghub 이고 서비스 안에서 /weather 아래에 산다.
 * LifeEnvServer 는 공통 봉투(ApiResponse)를 쓴다 — 서버가 단건도 목록도
 * `{ data: { result: [...], page } }` 로 감싸므로 여기서 `result` 를 벗긴다.
 * (`requestListClient` 의 점 경로 `data.result` 는 동작하지 않는다 —
 *  menu-favorite.ts 의 주석 참고. 그래서 `requestClient` + 헬퍼를 쓴다.)
 */
export namespace LifeWeatherApi {
  /** 관측 지역 */
  export interface Location {
    id: number;
    name: string;
    nx: number;
    ny: number;
    region3?: null | string;
    description?: null | string;
    /** 중기예보(육상) 구역 코드 */
    midTermLandCode?: null | string;
    /** 중기예보(기온) 구역 코드 */
    midTermTempCode?: null | string;
    /** 특보구역 코드 */
    warningAreaCode?: null | string;
    isActive: boolean;
    sortOrder: number;
  }

  /** 실황 (수집 이력) */
  export interface Info {
    id: number;
    location: string;
    weatherLocationId?: null | number;
    observationTime: string;
    temperatureC: number;
    condition: string;
    humidity?: null | number;
    windSpeed?: null | number;
    windDirection?: null | number;
    rainfall?: null | number;
    snowfall?: null | number;
    pty?: null | number;
    sensibleTemp?: null | number;
    yesterdayTemperature?: null | number;
  }

  /** 판정 기준 */
  export interface Standard {
    id: number;
    category: string;
    name: string;
    conditionText: string;
    thresholdValue?: null | number;
    operator?: null | string;
    thresholdValue2?: null | number;
    unit?: null | string;
    workStatus?: null | string;
    sortOrder: number;
    duration?: null | number;
    prevDayDiff?: null | number;
    avgYearDiff?: null | number;
    notificationInterval?: null | number;
    useSensibleTemp: boolean;
  }

  /** 기준별 대응 요령 */
  export interface Response {
    id: number;
    weatherStandardId: number;
    weatherStandard?: null | Standard;
    actionContent: string;
    description?: null | string;
    sortOrder: number;
  }

  /** 기준 초과 이벤트 */
  export interface EventRecord {
    id: number;
    weatherInfoId: number;
    weatherInfo?: null | Info;
    weatherStandardId: number;
    weatherStandard?: null | Standard;
    eventTime: string;
    measuredValue: number;
    isNotified: boolean;
  }

  /** 특보 */
  export interface Warning {
    id: number;
    stnId: number;
    tmFc: string;
    tmSeq: number;
    title: string;
    content: string;
    other?: null | string;
    warningNum?: null | string;
    announcementTime?: null | string;
    command?: null | string;
    collectedAt: string;
    matchedLocations?: Location[] | null;
    sentences?: any[] | null;
  }

  /** 예보 타임라인 (과거 실측 + 미래 예보 병합) */
  export interface TimelinePoint {
    date: string;
    time: string;
    temp?: null | number;
    pop?: null | number;
    rain?: null | number;
    sky?: null | string;
    pty?: null | string;
    windSpeed?: null | number;
    windDir?: null | number;
    reh?: null | number;
    isPast?: boolean;
    [key: string]: any;
  }

  /** 격자좌표 검색 결과 */
  export interface GridCoordinate {
    administrativeCode: string;
    region1?: null | string;
    region2?: null | string;
    region3?: null | string;
    nx: number;
    ny: number;
  }
}

/** 봉투에서 목록을 꺼낸다 — 어느 깊이로 감겨 와도 배열을 돌려준다 */
function toList<T = any>(res: any): T[] {
  if (Array.isArray(res)) return res;
  if (Array.isArray(res?.result)) return res.result;
  if (Array.isArray(res?.data?.result)) return res.data.result;
  return [];
}

/** 봉투가 단건도 { result: [obj] } 로 감싸므로 첫 원소를 꺼낸다 */
function toOne<T = any>(res: any): T | undefined {
  return toList<T>(res)[0];
}

// ── 실황 ─────────────────────────────────────────────────────

/** 전 지역 최신 실황 (20분 지나면 서버가 기상청을 다시 부른다) */
export async function getLatestWeather() {
  return toList<LifeWeatherApi.Info>(await requestClient.get<any>('/life/weather'));
}

/** 한 지역 실황 */
export async function getCurrentWeather(locationId: number) {
  return toOne<LifeWeatherApi.Info>(
    await requestClient.get<any>(`/life/weather/current/${locationId}`),
  );
}

/** 실측 이력 (지역명 · 일수) */
export async function getWeatherHistory(location?: string, days = 1) {
  return toList<LifeWeatherApi.Info>(
    await requestClient.get<any>('/life/weather/history', { params: { location, days } }),
  );
}

/** 특정 시각(KST)의 일자별 기온 */
export async function getHourlyHistory(locationId: number, hour: number, days = 7) {
  return toList<{ date: string; temp: number }>(
    await requestClient.get<any>('/life/weather/history/hourly', {
      params: { locationId, hour, days },
    }),
  );
}

// ── 예보 ─────────────────────────────────────────────────────

/** 과거 -10h ~ 미래 +10h 타임라인 (실측 + 단기 + 초단기 병합) */
export async function getForecast(locationId: number) {
  return toList<LifeWeatherApi.TimelinePoint>(
    await requestClient.get<any>(`/life/weather/forecast/${locationId}`),
  );
}

/** 주간(오늘~10일) 예보 — 단기 + 중기 병합 */
export async function getMidTermForecast(locationId: number) {
  return toList<any>(await requestClient.get<any>(`/life/weather/mid-term/${locationId}`));
}

// ── 특보 ─────────────────────────────────────────────────────

/** 특보 목록. all=true 면 최근 7일 전체 + 매칭 지역 · 문장 포함 */
export async function getWarnings(all?: boolean) {
  return toList<LifeWeatherApi.Warning>(
    await requestClient.get<any>('/life/weather/warnings', {
      params: all ? { all: true } : undefined,
    }),
  );
}

export async function getWarning(id: number) {
  return toOne<LifeWeatherApi.Warning>(
    await requestClient.get<any>(`/life/weather/warnings/${id}`),
  );
}

/** 특보 + 통보문 + 현황 묶음 */
export async function getWarningFullDetails(id: number) {
  return toOne<any>(await requestClient.get<any>(`/life/weather/warnings/${id}/full`));
}

/** 통보문 단건 (stnId · tmFc · tmSeq) */
export async function getWarningMsg(stnId: number, tmFc: string, tmSeq: number) {
  return toOne<any>(
    await requestClient.get<any>('/life/weather/warnings/msg', {
      params: { stnId, tmFc, tmSeq },
    }),
  );
}

/** 특보에 매칭된 관리 지역들 */
export async function getMatchedLocations(warningId: number) {
  return toList<LifeWeatherApi.Location>(
    await requestClient.get<any>(`/life/weather/warnings/${warningId}/locations`),
  );
}

/** 오늘 통보문 중 관리지역이 걸린 문장 (제목별 최신 1건) */
export async function getWarnings4Location() {
  return toList<any>(await requestClient.get<any>('/life/weather/warnings4location'));
}

/** 오늘 통보문 중 관리지역이 걸린 문장 (제목별 최초 · 최종) */
export async function getWarnings4LocationRange() {
  return toList<any>(await requestClient.get<any>('/life/weather/warnings4location-range'));
}

/** 특보구역 마스터 (기상청 구역 트리) */
export async function getWarningZones() {
  return toList<any>(await requestClient.get<any>('/life/weather/warning-zones'));
}

/** 지역별 특보 이력 (특보 ↔ 지역 매칭 기록) */
export async function getLocationWarningHistory(locationId?: number) {
  return toOne<{ history: any[]; locations: LifeWeatherApi.Location[] }>(
    await requestClient.get<any>('/life/weather/locations/warning-history', {
      params: locationId ? { locationId } : undefined,
    }),
  );
}

// ── 지역 관리 ────────────────────────────────────────────────

export async function getLocations() {
  return toList<LifeWeatherApi.Location>(
    await requestClient.get<any>('/life/weather/locations'),
  );
}

export async function createLocation(data: Partial<LifeWeatherApi.Location>) {
  return requestClient.post('/life/weather/locations', data);
}

export async function updateLocation(id: number, data: Partial<LifeWeatherApi.Location>) {
  return requestClient.put(`/life/weather/locations/${id}`, data);
}

export async function deleteLocation(id: number) {
  return requestClient.delete(`/life/weather/locations/${id}`);
}

export async function reorderLocations(items: { id: number; sortOrder: number }[]) {
  return requestClient.put('/life/weather/locations/reorder', items);
}

/** 격자좌표 검색 (지역명으로 nx/ny 찾기) */
export async function searchGrid(query: string) {
  return toList<LifeWeatherApi.GridCoordinate>(
    await requestClient.get<any>('/life/weather/locations/search-grid', {
      params: { query },
    }),
  );
}

// ── 기준 관리 ────────────────────────────────────────────────

export async function getStandards() {
  return toList<LifeWeatherApi.Standard>(
    await requestClient.get<any>('/life/weather/standards'),
  );
}

export async function createStandard(data: Partial<LifeWeatherApi.Standard>) {
  return requestClient.post('/life/weather/standards', data);
}

export async function updateStandard(id: number, data: Partial<LifeWeatherApi.Standard>) {
  return requestClient.put(`/life/weather/standards/${id}`, data);
}

export async function deleteStandard(id: number) {
  return requestClient.delete(`/life/weather/standards/${id}`);
}

// ── 대응 요령 ────────────────────────────────────────────────

export async function getResponses() {
  return toList<LifeWeatherApi.Response>(
    await requestClient.get<any>('/life/weather/responses'),
  );
}

export async function getResponsesByStandard(standardId: number) {
  return toList<LifeWeatherApi.Response>(
    await requestClient.get<any>(`/life/weather/responses/by-standard/${standardId}`),
  );
}

export async function createResponse(data: Partial<LifeWeatherApi.Response>) {
  return requestClient.post('/life/weather/responses', data);
}

export async function updateResponse(id: number, data: Partial<LifeWeatherApi.Response>) {
  return requestClient.put(`/life/weather/responses/${id}`, data);
}

export async function deleteResponse(id: number) {
  return requestClient.delete(`/life/weather/responses/${id}`);
}

export async function reorderResponses(items: { id: number; sortOrder: number }[]) {
  return requestClient.put('/life/weather/responses/reorder', items);
}

// ── 이벤트 기록 ──────────────────────────────────────────────

/** 기준 초과 이벤트 목록 (페이징) */
export async function getEvents(params: {
  endDate?: string;
  locationId?: number;
  page?: number;
  pageSize?: number;
  startDate?: string;
}) {
  return toOne<{
    items: LifeWeatherApi.EventRecord[];
    page: number;
    pageSize: number;
    totalCount: number;
  }>(await requestClient.get<any>('/life/weather/events', { params }));
}

/** 지금 발효 중인 이벤트 (최근 20분) */
export async function getCurrentEvents(locationId: number) {
  return toList<LifeWeatherApi.EventRecord>(
    await requestClient.get<any>('/life/weather/events/current', { params: { locationId } }),
  );
}

export async function deleteEvent(id: number) {
  return requestClient.delete(`/life/weather/events/${id}`);
}
