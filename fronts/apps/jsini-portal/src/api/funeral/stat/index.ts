import { requestClient } from '#/api/request';

/**
 * 통계 API — 과금 내역 · 빈소 사용 내역.
 *
 * 옛 시스템의 `t_goin_pay`(고인 한 명당 기본료 · 환경부담금 · 시설관리비 세 줄) 자리를
 * `smfr.deceased_facilities` 가 맡는다. 비용 행이 없는 고인에게는 백엔드가
 * 옛 기본 단가로 항목을 만들어 보여 준다 — 저장하지는 않는다.
 */
export namespace StatApi {
  /** 과금 항목 한 줄 */
  export interface BillingItem {
    id: string;
    /** 기본료 · 환경부담금 · 시설관리비 등 */
    title: string;
    unitPrice: number;
    /** 사용일수를 곱하는 항목인지 */
    applyPerDay: boolean;
    amount: number;
    remark?: string;
  }

  /** 고인 한 명의 과금 내역 */
  export interface Billing {
    deceasedId: string;
    deceasedName: string;
    roomId?: string;
    roomName?: string;
    buildingId?: string;
    buildingName?: string;
    startTime?: string;
    endTime?: string;
    useDays: number;
    items: BillingItem[];
    totalAmount: number;
    status: string;
  }

  /** 빈소 사용 내역 한 줄 */
  export interface RoomUsage {
    id: string;
    roomId: string;
    roomName: string;
    floorName?: string;
    buildingId?: string;
    buildingName?: string;
    deceasedId: string;
    deceasedName: string;
    startTime?: string;
    endTime?: string;
    useDays: number;
    billingAmount: number;
    inUse: boolean;
  }

  /** 화면 위에 얹는 요약 숫자 */
  export interface Summary {
    deceasedCount: number;
    roomUsageCount: number;
    totalUseDays: number;
    totalAmount: number;
  }
}

export async function getBillingStats(params?: {
  buildingId?: string;
  from?: string;
  to?: string;
}) {
  return requestClient.get<StatApi.Billing[]>('/funeral/stat/billing/list', {
    params,
  });
}

export async function getRoomUsageStats(params?: {
  buildingId?: string;
  roomId?: string;
  from?: string;
  to?: string;
}) {
  return requestClient.get<StatApi.RoomUsage[]>(
    '/funeral/stat/room-usage/list',
    { params },
  );
}

export async function getStatSummary(params?: {
  buildingId?: string;
  from?: string;
  to?: string;
}) {
  return requestClient.get<StatApi.Summary>('/funeral/stat/summary', {
    params,
  });
}
