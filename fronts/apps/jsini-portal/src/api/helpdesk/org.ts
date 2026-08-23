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

// 회사 **관리 화면은 제거했다**. 회사는 포털(`/system/company`)에서 관리한다 —
// 헬프데스크에 있던 9건은 포털 회사 데이터로 옮겼다(remark 에 출처를 남겼다).
// 그래서 등록·수정·삭제·검색·단건조회 함수도 함께 없앴다.
//
// 목록만 남는다. 회사를 '관리'하는 것이 아니라 업무 데이터에서 회사를
// **가리키기 위해** 쓴다 — 요청 화면들의 회사 셀렉트(store/helpdesk.ts)와
// 팀-회사 매핑 화면(views/helpdesk/org/team-company.vue)이다.
//
// 두 체계의 회사가 아직 각자 남아 있다. 요청·팀 데이터가 헬프데스크 회사 ID 를
// 참조하므로 그쪽을 지울 수는 없다(헬프데스크 DB 는 건드리지 않는다).

/** 회사 전체 목록 */
export async function getCompanyList() {
  return helpdeskClient.get<Company[]>('/companys');
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

// 관리자 비밀번호 변경 API 는 제거했다.
// 인증과 비밀번호는 JSini 포털(AuthServer)이 단독으로 관리하고,
// 헬프데스크 자체 로그인은 꺼져 있다(HelpDeskServer 의 LocalLogin:Enabled, 기본 false).
// 남아 있던 헬프데스크 비밀번호는 아무 데서도 쓰이지 않는다.
// 서버의 /admins/change-password 엔드포인트는 JinReception 때문에 남겨 두었다.

/** 비밀번호 찾기 */
// 비밀번호 찾기 API 는 제거했다 (결정 D9-B).
// 인증 없이 남의 비밀번호를 초기화할 수 있어 계정 잠금에 악용될 수 있었다.
// 계정과 인증은 JSini 관리 포털이 일원 관리한다.

// ============================================================
// 고객
//
// 고객 사용자 **관리 화면은 제거했다**(이식본에만 있던 화면이고, 계정 관리는
// JSini 관리 포털이 단독으로 맡는다). 그래서 등록·수정·삭제 함수도 함께 없앴다.
//
// 아래 두 개만 남는다 — 둘 다 **읽기**이고, 고객을 '관리'하는 것이 아니라
// 업무 데이터에서 고객을 **가리키기 위해** 쓴다.
//   · 요청 화면들의 고객 셀렉트 (store/helpdesk.ts)
//   · 계정 대조 화면 (api/portal/system/msa-users.ts)
// ============================================================

/** 고객 전체 목록 */
export async function getCustomerList() {
  return helpdeskClient.get<Customer[]>('/customers');
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
