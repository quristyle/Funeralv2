/**
 * 헬프데스크 화면들이 공유하는 상수.
 *
 * 상태·유형 코드는 서버 열거형(HelpDeskServer/Models/ImprovementStatus.cs)의 순번과 일치해야 한다.
 * 서버는 JSON 응답에 이름(`Completed`)을 싣고, 검색 조건으로는 순번(`3`)을 받는다.
 */
import type { ImprovementStatus, ImprovementType } from '#/api/helpdesk';

/** 상태 정의 — code 는 서버 열거형 순번 */
export interface StatusOption {
  code: number;
  color: string;
  label: string;
  value: ImprovementStatus;
}

/** 요청 상태 목록 */
export const REQUEST_STATUSES: StatusOption[] = [
  { code: 0, color: 'default', label: '대기', value: 'Pending' },
  { code: 1, color: 'processing', label: '진행', value: 'InProgress' },
  { code: 5, color: 'cyan', label: '협의', value: 'Consultation' },
  { code: 6, color: 'geekblue', label: '논의', value: 'Negotiation' },
  { code: 2, color: 'error', label: '반려', value: 'Rejected' },
  { code: 3, color: 'success', label: '완료', value: 'Completed' },
  { code: 7, color: 'green', label: '종료', value: 'UserCompleted' },
  { code: 4, color: 'default', label: '삭제', value: 'Delete' },
];

/** 상태 셀렉트 옵션 (전체 포함) */
export const REQUEST_STATUS_OPTIONS = [
  { label: '전체', value: null },
  ...REQUEST_STATUSES.map((s) => ({ label: s.label, value: s.code })),
];

/** 상태 이름으로 표시 정보를 찾는다. */
export function statusMeta(status?: ImprovementStatus | string) {
  return (
    REQUEST_STATUSES.find((s) => s.value === status) ?? {
      code: -1,
      color: 'default',
      label: status ?? '',
      value: 'Pending' as ImprovementStatus,
    }
  );
}

/** 요청 유형 정의 */
export interface TypeOption {
  code: number;
  label: string;
  value: ImprovementType;
}

/** 요청 유형 목록 */
export const REQUEST_TYPES: TypeOption[] = [
  { code: 0, label: '질문', value: 'Question' },
  { code: 1, label: '개선', value: 'Improvement' },
  { code: 2, label: '추가', value: 'Addition' },
  { code: 3, label: '기타', value: 'Etc' },
  { code: 4, label: '오류', value: 'Error' },
  { code: 5, label: '버그', value: 'Bug' },
  { code: 6, label: '긴급/장애', value: 'Emergency' },
];

/** 유형 셀렉트 옵션 */
export const REQUEST_TYPE_OPTIONS = REQUEST_TYPES.map((t) => ({
  label: t.label,
  value: t.value,
}));

/** 유형 이름 → 표시 라벨 */
export function typeLabel(type?: ImprovementType | string) {
  return REQUEST_TYPES.find((t) => t.value === type)?.label ?? type ?? '';
}

/** 'YYYY-MM-DD HH:mm' 로 표시. 값이 없으면 빈 문자열. */
export function formatDateTime(value?: null | string) {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** 'YYYY-MM-DD' 로 표시. 값이 없으면 빈 문자열. */
export function formatDate(value?: null | string) {
  if (!value) return '';
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return '';
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}
