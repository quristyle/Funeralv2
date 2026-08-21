import { requestClient } from '#/api/request';

export namespace StatApi {
  export interface BillingStat {
    id: string;
    companyId: string;
    companyName: string;
    billingMonth: string; // YYYY-MM
    roomUsageCount: number;
    totalAmount: number;
    paymentStatus: 'PAID' | 'UNPAID' | 'OVERDUE';
    paymentDate?: string;
  }

  export interface RoomUsageStat {
    id: string;
    roomId: string;
    roomName: string;
    deceasedName: string;
    useStartDate: string;
    useEndDate: string;
    durationHours: number;
    billingAmount: number;
  }
}

/**
 * 과금 내역 목록 조회
 */
export async function getBillingStats(params: { startDate?: string; endDate?: string; companyId?: string }) {
  return requestClient.get<StatApi.BillingStat[]>('/stat/billing/list', { params });
}

/**
 * 빈소 사용 내역 목록 조회
 */
export async function getRoomUsageStats(params: { startDate?: string; endDate?: string; roomId?: string }) {
  return requestClient.get<StatApi.RoomUsageStat[]>('/stat/room-usage/list', { params });
}
