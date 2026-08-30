import { requestClient } from '#/api/request';

/**
 * 생일 API (AuthServer).
 *
 * 생일 정본은 포털 계정(scom.accounts: birth_date · birth_date_is_lunar ·
 * birthday_celebrated)이고 API 는 AuthServer 다 (A안).
 * 입력·수정은 [계정 관리] 화면에서 한다 — 여기에는 조회와 축하 메시지만 있다.
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
    /** 포털 계정 ID (scom.accounts PK) */
    id: string;
    /** 로그인 ID */
    subjectId: string;
    name: string;
    birthDate: string;
    /** 올해 실제 발생일 (음력이면 양력 환산) */
    occurrenceDate: string;
    isLunar: boolean;
    isCelebrated: boolean;
    companyId?: null | string;
    companyName?: null | string;
    departmentId?: null | string;
    departmentName?: null | string;
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
      isLunar: boolean;
      originalBirthDate: string;
      type: string;
      userId: string;
    };
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
export async function getBirthdayCalendar(
  start: string,
  end: string,
  companyId?: string,
  departmentId?: string,
) {
  return toList<LifeBirthdayApi.CalendarEvent>(
    await requestClient.get<any>('/auth/birthday/calendar', {
      params: { start, end, companyId, departmentId },
    }),
  );
}

/** 월별 생일자 수 (12개) */
export async function getBirthdayStats(companyId?: string, departmentId?: string) {
  return toList<LifeBirthdayApi.MonthStat>(
    await requestClient.get<any>('/auth/birthday/stats', {
      params: { companyId, departmentId },
    }),
  );
}

/** 특정 월 생일자 */
export async function getBirthdayList(
  month: number,
  companyId?: string,
  departmentId?: string,
) {
  return toList<LifeBirthdayApi.Person>(
    await requestClient.get<any>('/auth/birthday/list', {
      params: { month, companyId, departmentId },
    }),
  );
}

/** 이번 달 생일자 */
export async function getCurrentBirthdays(companyId?: string, departmentId?: string) {
  return toList<LifeBirthdayApi.Person>(
    await requestClient.get<any>('/auth/birthday/current', {
      params: { companyId, departmentId },
    }),
  );
}

/** 오늘 생일자 (메시지 수 포함) */
export async function getTodayBirthdays(companyId?: string, departmentId?: string) {
  return toList<LifeBirthdayApi.Person>(
    await requestClient.get<any>('/auth/birthday/today', {
      params: { companyId, departmentId },
    }),
  );
}

/** 축하 메시지 보내기 */
export async function sendBirthdayMessage(recipientId: string, content: string) {
  return requestClient.post('/auth/birthday/message', { recipientId, content });
}

/** 오늘 생일자들이 올해 받은 메시지 */
export async function getTodayMessages() {
  return toList<LifeBirthdayApi.Message>(
    await requestClient.get<any>('/auth/birthday/today/messages'),
  );
}

/** 내가 받은 메시지 */
export async function getMyMessages() {
  return toList<LifeBirthdayApi.Message>(await requestClient.get<any>('/auth/birthday/message'));
}

/** 내가 보낸 메시지 */
export async function getSentMessages() {
  return toList<LifeBirthdayApi.Message>(
    await requestClient.get<any>('/auth/birthday/message/sent'),
  );
}
