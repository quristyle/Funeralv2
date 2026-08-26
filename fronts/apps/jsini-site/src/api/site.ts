/**
 * SiteServer 의 공개 조회.
 *
 * axios 를 쓰지 않는다. 여기서 필요한 것은 GET 몇 개와 POST 하나뿐이고,
 * 정적 사이트라 번들 크기가 곧 첫 화면 속도다. `fetch` 로 충분하다.
 *
 * 프리렌더(vite-ssg) 때는 서버가 떠 있지 않을 수 있다. 그때 빌드가 실패하면
 * 배포가 API 상태에 묶이므로, 실패하면 **빈 값을 돌려주고 화면은 그대로 그린다.**
 * 자료는 브라우저에서 다시 불러온다.
 */
const BASE = '/api/site';

export interface Section {
  sectionKey: string;
  title: string;
  subtitle?: string;
  body?: string;
  sortOrder: number;
}

export interface PostListItem {
  slug: string;
  title: string;
  summary?: string;
  coverUrl?: string;
  publishedAt?: string;
}

export interface PostDetail extends PostListItem {
  body?: string;
}

export interface DownloadItem {
  id: string;
  title: string;
  description?: string;
  category?: string;
  fileName?: string;
  fileSize?: number;
  downloadCount: number;
  downloadUrl: string;
}

/** 이 저장소의 응답 봉투. `data.result` 안에 목록이 들어온다. */
interface Envelope<T> {
  success: boolean;
  data?: { result?: T[] };
}

async function getList<T>(path: string): Promise<T[]> {
  try {
    const res = await fetch(`${BASE}${path}`);
    if (!res.ok) return [];
    const json = (await res.json()) as Envelope<T>;
    return json.data?.result ?? [];
  } catch {
    // 프리렌더 중이거나 서버가 내려간 경우. 화면은 빈 상태로 그린다.
    return [];
  }
}

export interface InquiryRequest {
  name: string;
  company?: string;
  email: string;
  phone?: string;
  category?: string;
  subject: string;
  message: string;
  locale: string;
  /** 개인정보 수집·이용 동의. false 면 서버가 거절한다 */
  consent: boolean;
  /**
   * 허니팟. 사람에게는 보이지 않는 칸이라 **비어 있어야 정상**이다.
   * 채워져 있으면 서버가 조용히 버리고 성공 응답을 준다 — 봇에게 단서를 주지 않는다.
   */
  website?: string;
}

export const siteApi = {
  sections: (locale: string, keyPrefix?: string) =>
    getList<Section>(
      `/sections?locale=${locale}${keyPrefix ? `&keyPrefix=${encodeURIComponent(keyPrefix)}` : ''}`,
    ),

  posts: (locale: string, take = 20) => getList<PostListItem>(`/posts?locale=${locale}&take=${take}`),

  post: async (locale: string, slug: string): Promise<PostDetail | null> => {
    const rows = await getList<PostDetail>(`/posts/${encodeURIComponent(slug)}?locale=${locale}`);
    return rows[0] ?? null;
  },

  downloads: (locale: string, category?: string) =>
    getList<DownloadItem>(
      `/downloads?locale=${locale}${category ? `&category=${encodeURIComponent(category)}` : ''}`,
    ),

  /**
   * 문의를 접수한다.
   *
   * 실패 이유를 자세히 알려 주지 않는다. 특히 **허니팟에 걸린 경우 서버가 성공 응답을 준다** —
   * 봇에게 무엇에 걸렸는지 알려 주지 않기 위한 것이라, 화면도 그대로 성공으로 처리한다.
   * 게이트웨이가 IP 당 분당 3회로 조이므로 429 가 올 수 있다.
   */
  submitInquiry: async (
    body: InquiryRequest,
  ): Promise<{ message?: string; ok: boolean; rateLimited?: boolean }> => {
    try {
      const res = await fetch(`${BASE}/inquiries`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
      });

      if (res.status === 429) {
        return { ok: false, rateLimited: true };
      }

      const json = (await res.json().catch(() => null)) as
        | { message?: string; success?: boolean }
        | null;

      return { ok: res.ok && json?.success !== false, message: json?.message };
    } catch {
      return { ok: false };
    }
  },

  /**
   * 조회를 센다. 실패해도 아무것도 하지 않는다 — 집계는 부수 효과일 뿐이다.
   * 브라우저에서만 부른다(프리렌더 때 부르면 빌드가 숫자를 올려 버린다).
   */
  recordVisit: (path: string, locale: string) => {
    if (typeof window === 'undefined') return;
    void fetch(`${BASE}/visits?path=${encodeURIComponent(path)}&locale=${locale}`, {
      method: 'POST',
    }).catch(() => {});
  },
};
