import { requestClient } from '#/api/request';

/**
 * ⚠ 이 파일의 함수들은 **아직 붙지 않은 임시 자리**다.
 * 가리키는 `/help/**` 경로는 실제로 없다.
 *
 * 실제로 동작하는 것은 AuthServer 에 붙은 아래 두 곳이다.
 *   F.A.Q  `#/api/portal/faq`   (화면: views/funeral/help/faq)
 *   Q&A    `#/api/portal/qna`   (화면: views/funeral/help/qna)
 *
 * 남아 있는 `faq-custom` · `archive-custom` 화면이 이 파일을 아직 참조하고 있어서
 * 지우지 않았다. 새로 붙일 때는 위의 두 곳을 쓴다.
 *
 * 문의(`/help/inquiry`)와 Q&A 함수는 **지웠다** —
 * 문의 화면은 Q&A 가 같은 일을 해서 없앴고(`docs/sql/help_inquiry_drop.sql`),
 * Q&A 는 위의 `#/api/portal/qna` 로 옮겼다.
 */
export namespace HelpApi {
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
