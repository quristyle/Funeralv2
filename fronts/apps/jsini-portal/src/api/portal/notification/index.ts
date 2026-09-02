import { unwrapOne } from '#/api/envelope';
import { requestClient } from '#/api/request';

/**
 * 내 알림 설정 API — NotificationServer.
 *
 * 게이트웨이 경로는 `/api/notification` 이고 서비스 안에서 `/notifications` 아래에
 * 산다. 그래서 여기 경로가 `/notification/notifications/...` 로 두 겹이다
 * (`baseURL` 이 이미 `/api` 라 `/api` 는 적지 않는다).
 *
 * **예전에는 헬프데스크(`/api/helpdesk/push/*`)를 봤다.** `/system/push/setting` 이
 * 헬프데스크의 구독 시험 화면을 옮겨 온 것이라 데이터도 거기 있었다 —
 * 포털 계정으로 로그인한 사람이 헬프데스크의 신원 체계(int Admin.Id) 위에
 * 구독을 만들려 하니 맞을 수가 없었다. 관리 주체를 포털로 옮겼다.
 * 배경: docs/analysis/29-notification-server.md 8절.
 */
export namespace NotificationApi {
  /** 알림 수신 스위치. 서버에 행이 없으면 `saved` 가 거짓이고 값은 기본값이다. */
  export interface Preference {
    /** 브라우저 푸시를 받을지 */
    pushEnabled: boolean;
    /** 이메일 알림을 받을지 (역할로 보내는 메일에만 걸린다) */
    emailEnabled: boolean;
    /** 날씨(기상 특보·임계치) 알림을 받을지 */
    weatherEnabled: boolean;
    /** 저장한 적이 있나. 거짓이면 위 값은 기본값이다 */
    saved: boolean;
    updatedAt?: null | string;
  }

  /** 구독된 기기 한 대 */
  export interface Device {
    /** 푸시 서비스 주소. 이것으로 "지금 이 브라우저" 를 알아본다 */
    endpoint: string;
    source?: null | string;
    userAgent?: null | string;
    lastSentAt?: null | string;
    createdAt: string;
    /** 연달아 실패한 횟수. 0 이 아니면 그 기기는 못 받고 있을 수 있다 */
    failureCount: number;
  }

  /** 화면이 한 번에 받는 상태 */
  export interface MyState {
    ownerType: string;
    ownerKey: string;
    preference: Preference;
    /** 서버가 푸시를 보낼 수 있는 상태인가 (VAPID 설정 여부) */
    pushAvailable: boolean;
    /** 브라우저가 구독을 만들 때 쓰는 공개 키 */
    vapidPublicKey?: null | string;
    devices: Device[];
  }

  /** 브라우저의 `PushSubscription` 을 서버가 받는 모양으로 펼친 것 */
  export interface SubscribePayload {
    endpoint: string;
    p256dh: string;
    auth: string;
    /** 어느 시스템에서 구독했나 (참고용) */
    source?: string;
  }

  /** 발송 결과. `sent` 가 0 이면 `message` 에 이유가 있다 */
  export interface SendResult {
    sent: number;
    failed: number;
    removed: number;
    ownersWithoutSubscription: number;
    /** 본인이 푸시를 꺼 두어 제외된 대상 수 */
    optedOut: number;
    message?: null | string;
  }
}

const BASE = '/notification/notifications';

/**
 * 봉투에서 단건을 꺼낸다.
 *
 * 공통 봉투(`ApiResponse`)는 **단건도 `{ result: [obj], page }` 로 감싼다**
 * (`BuildSerializedData`). `requestClient` 가 `data` 까지만 벗기므로 여기서 한 겹 더
 * 벗긴다 — `requestListClient` 의 점 경로(`data.result`)는 동작하지 않는다
 * (life/weather · menu-favorite 의 주석과 같은 이유).
 */
function toOne<T>(res: any): T {
  return unwrapOne<T>(res) as T;
}

/** 내 알림 설정 · 공개 키 · 기기 목록을 한 번에 받는다. */
export async function getMyNotificationState() {
  return toOne<NotificationApi.MyState>(
    await requestClient.get<any>(`${BASE}/preferences/me`),
  );
}

/**
 * 스위치를 바꾼다. **준 항목만 바뀐다** — 하나만 눌러도 나머지가 되돌아가지 않는다.
 */
export async function updateMyNotificationPreference(patch: {
  emailEnabled?: boolean;
  pushEnabled?: boolean;
  weatherEnabled?: boolean;
}) {
  return toOne<NotificationApi.Preference>(
    await requestClient.put<any>(`${BASE}/preferences/me`, patch),
  );
}

/** 이 브라우저의 구독을 등록한다. 같은 기기가 다시 부르면 갱신된다. */
export async function subscribeMyDevice(
  payload: NotificationApi.SubscribePayload,
) {
  return requestClient.post<boolean>(`${BASE}/subscriptions`, payload);
}

/**
 * 구독을 해제한다. 자기 것만 지울 수 있다.
 *
 * 서버에 이미 없으면 404 다. 화면에서는 "없으니 지운 셈" 으로 볼 자리가 있어
 * (브라우저에는 남고 서버에서는 지워진 경우) 오류 알림을 띄우지 않는다.
 */
export async function unsubscribeMyDevice(endpoint: string) {
  return requestClient.delete<boolean>(`${BASE}/subscriptions`, {
    params: { endpoint },
    skipErrorMessage: true,
  } as any);
}

/**
 * 나에게 시험 알림을 보낸다.
 *
 * 대상을 서버가 정한다 — 화면이 주인 키를 적어 보내지 않는다.
 * 푸시를 꺼 두었으면 보내지 않고 그 이유가 `message` 로 돌아온다
 * (시험이 실제 발송과 같은 길을 지나야 뜻이 있다).
 */
export async function sendTestPushToMe(message?: {
  body?: string;
  title?: string;
  url?: string;
}) {
  return toOne<NotificationApi.SendResult>(
    await requestClient.post<any>(`${BASE}/push/test`, message ?? {}),
  );
}
