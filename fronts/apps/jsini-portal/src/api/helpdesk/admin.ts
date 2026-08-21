/**
 * 대시보드 통계 · 헬프데스크 자체 메뉴/역할 관리 · 사용자 설정 · 계정 연결 API.
 */
import type {
  AppRole,
  AuthUserLink,
  HelpdeskIdentity,
  HelpdeskMenu,
  HelpdeskUserType,
  RoleMenuPermission,
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
// 푸시 알림 (구독 · 알림함)
// ============================================================

/** 내 알림 이력 */
export async function getMyNotifications(params?: Record<string, any>) {
  return helpdeskClient.get<any>('/push/notifications', { params });
}

/** 알림을 읽음 처리한다. */
export async function markNotificationRead(id: number) {
  return helpdeskClient.post(`/push/notifications/${id}/read`);
}

/** 현재 브라우저 구독이 서버에 등록되어 있는지 확인한다. */
export async function isPushSubscribed(endpoint: string) {
  return helpdeskClient.get<any>('/push/is-subscribed', {
    params: { endpoint },
  });
}

/** 웹푸시 구독을 등록한다. */
export async function subscribePush(subscription: unknown) {
  return helpdeskClient.post('/push/subscribe', subscription);
}

/** 웹푸시 구독을 해제한다. */
export async function unsubscribePush(endpoint: string) {
  return helpdeskClient.post('/push/unsubscribe', { endpoint });
}

/** 테스트 알림을 보낸다. */
export async function sendTestPush(payload: Record<string, any>) {
  return helpdeskClient.post('/push/notify', payload);
}

// ============================================================
// 헬프데스크 자체 메뉴 (JinReception 화면 권한 관리용)
// ============================================================

/** 내가 볼 수 있는 메뉴 트리 */
export async function getHelpdeskMenus() {
  return helpdeskClient.get<HelpdeskMenu[]>('/menus');
}

/** 관리용 전체 메뉴 트리 */
export async function getManageMenus() {
  return helpdeskClient.get<HelpdeskMenu[]>('/menus/manage');
}

/** 메뉴 생성 */
export async function createHelpdeskMenu(data: Partial<HelpdeskMenu>) {
  return helpdeskClient.post<HelpdeskMenu>('/menus', data);
}

/** 메뉴 수정 */
export async function updateHelpdeskMenu(
  id: number,
  data: Partial<HelpdeskMenu>,
) {
  return helpdeskClient.put<HelpdeskMenu>(`/menus/${id}`, data);
}

/** 메뉴 삭제 */
export async function deleteHelpdeskMenu(id: number) {
  return helpdeskClient.delete(`/menus/${id}`);
}

/** 메뉴 이동 (드래그 앤 드롭) */
export async function moveHelpdeskMenu(data: Record<string, any>) {
  return helpdeskClient.post('/menus/move', data);
}

// ============================================================
// 역할 · 권한
// ============================================================

/** 역할 목록 */
export async function getRoles() {
  return helpdeskClient.get<AppRole[]>('/roles');
}

/** 역할 생성 */
export async function createRole(data: Partial<AppRole>) {
  return helpdeskClient.post<AppRole>('/roles', data);
}

/** 역할 수정 */
export async function updateRole(id: number, data: Partial<AppRole>) {
  return helpdeskClient.put<AppRole>(`/roles/${id}`, data);
}

/** 역할 삭제 */
export async function deleteRole(id: number) {
  return helpdeskClient.delete(`/roles/${id}`);
}

/** 역할에 속한 사용자 목록 */
export async function getRoleUsers(roleId: number) {
  return helpdeskClient.get<any[]>(`/roles/${roleId}/users`);
}

/** 역할에 사용자 추가 */
export async function addUserToRole(
  roleId: number,
  userType: HelpdeskUserType,
  userId: number,
) {
  return helpdeskClient.post('/roles/users', { roleId, userId, userType });
}

/** 역할에서 사용자 제거 */
export async function removeUserFromRole(
  roleId: number,
  userType: HelpdeskUserType,
  userId: number,
) {
  return helpdeskClient.delete(`/roles/${roleId}/users/${userType}/${userId}`);
}

/** 역할이 배정되지 않은 사용자 목록 */
export async function getUnassignedUsers() {
  return helpdeskClient.get<any[]>('/common/unassigned-users');
}

/** 역할별 메뉴 권한 */
export async function getRolePermissions(roleId: number) {
  return helpdeskClient.get<RoleMenuPermission[]>(
    `/roles/${roleId}/permissions`,
  );
}

/** 권한 단건 저장 */
export async function saveRolePermission(data: RoleMenuPermission) {
  return helpdeskClient.post('/roles/permissions', data);
}

/** 권한 일괄 저장 */
export async function saveRolePermissionsBatch(data: RoleMenuPermission[]) {
  return helpdeskClient.post('/roles/permissions/batch', data);
}

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
