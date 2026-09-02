/**
 * 대시보드 통계 · 사용자 설정 · 계정 연결 API.
 */
import type {
  AuthUserLink,
  HelpdeskIdentity,
  HelpdeskUserType,
  UserPropertyMap,
} from './types';

import { helpdeskClient, helpdeskFetchPageByGet } from './request';

// ============================================================
// 대시보드 통계
// ============================================================

/** 회사별 요청 통계 */
export async function getCompanyStats() {
  return helpdeskClient.get<any>('/dashboard/company-stats');
}

/** 내 회사 통계 (고객용) */
export async function getMyCompanyStats() {
  return helpdeskClient.get<any>('/dashboard/my-company-stats');
}

/** 내 월별 통계 */
export async function getMyMonthlyStats() {
  return helpdeskClient.get<any>('/dashboard/my-monthly-stats');
}

/** 담당자 처리 통계 */
export async function getAdminStats() {
  return helpdeskClient.get<any>('/dashboard/admin-stats');
}

/** 전체 담당자 통계 */
export async function getAllAdminStats() {
  return helpdeskClient.get<any>('/dashboard/all-admin-stats');
}

/** 담당자 기여도 통계 */
export async function getAdminContributionStats(year?: number, month?: number) {
  return helpdeskClient.get<any>('/dashboard/admin-contribution-stats', {
    params: { month, year },
  });
}

/** 담당자 기여도 추이 */
export async function getAdminContributionTrend() {
  return helpdeskClient.get<any>('/dashboard/admin-contribution-trend');
}

// ============================================================
// 푸시 통계 — 서버에서는 dashboard 그룹에 들어 있다
// ============================================================

/** 푸시 발송 통계 */
export async function getPushStats(days = 7) {
  return helpdeskClient.get<any>('/dashboard/push-stats', { params: { days } });
}

/** 푸시 성공률 추이 */
export async function getPushSuccessTrend(interval = 'daily', days = 30) {
  return helpdeskClient.get<any>('/dashboard/push-success-trend', {
    params: { days, interval },
  });
}

/** 푸시 실패 사유 상위 */
export async function getPushFailureReasons(days = 7, topN = 5) {
  return helpdeskClient.get<any>('/dashboard/push-failure-reasons', {
    params: { days, topN },
  });
}

/** 푸시 참여 통계 */
export async function getPushEngagementStats(days = 7) {
  return helpdeskClient.get<any>('/dashboard/push-engagement-stats', {
    params: { days },
  });
}

/** 성과 상위 푸시 메시지 */
export async function getTopPerformingMessages(topN = 10) {
  return helpdeskClient.get<any>('/dashboard/top-performing-messages', {
    params: { topN },
  });
}

/** 사용자별 푸시 반응 통계 */
export async function getUserEngagementStats(topN = 20) {
  return helpdeskClient.get<any>('/dashboard/user-engagement-stats', {
    params: { topN },
  });
}

/** 푸시 발송 이력 (페이징) */
export async function getPushLogs(params?: Record<string, any>) {
  return helpdeskFetchPageByGet<any>('/dashboard/push-logs', params);
}

/** 발송 실패 사유 목록 (필터용) */
export async function getDistinctFailureReasons() {
  return helpdeskClient.get<string[]>('/dashboard/distinct-failure-reasons');
}

// ============================================================
// 푸시 알림 (알림함)
// ============================================================
//
// **구독 등록·해제·시험 발송은 여기 없다.** 포털 계정의 구독은 NotificationServer 가
// 관장한다 — `#/api/portal/notification` 을 쓴다. 헬프데스크의 구독 표는 주인을
// `(int Admin.Id, UserType)` 로 잡아 포털 로그인 아이디로는 맞출 수가 없었다.
// 남은 둘은 헬프데스크 테이블을 읽는 화면 기능이라 여기 남는다
// (29-notification-server.md "일부러 옮기지 않은 것").

/** 내 알림 이력 */
export async function getMyNotifications(params?: Record<string, any>) {
  return helpdeskClient.get<any>('/push/notifications', { params });
}

/** 알림을 읽음 처리한다. */
export async function markNotificationRead(id: number) {
  return helpdeskClient.post(`/push/notifications/${id}/read`);
}

// ============================================================
// 헬프데스크 자체 메뉴 · 역할 · 권한
// ============================================================
//
// 메뉴·역할·화면 접근 권한은 JSini 관리 포털이 일원 관리한다
// (scom.system_menus / scom.roles / scom.role_menus).
// 헬프데스크도 자체 테이블(jsini.menu, approle, rolemenupermission)을 갖고 있지만
// 포털로 이식하면서 화면과 API 를 모두 걷어냈다.
//
// 헬프데스크가 관리하던 권한 항목(조회/등록/수정/삭제, 확장 1~8)은
// 포털 role_menus 의 can_view·can_search·can_create·can_update·can_delete·
// can_print·can_excel·can_cust1~8 에 모두 대응된다.
//
// 서버(HelpDeskServer)에는 /api/menus, /api/roles 엔드포인트가 아직 남아 있다.
// 아직 살아 있는 JinReception 이 쓰고 있을 수 있어 서버 쪽은 건드리지 않았다.
// JinReception 을 내린 뒤 정리 대상이다.

// ============================================================
// 사용자 개인 설정
// ============================================================

/**
 * 내 설정 조회.
 * 서버는 배열이 아니라 키-값 객체로 주고받는다 (예: `{ receiveEmail: 'true' }`).
 */
export async function getUserProperties() {
  return helpdeskClient.get<UserPropertyMap>('/user-properties');
}

/** 내 설정 저장. 바뀐 키만 담아 보내면 된다. */
export async function saveUserProperties(data: UserPropertyMap) {
  return helpdeskClient.put('/user-properties', data);
}

/** 내 정보 */
export async function getMyInfo() {
  return helpdeskClient.get<any>('/users/info');
}

// ============================================================
// funeralv2 계정 ↔ 헬프데스크 계정 연결
// ============================================================

/** 등록된 계정 연결 목록 */
export async function getAuthLinks() {
  return helpdeskClient.get<AuthUserLink[]>('/auth-links/');
}

/** 현재 로그인 계정이 해석된 헬프데스크 신원 */
export async function getMyHelpdeskIdentity() {
  return helpdeskClient.get<HelpdeskIdentity>('/auth-links/me');
}

/** 계정 연결 등록 · 변경 */
export async function saveAuthLink(data: {
  authUserId: string;
  helpdeskUserId: number;
  userType: HelpdeskUserType;
}) {
  return helpdeskClient.post('/auth-links/', data);
}

/** 계정 연결 해제 */
export async function deleteAuthLink(id: number) {
  return helpdeskClient.delete(`/auth-links/${id}`);
}
