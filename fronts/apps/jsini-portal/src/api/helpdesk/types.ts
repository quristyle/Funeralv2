/**
 * 헬프데스크 도메인 타입.
 * HelpDeskServer 의 Models/* 와 대응한다.
 */

/**
 * 개선요청 처리 상태.
 * 서버는 JSON 에 이름으로 실어 보내고, 검색 조건에는 열거형 순번(0~7)을 받는다.
 */
export type ImprovementStatus =
  | 'Completed'
  | 'Consultation'
  | 'Delete'
  | 'InProgress'
  | 'Negotiation'
  | 'Pending'
  | 'Rejected'
  | 'UserCompleted';

/** 개선요청 유형 */
export type ImprovementType =
  | 'Addition'
  | 'Bug'
  | 'Emergency'
  | 'Error'
  | 'Etc'
  | 'Improvement'
  | 'Question';

/** 고객 계정 상태 */
export type CustomerStatus = 'Active' | 'Pending' | 'Rejected';

/** 헬프데스크 계정 종류 */
export type HelpdeskUserType = 'admin' | 'customer';

/** 모든 엔티티가 공유하는 감사 필드 */
export interface HelpdeskBaseEntity {
  createdAt?: string;
  createdBy?: string;
  id: number;
  modifiedAt?: string;
  modifiedBy?: string;
}

/** 고객사 */
export interface Company extends HelpdeskBaseEntity {
  name: string;
}

/** 팀 */
export interface Team extends HelpdeskBaseEntity {
  name: string;
  remark?: string;
}

/** 관리자 */
export interface Admin extends HelpdeskBaseEntity {
  adminTeams?: AdminTeam[];
  email?: string;
  isDeleted?: boolean;
  loginId: string;
  mustChangePassword?: boolean;
  password?: string;
  photo?: string;
  userName: string;
}

/** 관리자-팀 매핑 */
export interface AdminTeam {
  adminId: number;
  team?: Team;
  teamId: number;
}

/** 고객 */
export interface Customer extends HelpdeskBaseEntity {
  company?: Company;
  companyId: number;
  email?: string;
  isDeleted?: boolean;
  loginId: string;
  password?: string;
  photo?: string;
  remake?: string;
  sex?: string;
  status?: CustomerStatus;
  userName: string;
}

/** 개선요청 */
export interface ImprovementRequest extends HelpdeskBaseEntity {
  assignedAdmin?: Admin;
  assignedAdminId?: null | number;
  attachments?: Attachment[];
  comments?: ImprovementComment[];
  companyId?: number;
  completedAt?: null | string;
  content?: string;
  customer?: Customer;
  customerId?: number;
  dueDate?: null | string;
  ipType?: ImprovementType;
  isEmergency?: boolean;
  mainPhoto?: string;
  attachmentCount?: number;
  admin?: Admin;
  adminId?: null | number;
  company?: Company;
  completededAt?: null | string;
  description?: string;
  projectId?: null | number;
  status: ImprovementStatus;
  statusName?: string;
  title: string;
}

/** 요청 댓글 */
export interface ImprovementComment extends HelpdeskBaseEntity {
  attachments?: Attachment[];
  authorName?: string;
  authorType?: HelpdeskUserType;
  content: string;
  parentId?: null | number;
  requestId: number;
}

/** 첨부파일 */
export interface Attachment extends HelpdeskBaseEntity {
  contentType?: string;
  entityId?: number;
  entityType?: string;
  fileName: string;
  filePath?: string;
  fileSize?: number;
}


/** 프로젝트 */
export interface Project extends HelpdeskBaseEntity {
  companyId?: null | number;
  name: string;
  projectEnd?: null | string;
  projectStart?: null | string;
  remark?: string;
  team?: Team;
  teamId?: null | number;
}

/** WBS 작업 항목 */
export interface Wbs {
  actualEnd?: null | string;
  actualStart?: null | string;
  managerId?: null | number;
  parentWbsId?: null | number;
  planEnd?: null | string;
  planStart?: null | string;
  priority?: string;
  progress?: number;
  projectId: number;
  responsibleUserId?: null | number;
  riskLevel?: null | string;
  status?: string;
  wbsCode?: string;
  wbsLevel?: null | number;
  wbsName: string;
  wbsRid: number;
  wbsType?: null | string;
}

/**
 * 서버가 내려주는 WBS 트리 노드.
 * 원본이 PrimeVue TreeTable 을 쓰던 구조 그대로다 — 실제 값은 `data` 안에 있다.
 */
export interface WbsTreeNode {
  children?: WbsTreeNode[];
  data: Wbs;
  key: string;
}

/** WBS 선후행 연결 */
export interface WbsLink {
  id: number;
  source: number;
  target: number;
  type?: string;
}

/** 일정 */
export interface Schedule extends Omit<HelpdeskBaseEntity, 'id'> {
  companyId?: null | number;
  completedDate?: null | string;
  description?: string;
  endDate?: string;
  /**
   * 일정만 기본키가 uuid 다. 다른 엔티티는 정수 id 를 쓴다.
   * (jsini.schedules.id 가 uuid 타입)
   */
  id: string;
  /** 특정 회사에 묶이지 않은 공통 일정인지 */
  isCommon?: boolean;
  isCompleted?: boolean;
  startDate?: string;
  title: string;
}

/** 체크리스트 항목 */
export interface Checklist extends HelpdeskBaseEntity {
  category?: string;
  completedAt?: null | string;
  isChecked?: boolean;
  itemName: string;
  note?: string;
  sortOrder?: number;
}

/**
 * 사용자 개인 설정.
 * 서버는 값을 문자열로 저장한다 — 불리언 설정도 `'true'` / `'false'` 로 오간다.
 */
export type UserPropertyMap = Record<string, string>;

/** funeralv2 계정 ↔ 헬프데스크 계정 매핑 */
export interface AuthUserLink {
  authUserId: string;
  createdAt?: string;
  helpdeskUserId: number;
  id: number;
  userName?: string;
  userType: HelpdeskUserType;
}

/**
 * 현재 토큰이 해석된 헬프데스크 신원.
 *
 * 두 가지를 구분해서 담는다.
 *
 * - `isAdmin` — **무엇을 할 수 있는가.** 포털 역할이 관리자면 계정 연결이 없어도 참이다.
 *   조회·관리 화면을 열지 결정하는 값이다.
 * - `linked` / `helpdeskUserId` — **'내 것'을 가리킬 수 있는가.** 요청 작성자·담당자·댓글은
 *   헬프데스크 내부 ID 를 참조하므로, 연결이 없으면 "내가 쓴 것"을 찾을 수 없다.
 *
 * 전에는 `helpdeskUserId` 하나로 둘을 겸했다. 그래서 연결이 없는 관리자 계정은
 * 조회 화면조차 열리지 않았다.
 */
export interface HelpdeskIdentity {
  /** 담당자 권한이 계정 연결이 아니라 포털 역할에서 온 것인가 (안내 문구가 갈린다) */
  adminByRole?: boolean;
  companyId?: null | string;
  /** 연결된 헬프데스크 내부 ID. 연결이 없으면 없다 */
  helpdeskUserId?: null | number;
  /** 담당자 권한이 있는가 */
  isAdmin?: boolean;
  /** 헬프데스크 내부 레코드에 이어져 있는가 */
  linked?: boolean;
  loginType?: HelpdeskUserType | null;
  userName?: string;
}

/** 목록 검색 공통 파라미터 */
export interface HelpdeskSearchParams {
  [key: string]: any;
  pageNo?: number;
  pageSize?: number;
  sortField?: string;
  sortOrder?: 'asc' | 'desc';
}
