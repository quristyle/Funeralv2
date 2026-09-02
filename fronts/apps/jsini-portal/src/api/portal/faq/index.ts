import { unwrapOne } from '#/api/envelope';
import { requestClient } from '#/api/request';

/**
 * F.A.Q API.
 *
 * F.A.Q 는 JSini 관리 포털이 관리하고 모든 MSA 사용자에게 공통으로 보인다.
 * 각 MSA 가 자기 F.A.Q 를 따로 두지 않는다(공지와 같은 방침).
 *
 * 관리자만 등록·수정·삭제하고 나머지 사용자는 읽는다. **판정은 서버가 한다** —
 * 목록 응답의 `canManage` 를 보고 화면이 버튼을 켜고, 저장 요청은 서버가 다시 확인한다.
 */
export namespace FaqApi {
  /** F.A.Q 한 건 */
  export interface Faq {
    /** 답변 (HTML) */
    answer?: null | string;
    /** 분류. 비우면 화면이 '기타' 로 묶는다. */
    category?: null | string;
    createdAt?: string;
    id: string;
    orderNo: number;
    question: string;
    /** 0: 비활성, 1: 활성 */
    status: number;
    updatedAt?: null | string;
  }

  /** 목록 응답 */
  export interface FaqList {
    /** 지금 등록된 분류 목록. 등록 창의 분류 추천에 쓴다. */
    categories: string[];
    /** 등록·수정·삭제할 수 있는 사용자인지 (= 관리자) */
    canManage: boolean;
    items: Faq[];
  }

  /** 등록·수정 요청 */
  export interface SaveFaq {
    answer?: null | string;
    category?: null | string;
    orderNo: number;
    question: string;
    status: number;
  }
}

/**
 * AuthServer 의 응답 필터는 단건 객체도 `{ result: [ ... ], page }` 로 감싸 보낸다.
 * 배열로 와도 객체로 와도 하나를 꺼내 준다.
 */
function pickOne<T>(res: any): T | undefined {
  return unwrapOne<T>(res);
}

/** 목록. 관리자에게는 비활성 항목까지 온다. */
export async function getFaqList(params?: {
  category?: string;
  keyword?: string;
}) {
  const res = await requestClient.get<any>('/auth/faqs', { params });

  return (
    pickOne<FaqApi.FaqList>(res) ?? {
      canManage: false,
      categories: [],
      items: [],
    }
  );
}

export async function getFaq(id: string) {
  const res = await requestClient.get<any>(`/auth/faqs/${id}`);
  return pickOne<FaqApi.Faq>(res);
}

export async function createFaq(data: FaqApi.SaveFaq) {
  return requestClient.post('/auth/faqs', data);
}

export async function updateFaq(id: string, data: FaqApi.SaveFaq) {
  return requestClient.put(`/auth/faqs/${id}`, data);
}

export async function deleteFaq(id: string) {
  return requestClient.delete(`/auth/faqs/${id}`);
}
