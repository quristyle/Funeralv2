import { unwrapList } from '#/api/envelope';
import { currentAiModel, currentAiProvider } from '#/api/portal/ai/provider';
import { requestClient } from '#/api/request';

export namespace SystemI18nApi {
  export interface I18nResource {
    id: number;
    key: string;
    locale: string;
    value: string;
    category?: string;
  }

  export interface CreateI18nResource {
    key: string;
    locale: string;
    value: string;
    category?: string;
  }
}

/**
 * 특정 로케일의 다국어 자원 목록 조회
 */
export async function getI18nListByLocale(locale: string) {
  try {
    return unwrapList<SystemI18nApi.I18nResource>(
      await requestClient.get(`/auth/system/i18n/${locale}`),
    );
  } catch (error) {
    console.warn(`[I18n API] 서버 접근 불가. 로컬 다국어 파일로 대체합니다. (${locale})`, error);
    return [] as SystemI18nApi.I18nResource[];
  }
}

/**
 * 전체 다국어 자원 목록 조회
 */
export async function getAllI18nList() {
  return requestClient.get<SystemI18nApi.I18nResource[]>('/auth/system/i18n/list');
}

/**
 * 다국어 자원 페이징 조회
 */
export async function getI18nPaged(params: any) {
  return requestClient.get<any>('/auth/system/i18n/paged', { params });
}

/**
 * 다국어 자원 생성
 */
export async function createI18nResource(data: SystemI18nApi.CreateI18nResource) {
  return requestClient.post<SystemI18nApi.I18nResource>('/auth/system/i18n', data);
}

/**
 * 다국어 자원 수정
 */
export async function updateI18nResource(id: number, data: SystemI18nApi.CreateI18nResource) {
  return requestClient.put<boolean>(`/auth/system/i18n/${id}`, data);
}

/**
 * 다국어 자원 삭제
 */
export async function deleteI18nResource(id: number) {
  return requestClient.delete<boolean>(`/auth/system/i18n/${id}`);
}

/**
 * 누락된 다국어 키 존재 확인 및 자동 생성 요청
 */
export async function ensureI18nResource(data: { locale: string; key: string; defaultValue?: string }) {
  // 당분간 중지
  //return requestClient.post<boolean>('/auth/system/i18n/ensure', data);
  return null;
}

/**
 * 다국어 키를 바탕으로 번역(한글/영문) 추천 받기
 */
export async function suggestI18nTranslation(key: string, targetLang: string) {
  // provider — 사용자가 환경설정에서 고른 AI 모델(#/api/portal/ai/provider.ts).
  return requestClient.get<unknown>('/ai/suggest-i18n', {
    params: { key, targetLang, provider: currentAiProvider(), model: currentAiModel() },
  });
}
