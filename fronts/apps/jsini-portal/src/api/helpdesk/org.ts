/**
 * 헬프데스크 조직 API — 회사 / 팀 / 관리자 / 고객.
 */
import type {
  Admin,
  Company,
  Customer,
  HelpdeskSearchParams,
  Team,
} from './types';

import { helpdeskClient, helpdeskFetchPage } from './request';

// ============================================================
// 회사
// ============================================================

/** 회사 전체 목록 */
export async function getCompanyList() {
  return helpdeskClient.get<Company[]>('/companys');
}

/** 회사 검색 (페이징) */
export async function searchCompanies(params: HelpdeskSearchParams) {
  return helpdeskFetchPage<Company>('/companys/srch', params);
}

/** 회사 단건 조회 */
export async function getCompany(id: number) {
  return helpdeskClient.get<Company>(`/companys/${id}`);
}

/** 회사 생성 */
export async function createCompany(data: Partial<Company>) {
  return helpdeskClient.post<Company>('/companys', data);
}

/** 회사 수정 */
export async function updateCompany(id: number, data: Partial<Company>) {
  return helpdeskClient.put<Company>(`/companys/${id}`, data);
}

/** 회사 삭제 */
export async function deleteCompany(id: number) {
  return helpdeskClient.delete(`/companys/${id}`);
}

// ============================================================
// 팀
// ============================================================

/** 팀 전체 목록 */
export async function getTeamList() {
  return helpdeskClient.get<Team[]>('/teams');
}

/** 팀 검색 (페이징) */
export async function searchTeams(params: HelpdeskSearchParams) {
  return helpdeskFetchPage<Team>('/teams/srch', params);
}

/** 팀 단건 조회 */
export async function getTeam(id: number) {
  return helpdeskClient.get<Team>(`/teams/${id}`);
}

/** 팀 생성 */
export async function createTeam(data: Partial<Team>) {
  return helpdeskClient.post<Team>('/teams', data);
}

/** 팀 수정 */
export async function updateTeam(id: number, data: Partial<Team>) {
  return helpdeskClient.put<Team>(`/teams/${id}`, data);
}

/** 팀 삭제 */
export async function deleteTeam(id: number) {
  return helpdeskClient.delete(`/teams/${id}`);
}

/** 팀에 배정된 회사 목록 */
export async function getTeamCompanies(teamId: number) {
  return helpdeskClient.get<Company[]>(`/teams/${teamId}/companies`);
}

/** 팀에 회사 배정 */
export async function setTeamCompanies(teamId: number, companyIds: number[]) {
  return helpdeskClient.post(`/teams/${teamId}/companies`, { companyIds });
}

// ============================================================
// 관리자
// ============================================================

/** 관리자 전체 목록 */
export async function getAdminList() {
  return helpdeskClient.get<Admin[]>('/admins');
}

/** 관리자 검색 (페이징) */
export async function searchAdmins(params: HelpdeskSearchParams) {
  return helpdeskFetchPage<Admin>('/admins/srch', params);
}

/** 관리자 단건 조회 */
export async function getAdmin(id: number) {
  return helpdeskClient.get<Admin>(`/admins/${id}`);
}

/** 관리자 생성 */
export async function createAdmin(data: Partial<Admin>) {
  return helpdeskClient.post<Admin>('/admins', data);
}

/** 관리자 수정 */
export async function updateAdmin(id: number, data: Partial<Admin>) {
  return helpdeskClient.put<Admin>(`/admins/${id}`, data);
}

/** 관리자 삭제 */
export async function deleteAdmin(id: number) {
  return helpdeskClient.delete(`/admins/${id}`);
}

/** 관리자 비밀번호 변경 */
export async function changeAdminPassword(payload: {
  currentPassword?: string;
  loginId: string;
  newPassword: string;
}) {
  return helpdeskClient.post('/admins/change-password', payload);
}

/** 비밀번호 찾기 */
export async function findAdminPassword(payload: {
  email: string;
  loginId: string;
}) {
  return helpdeskClient.post('/admins/find-password', payload);
}

// ============================================================
// 고객
// ============================================================

/** 고객 전체 목록 */
export async function getCustomerList() {
  return helpdeskClient.get<Customer[]>('/customers');
}

/** 고객 검색 (페이징) */
export async function searchCustomers(params: HelpdeskSearchParams) {
  return helpdeskFetchPage<Customer>('/customers/srch', params);
}

/** 고객 단건 조회 */
export async function getCustomer(id: number) {
  return helpdeskClient.get<Customer>(`/customers/${id}`);
}

/** 고객 생성 */
export async function createCustomer(data: Partial<Customer>) {
  return helpdeskClient.post<Customer>('/customers', data);
}

/** 고객 수정 */
export async function updateCustomer(id: number, data: Partial<Customer>) {
  return helpdeskClient.put<Customer>(`/customers/${id}`, data);
}

/** 고객 삭제 */
export async function deleteCustomer(id: number) {
  return helpdeskClient.delete(`/customers/${id}`);
}

/**
 * 로그인 아이디로 고객을 찾는다.
 * 회사마다 `pub_{회사ID}` 형태의 공용 계정이 있어, 관리자가 회사를 대신해
 * 요청을 등록할 때 그 계정을 작성자로 쓴다.
 */
export async function getCustomerByLoginId(loginId: string) {
  const list = (await getCustomerList()) ?? [];
  return list.find((c) => c.loginId === loginId);
}

/** 관리자·고객을 합친 사용자 목록 (담당자 선택 등에 사용) */
export async function getAllUsers() {
  return helpdeskClient.get<
    { userId: number; userName: string; userType: string }[]
  >('/users/');
}

// ============================================================
// 문의하기
// ============================================================

/** 문의 등록 요청 */
export interface ContactUsPayload {
  email: string;
  message: string;
  name: string;
  subject: string;
}

/** 문의를 등록한다. 서버가 담당자에게 메일·푸시로 알린다. */
export async function sendContactUs(payload: ContactUsPayload) {
  return helpdeskClient.post('/contact', payload);
}
