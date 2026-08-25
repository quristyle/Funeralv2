import { requestClient } from '#/api/request';

/**
 * Q&A API.
 *
 * 누구나 질문하고 관리자가 답한다. 답글에 답글을 다는 것도 같은 방식이라
 * 깊이 제한이 없다 — 답글은 `children` 에 같은 모양으로 계속 이어진다.
 *
 * [무엇이 보이나]
 *   관리자   전부
 *   그 외    공개된 글 + 자기가 쓴 글
 *
 * 서버가 이미 걸러서 내려주므로 화면은 받은 것을 그대로 그리면 된다.
 * 버튼을 켜고 끄는 판단도 서버가 준 값(`canManage` · `canWrite` · `canEdit`)을 쓴다.
 */
export namespace QnaApi {
  /** 질문·답글 (같은 모양이다) */
  export interface Post {
    /**
     * 작성자 프로필 사진 주소. 없으면 화면이 이름 첫 글자로 대신 그린다.
     * 글에 새겨 둔 값이 아니라 조회할 때 계정에서 읽은 것이다.
     */
    authorAvatar?: null | string;
    authorId?: null | string;
    authorName?: null | string;
    /** 고칠 수 있는지 (본인 글 또는 관리자) */
    canEdit: boolean;
    /** 답글. 깊이 제한 없이 같은 모양으로 이어진다. */
    children: Post[];
    /** 본문 (HTML) */
    content: string;
    createdAt?: string;
    depth: number;
    id: string;
    /** 관리자가 쓴 답변인지 */
    isAnswer: boolean;
    /** 관리자 답변이 하나라도 달렸는지 (질문에서만 채워진다) */
    isAnswered?: boolean;
    /** 내가 쓴 글인지 */
    isMine: boolean;
    /** 공개 여부. 관리자가 정한다. */
    isPublic: boolean;
    /** 스레드에서 가장 마지막 글의 시각 (질문에서만) */
    lastPostedAt?: null | string;
    /** 답글이면 부모 글 아이디 */
    parentId?: null | string;
    /** 보이는 답글 수 (질문에서만) */
    replyCount?: number;
    rootId: string;
    /** 제목. 질문(뿌리)만 갖는다. */
    title?: null | string;
    updatedAt?: null | string;
  }

  /** 목록 응답 */
  export interface PostList {
    /** 남의 글에 답하고 공개 여부를 정할 수 있는지 (= 관리자) */
    canManage: boolean;
    /** 질문·답글을 쓸 수 있는지 */
    canWrite: boolean;
    items: Post[];
    page: number;
    pageSize: number;
    /** 조건에 맞는 질문 수 (답글은 세지 않는다) */
    total: number;
  }

  /** 목록 조건 */
  export interface ListParams {
    /**
     * `mine` 내가 쓴 질문 · `pending` 공개 대기(관리자만) ·
     * `unanswered` 답변 없음 · 비우면 전체
     */
    filter?: string;
    keyword?: string;
    page?: number;
    pageSize?: number;
  }

  /** 질문·답글 등록 */
  export interface CreatePost {
    content: string;
    /** 공개 여부. 관리자가 보낸 값만 반영된다. */
    isPublic?: boolean;
    /** 답글이면 부모 글 아이디. 질문이면 비운다. */
    parentId?: null | string;
    /** 제목. 질문일 때만 쓴다. */
    title?: null | string;
  }

  /** 수정 */
  export interface UpdatePost {
    content: string;
    isPublic?: boolean;
    title?: null | string;
  }
}

/**
 * AuthServer 의 응답 필터는 단건 객체도 `{ result: [ ... ], page }` 로 감싸 보낸다.
 * 배열로 와도 객체로 와도 하나를 꺼내 준다.
 */
function pickOne<T>(res: any): T | undefined {
  const raw = res?.result ?? res?.data?.result ?? res;
  return (Array.isArray(raw) ? raw[0] : raw) as T | undefined;
}

export async function getQnaList(params?: QnaApi.ListParams) {
  const res = await requestClient.get<any>('/auth/qna', { params });

  return (
    pickOne<QnaApi.PostList>(res) ?? {
      canManage: false,
      canWrite: false,
      items: [],
      page: 1,
      pageSize: 20,
      total: 0,
    }
  );
}

/** 글 하나가 속한 스레드를 뿌리부터. 답글을 단 뒤 그 스레드만 다시 그릴 때 쓴다. */
export async function getQnaThread(id: string) {
  const res = await requestClient.get<any>(`/auth/qna/${id}`);
  return pickOne<QnaApi.Post>(res);
}

/** 질문(parentId 없음) · 답글(parentId 있음) 등록 */
export async function createQnaPost(data: QnaApi.CreatePost) {
  const res = await requestClient.post<any>('/auth/qna', data);
  return pickOne<QnaApi.Post>(res);
}

export async function updateQnaPost(id: string, data: QnaApi.UpdatePost) {
  return requestClient.put(`/auth/qna/${id}`, data);
}

/** 삭제. 답글까지 함께 지워진다. */
export async function deleteQnaPost(id: string) {
  return requestClient.delete(`/auth/qna/${id}`);
}

/**
 * 공개 여부 변경 (관리자 전용).
 *
 * @param includeReplies 참이면 답글까지 같은 값으로 함께 바꾼다.
 */
export async function setQnaVisibility(
  id: string,
  isPublic: boolean,
  includeReplies = false,
) {
  return requestClient.put(`/auth/qna/${id}/visibility`, {
    includeReplies,
    isPublic,
  });
}
