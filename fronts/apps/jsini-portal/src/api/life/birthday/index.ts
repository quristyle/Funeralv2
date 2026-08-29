import { requestClient } from '#/api/request';

/**
 * 생활과환경 — 생일 API (GhubServer).
 *
 * 생일 대상자는 ghub.birthday_profiles 다 — 포털 계정(scom)이 아니라
 * GHUB 에서 이관한 별도 명단이다 (docs/analysis/38-ghub-migration.md 3절).
 *
 * 서버가 단건도 목록도 `{ data: { result: [...], page } }` 로 감싸므로
 * 여기서 `result` 를 벗긴다 (`requestListClient` 의 점 경로는 동작하지 않는다 —
 * menu-favorite.ts 주석 참고).
 */
export namespace LifeBirthdayApi {
  /** 월별 통계 */
  export interface MonthStat {
    month: number;
    total: number;
    solar: number;
    lunar: number;
  }

  /** 생일자 (목록·이번달·오늘) */
  export interface Person {
    /** birthday_profiles PK — 수정 · 초기화에 쓴다 */
    id: number;
    /** 로그인 ID */
    subjectId: string;
    name: string;
    birthDate: string;
    /** 올해 실제 발생일 (음력이면 양력 환산) */
    occurrenceDate: string;
    isLunar: boolean;
    isCelebrated: boolean;
    companyCode?: null | string;
    department?: null | string;
    /** 오늘 생일자에만 */
    thumbnailUrl?: null | string;
    /** 오늘 생일자에만 — 올해 받은 메시지 수 */
    messageCount?: number;
  }

  /** FullCalendar 형 이벤트 */
  export interface CalendarEvent {
    id: string;
    title: string;
    start: string;
    allDay: boolean;
    backgroundColor: string;
    borderColor: string;
    extendedProps: {
      dbId: number;
      isLunar: boolean;
      originalBirthDate: string;
      type: string;
      userId: string;
    };
  }

  /** 생일 등록 · 수정 요청 */
  export interface Entry {
    subjectId: string;
    name: string;
    birthDate: string;
    isLunar: boolean;
    isCelebrated: boolean;
  }

  /** 축하 메시지 */
  export interface Message {
    id: number;
    content: string;
    createdAt: string;
    senderName?: string;
    senderDepartment?: null | string;
    recipientName?: string;
    recipientDepartment?: null | string;
    recipientId?: string;
  }
}

/** 봉투에서 목록을 꺼낸다 */
function toList<T = any>(res: any): T[] {
  if (Array.isArray(res)) return res;
  if (Array.isArray(res?.result)) return res.result;
  if (Array.isArray(res?.data?.result)) return res.data.result;
  return [];
}

/** 달력 이벤트 (start ~ end, YYYY-MM-DD) */
export async function getBirthdayCalendar(start: string, end: string, companyCode?: string) {
  return toList<LifeBirthdayApi.CalendarEvent>(
    await requestClient.get<any>('/ghub/birthday/calendar', {
      params: { start, end, companyCode },
    }),
  );
}

/** 월별 생일자 수 (12개) */
export async function getBirthdayStats(companyCode?: string) {
  return toList<LifeBirthdayApi.MonthStat>(
    await requestClient.get<any>('/ghub/birthday/stats', {
      params: companyCode ? { companyCode } : undefined,
    }),
  );
}

/** 특정 월 생일자 */
export async function getBirthdayList(month: number, companyCode?: string) {
  return toList<LifeBirthdayApi.Person>(
    await requestClient.get<any>('/ghub/birthday/list', {
      params: { month, companyCode },
    }),
  );
}

/** 이번 달 생일자 */
export async function getCurrentBirthdays(companyCode?: string) {
  return toList<LifeBirthdayApi.Person>(
    await requestClient.get<any>('/ghub/birthday/current', {
      params: companyCode ? { companyCode } : undefined,
    }),
  );
}

/** 오늘 생일자 (썸네일 · 메시지 수 포함) */
export async function getTodayBirthdays(companyCode?: string) {
  return toList<LifeBirthdayApi.Person>(
    await requestClient.get<any>('/ghub/birthday/today', {
      params: companyCode ? { companyCode } : undefined,
    }),
  );
}

/** 생일 등록 (있으면 갱신, 없으면 새 명단 행) */
export async function upsertBirthday(data: LifeBirthdayApi.Entry) {
  return requestClient.post('/ghub/birthday', data);
}

/** 생일 정보 수정 (id = birthday_profiles PK) */
export async function updateBirthday(id: number, data: LifeBirthdayApi.Entry) {
  return requestClient.put(`/ghub/birthday/${id}`, data);
}

/** 생일 정보 초기화 (행은 남는다) */
export async function resetBirthday(id: number) {
  return requestClient.delete(`/ghub/birthday/${id}`);
}

/** 축하 메시지 보내기 */
export async function sendBirthdayMessage(recipientId: string, content: string) {
  return requestClient.post('/ghub/birthday/message', { recipientId, content });
}

/** 오늘 생일자들이 올해 받은 메시지 */
export async function getTodayMessages() {
  return toList<LifeBirthdayApi.Message>(
    await requestClient.get<any>('/ghub/birthday/today/messages'),
  );
}

/** 내가 받은 메시지 */
export async function getMyMessages() {
  return toList<LifeBirthdayApi.Message>(await requestClient.get<any>('/ghub/birthday/message'));
}

/** 내가 보낸 메시지 */
export async function getSentMessages() {
  return toList<LifeBirthdayApi.Message>(
    await requestClient.get<any>('/ghub/birthday/message/sent'),
  );
}
