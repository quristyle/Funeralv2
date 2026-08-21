/**
 * 프로젝트 · WBS · 일정 · 체크리스트 · 공지 API.
 */
import type {
  Checklist,
  Notice,
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

/** 일정 목록 */
export async function getSchedules(params?: { companyId?: number }) {
  return helpdeskClient.get<Schedule[]>('/schedules', { params });
}

/** 일정 단건 조회 */
export async function getSchedule(id: number) {
  return helpdeskClient.get<Schedule>(`/schedules/${id}`);
}

/** 일정 생성 */
export async function createSchedule(data: Partial<Schedule>) {
  return helpdeskClient.post<Schedule>('/schedules', data);
}

/** 일정 수정 */
export async function updateSchedule(id: number, data: Partial<Schedule>) {
  return helpdeskClient.put<Schedule>(`/schedules/${id}`, data);
}

/** 일정 삭제 */
export async function deleteSchedule(id: number) {
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

/** 공지 목록 */
export async function getNotices() {
  return helpdeskClient.get<Notice[]>('/notices');
}

/** 공지 단건 조회 */
export async function getNotice(id: number) {
  return helpdeskClient.get<Notice>(`/notices/${id}`);
}

/** 공지 생성 */
export async function createNotice(data: Partial<Notice>) {
  return helpdeskClient.post<Notice>('/notices', data);
}

/** 공지 수정 */
export async function updateNotice(id: number, data: Partial<Notice>) {
  return helpdeskClient.put<Notice>(`/notices/${id}`, data);
}

/** 공지 삭제 */
export async function deleteNotice(id: number) {
  return helpdeskClient.delete(`/notices/${id}`);
}

// ============================================================
// 릴리즈 빌드 도구
// ============================================================

/** 릴리즈 빌드 실행 */
export async function runRelease(payload?: Record<string, any>) {
  return helpdeskClient.post('/build/release', payload ?? {});
}

/** GitHub 릴리즈 빌드 실행 */
export async function runReleaseGithub(payload?: Record<string, any>) {
  return helpdeskClient.post('/build/release_ghub', payload ?? {});
}
