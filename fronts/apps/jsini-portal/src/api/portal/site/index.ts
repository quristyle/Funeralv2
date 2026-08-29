import { requestClient } from '#/api/request';

/**
 * 회사 소개 사이트(SiteServer) 관리 API.
 *
 * 게이트웨이 경로는 /api/site/admin/* (인증 필수) 다.
 * 서버가 목록을 { data: { result: [...] } } 로 감싸므로 result 를 벗긴다.
 */
export namespace SiteInquiryApi {
  /** 문의 (관리용 — 본문 · 내부 메모 포함) */
  export interface Inquiry {
    id: string;
    name: string;
    company?: null | string;
    email: string;
    phone?: null | string;
    category?: null | string;
    subject: string;
    /** 본문 HTML — 서버가 허용 목록으로 거른 것이라 v-html 로 그려도 된다 */
    message: string;
    locale: string;
    /** new(신규) · reading(확인 중) · answered(답변 완료) · spam(스팸) */
    status: string;
    internalNote?: null | string;
    clientIp?: null | string;
    consentedAt: string;
    createdAt: string;
  }

  /** 답장 요청 */
  export interface Reply {
    /** 받는 사람 — 문의에 이메일이 있으면 채워 주고, 없으면 관리자가 적는다 */
    to: string;
    subject: string;
    /** 본문 HTML — 메일 틀은 서버가 입힌다 */
    body: string;
  }
}

function toList<T = any>(res: any): T[] {
  if (Array.isArray(res)) return res;
  if (Array.isArray(res?.result)) return res.result;
  if (Array.isArray(res?.data?.result)) return res.data.result;
  return [];
}

/** 문의 목록 (최근 500건) */
export async function getSiteInquiries(status?: string) {
  return toList<SiteInquiryApi.Inquiry>(
    await requestClient.get<any>('/site/admin/inquiries', {
      params: status ? { status } : undefined,
    }),
  );
}

/** 상태 변경 (new · reading · answered · spam) */
export async function setSiteInquiryStatus(id: string, value: string) {
  return requestClient.put(`/site/admin/inquiries/${id}/status`, null, {
    params: { value },
  });
}

/** 답장 보내기 — 성공하면 서버가 상태를 answered 로 올린다 */
export async function replySiteInquiry(id: string, data: SiteInquiryApi.Reply) {
  return requestClient.post(`/site/admin/inquiries/${id}/reply`, data);
}
