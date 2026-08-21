import { requestClient } from '#/api/request';

export namespace HelpApi {
  export interface Inquiry {
    id: string;
    title: string;
    content: string;
    status: 'PENDING' | 'ANSWERED';
    answer?: string;
    authorName: string;
    createdAt: string;
  }

  export interface Qna {
    id: string;
    question: string;
    answer?: string;
    isPublic: boolean;
    authorName: string;
    createdAt: string;
  }

  export interface Faq {
    id: string;
    question: string;
    answer: string;
    category: string;
    sortOrder: number;
  }

  export interface ArchiveItem {
    id: string;
    title: string;
    content?: string;
    fileName?: string;
    fileUrl?: string;
    fileSize?: number;
    downloadCount: number;
    createdAt: string;
  }
}

// === 문의 API ===
export async function getInquiries() {
  return requestClient.get<HelpApi.Inquiry[]>('/help/inquiry/list');
}
export async function createInquiry(data: { title: string; content: string }) {
  return requestClient.post('/help/inquiry', data);
}

// === Q&A API ===
export async function getQnas() {
  return requestClient.get<HelpApi.Qna[]>('/help/qna/list');
}
export async function createQna(data: { question: string; isPublic: boolean }) {
  return requestClient.post('/help/qna', data);
}

// === FAQ API ===
export async function getFaqs() {
  return requestClient.get<HelpApi.Faq[]>('/help/faq/list');
}
export async function createFaq(data: Omit<HelpApi.Faq, 'id'>) {
  return requestClient.post('/help/faq', data);
}
export async function updateFaq(id: string, data: Omit<HelpApi.Faq, 'id'>) {
  return requestClient.put(`/help/faq/${id}`, data);
}
export async function deleteFaq(id: string) {
  return requestClient.delete(`/help/faq/${id}`);
}

// === 자료실 API ===
export async function getArchiveItems() {
  return requestClient.get<HelpApi.ArchiveItem[]>('/help/archive/list');
}
export async function downloadArchiveFile(id: string) {
  return requestClient.post(`/help/archive/${id}/download`);
}
