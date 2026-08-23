/**
 * 프로젝트 · WBS · 일정 · 체크리스트 · 공지 API.
 */
import type {
  Checklist,
  Project,
  Schedule,
  Wbs,
  WbsLink,
  WbsTreeNode,
} from './types';

import { helpdeskClient } from './request';

// ============================================================
// 프로젝트
// ============================================================

/** 프로젝트 목록 */
export async function getProjects() {
  return helpdeskClient.get<Project[]>('/project');
}

/** 프로젝트 생성 */
export async function createProject(data: Partial<Project>) {
  return helpdeskClient.post<Project>('/project', data);
}

/** 프로젝트 수정 */
export async function updateProject(id: number, data: Partial<Project>) {
  return helpdeskClient.put<Project>(`/project/${id}`, data);
}

/** 프로젝트 삭제 */
export async function deleteProject(id: number) {
  return helpdeskClient.delete(`/project/${id}`);
}

/** 프로젝트 대시보드 통계 */
export async function getProjectStats(projectId: number) {
  return helpdeskClient.get<any>(`/dashboard/project-stats/${projectId}`);
}

// ============================================================
// WBS
// ============================================================

/**
 * 프로젝트의 WBS 트리.
 * 서버가 이미 계층 구조({key, data, children})로 만들어 내려준다.
 */
export async function getWbsTree(projectId: number) {
  return helpdeskClient.get<WbsTreeNode[]>('/wbs', { params: { projectId } });
}

/** WBS 항목 생성 */
export async function createWbs(data: Partial<Wbs>) {
  return helpdeskClient.post<Wbs>('/wbs', data);
}

/** WBS 항목 수정 */
export async function updateWbs(wbsRid: number, data: Partial<Wbs>) {
  return helpdeskClient.put<Wbs>(`/wbs/${wbsRid}`, data);
}

/** WBS 항목 삭제 */
export async function deleteWbs(wbsRid: number) {
  return helpdeskClient.delete(`/wbs/${wbsRid}`);
}

/** WBS 선후행 연결 목록 */
export async function getWbsLinks(projectId: number) {
  return helpdeskClient.get<WbsLink[]>('/wbslink', { params: { projectId } });
}

/** WBS 연결 생성 */
export async function createWbsLink(data: Partial<WbsLink>) {
  return helpdeskClient.post<WbsLink>('/wbslink', data);
}

/** WBS 연결 삭제 */
export async function deleteWbsLink(id: number) {
  return helpdeskClient.delete(`/wbslink/${id}`);
}

/**
 * WBS 다이어그램 조회. 없으면 404 이므로 호출부에서 잡아 null 로 다룬다.
 * `diagramData` 에 그래프 정의가 문자열로 들어 있다.
 */
export async function getWbsDiagram(wbsRid: number) {
  return helpdeskClient.get<{ diagramData?: string; wbsRid: number }>(
    `/wbs-diagram/${wbsRid}`,
  );
}

/** WBS 다이어그램 저장 (같은 wbsRid 가 있으면 덮어쓴다) */
export async function saveWbsDiagram(data: {
  diagramData: string;
  wbsRid: number;
}) {
  return helpdeskClient.post('/wbs-diagram', data);
}

// ============================================================
// 일정
// ============================================================

/**
 * 일정 API 만 응답 봉투가 다르다.
 *
 * 다른 엔드포인트는 `{ success, message, data }` 로 감싸 보내지만
 * ScheduleEndpoints 는 `{ data: [...] }` 만 보낸다(`success` 없음).
 * 요청 클라이언트는 `success` 가 있어야 봉투로 알아보고 벗겨내므로,
 * 여기서 한 번 더 확인해 배열을 꺼낸다.
 */
function unwrapSchedule<T>(res: any): T {
  if (res && typeof res === 'object' && !Array.isArray(res) && 'data' in res) {
    return res.data as T;
  }
  return res as T;
}

/** 일정 목록 */
export async function getSchedules(params?: { companyId?: number }) {
  const res = await helpdeskClient.get<any>('/schedules', { params });
  return unwrapSchedule<Schedule[]>(res) ?? [];
}

/** 일정 단건 조회 */
export async function getSchedule(id: string) {
  const res = await helpdeskClient.get<any>(`/schedules/${id}`);
  return unwrapSchedule<Schedule>(res);
}

/** 일정 생성 */
export async function createSchedule(data: Partial<Schedule>) {
  return helpdeskClient.post<Schedule>('/schedules', data);
}

/** 일정 수정 */
export async function updateSchedule(id: string, data: Partial<Schedule>) {
  return helpdeskClient.put<Schedule>(`/schedules/${id}`, data);
}

/** 일정 삭제 */
export async function deleteSchedule(id: string) {
  return helpdeskClient.delete(`/schedules/${id}`);
}

// ============================================================
// 체크리스트
// ============================================================

/** 체크리스트 목록 */
export async function getChecklists() {
  return helpdeskClient.get<Checklist[]>('/checklists');
}

/** 체크리스트 단건 조회 */
export async function getChecklist(id: number) {
  return helpdeskClient.get<Checklist>(`/checklists/${id}`);
}

/** 체크리스트 생성 */
export async function createChecklist(data: Partial<Checklist>) {
  return helpdeskClient.post<Checklist>('/checklists', data);
}

/** 체크리스트 수정 */
export async function updateChecklist(id: number, data: Partial<Checklist>) {
  return helpdeskClient.put<Checklist>(`/checklists/${id}`, data);
}

/** 체크리스트 삭제 */
export async function deleteChecklist(id: number) {
  return helpdeskClient.delete(`/checklists/${id}`);
}

// ============================================================
// 공지사항
// ============================================================

// 공지는 JSini 관리 포털이 공통으로 관리한다(#/api/portal/notice).
// 헬프데스크가 따로 들고 있던 공지 화면·API 는 제거했다.

// 배포(릴리즈)는 JSini 관리 포털이 관장한다(#/api/portal/release).
// 헬프데스크가 들고 있던 배포 화면·API 는 제거했다.
