/**
 * 로그인 페이지 url 주소
 */
export const LOGIN_PATH = '/auth/login';

export interface LanguageOption {
  label: string;
  value: 'en-US' | 'ko-KR' ;
}

/**
 * Supported languages
 */
export const SUPPORT_LANGUAGES: LanguageOption[] = [
  {
    label: '한국어',
    value: 'ko-KR',
  },
  {
    label: 'English',
    value: 'en-US',
  },
  {
    label: '키확인용',
    value: 'ab-AB',
  },
];
