/**
 * 로그인 페이지 url 주소
 */
export const LOGIN_PATH = '/auth/login';

export interface LanguageOption {
  label: string;
  value: 'en' | 'ko';
}

/**
 * Supported languages
 */
export const SUPPORT_LANGUAGES: LanguageOption[] = [
  {
    label: '한국어',
    value: 'ko',
  },
  {
    label: 'English',
    value: 'en',
  },
];
