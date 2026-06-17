export type Locale = 'en-US' | 'ko-KR';

export const messages: Record<Locale, Record<string, string>> = {
  'en-US': {
    cancel: 'Cancel',
    collapse: 'Collapse',
    confirm: 'Confirm',
    expand: 'Expand',
    prompt: 'Prompt',
    reset: 'Reset',
    submit: 'Submit',
  },
  'ko-KR': {
    cancel: '취소',
    collapse: '접기',
    confirm: '확인',
    expand: '펼치기',
    prompt: '안내',
    reset: '초기화',
    submit: '제출',
  },
};

export const getMessages = (locale: Locale) => messages[locale];
