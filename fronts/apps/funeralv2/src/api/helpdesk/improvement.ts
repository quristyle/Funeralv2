/**
 * 개선요청(헬프데스크 티켓) 및 댓글 API.
 */
import type {
  Attachment,
  HelpdeskSearchParams,
  ImprovementComment,
  ImprovementRequest,
  ImprovementStatus,
} from './types';

import { helpdeskClient, helpdeskFetchPage } from './request';

/** 요청 검색. 서버는 조회 조건을 본문으로 받는다. */
export async function searchRequests(params: HelpdeskSearchParams) {
  return helpdeskFetchPage<ImprovementRequest>('/requests/srch', params);
}

/**
 * 요청 단건 조회.
 * 서버의 GET /requests/{id} 는 연관 데이터를 다 붙여주지 않아, 원본 화면과 동일하게 검색 API 를 쓴다.
 */
export async function getRequest(id: number) {
  const page = await helpdeskFetchPage<ImprovementRequest>('/requests/srch', {
    id: String(id),
    remove: 'customerId',
  });
  return page.items[0];
}

/** 요청 생성 */
export async function createRequest(data: Partial<ImprovementRequest>) {
  return helpdeskClient.post<ImprovementRequest>('/requests', data);
}

/** 첨부파일과 함께 요청 생성 */
export async function createRequestWithFiles(formData: FormData) {
  return helpdeskClient.post<ImprovementRequest>('/requests', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
}

/** 요청 수정 */
export async function updateRequest(
  id: number,
  data: Partial<ImprovementRequest>,
) {
  return helpdeskClient.put<ImprovementRequest>(`/requests/${id}`, data);
}

/** 첨부파일과 함께 요청 수정 */
export async function updateRequestWithFiles(id: number, formData: FormData) {
  return helpdeskClient.put<ImprovementRequest>(`/requests/${id}`, formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
}

/** 요청 삭제 */
export async function deleteRequest(id: number) {
  return helpdeskClient.delete(`/requests/${id}`);
}

/** 요청 접수 · 상태 변경. 담당자로 지정된다. */
export async function acceptRequest(
  id: number,
  status: ImprovementStatus,
  adminId?: number,
) {
  return helpdeskClient.put(`/requests/accept/${id}`, { adminId, status });
}

/** 요청 접수 취소 (담당자·상태 초기화) */
export async function resetRequest(id: number, adminId?: number) {
  return helpdeskClient.put(`/requests/reset/${id}`, { adminId });
}

// ============================================================
// 댓글
// ============================================================

/** 특정 요청의 댓글 목록 */
export async function getRequestComments(requestId: number) {
  return helpdeskClient.get<ImprovementComment[]>(
    `/requests/${requestId}/comments`,
  );
}

/** 댓글 등록 */
export async function createComment(data: Partial<ImprovementComment>) {
  return helpdeskClient.post<ImprovementComment>('/comments', data);
}

/** 첨부파일과 함께 댓글 등록 */
export async function createCommentWithFiles(formData: FormData) {
  return helpdeskClient.post<ImprovementComment>('/comments', formData, {
    headers: { 'Content-Type': 'multipart/form-data' },
  });
}

/** 댓글 단건 조회 */
export async function getComment(id: number) {
  return helpdeskClient.get<ImprovementComment>(`/comments/${id}`);
}

/** 댓글 삭제 */
export async function deleteComment(id: number) {
  return helpdeskClient.delete(`/comments/${id}`);
}

/** 내가 쓴 댓글 목록 */
export async function getMyComments(params?: {
  endDate?: string;
  keyword?: string;
  startDate?: string;
}) {
  return helpdeskClient.get<ImprovementComment[]>('/comments/my', { params });
}

// ============================================================
// 첨부파일
// ============================================================

/** 특정 엔티티의 첨부파일 목록 */
export async function getAttachments(entityType: string, entityId: number) {
  return helpdeskClient.get<Attachment[]>('/attachments', {
    params: { entityId, entityType },
  });
}

/** 첨부파일 삭제 */
export async function deleteAttachment(id: number) {
  return helpdeskClient.delete(`/attachments/${id}`);
}

// ============================================================
// 요청 기반 리포트
// ============================================================

/** 월별 요청 통계 */
export async function getMonthlyReport(
  year: number,
  month: number,
  companyId?: number,
) {
  return helpdeskClient.get<any>('/requests/report/monthly', {
    params: { companyId, month, year },
  });
}

/** 협업 리포트 */
export async function getCollaborationReport(year: number, month: number) {
  return helpdeskClient.get<any>('/requests/report/collaboration', {
    params: { month, year },
  });
}

/** 품질 리포트 */
export async function getQualityReport(year: number, month: number) {
  return helpdeskClient.get<any>('/requests/report/quality', {
    params: { month, year },
  });
}

/** 긴급/장애 발생 목록 */
export async function getEmergencyIncidents() {
  return helpdeskClient.get<any>('/requests/report/emergency');
}
