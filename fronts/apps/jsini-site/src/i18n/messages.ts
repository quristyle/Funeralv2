/**
 * 화면 고정 문구.
 *
 * vue-i18n 을 쓰지 않는다. 이 사이트의 문구는 대부분 DB(`site.sections`)에서 오고,
 * 여기 남는 것은 메뉴 이름처럼 코드에 붙은 몇 개뿐이다. 그 정도를 위해
 * i18n 런타임을 얹으면 정적 번들만 무거워진다.
 *
 * 언어를 늘릴 때는 `Locale` 에 코드를 더하고 아래 표에 한 줄을 더한다.
 * DB 쪽 언어 열도 같은 코드를 쓴다 (SiteServer 의 NormalizeLocale).
 */
export type Locale = 'en' | 'ko';

export const LOCALES: Locale[] = ['ko', 'en'];

export const DEFAULT_LOCALE: Locale = 'ko';

interface Messages {
  nav: {
    home: string;
    about: string;
    news: string;
    downloads: string;
    contact: string;
  };
  hero: {
    eyebrow: string;
    headline: string;
    lead: string;
    cta: string;
  };
  common: {
    readMore: string;
    download: string;
    empty: string;
    loading: string;
    backToList: string;
    langLabel: string;
  };
  contact: {
    title: string;
    lead: string;
    email: string;
    form: {
      name: string;
      company: string;
      emailField: string;
      phone: string;
      category: string;
      subject: string;
      message: string;
      optional: string;
      /** 동의 체크박스 옆 한 줄. 자세한 문구는 DB(`contact.consent`)에서 온다 */
      consent: string;
      consentTitle: string;
      submit: string;
      sending: string;
      done: string;
      failed: string;
      rateLimited: string;
      required: string;
    };
  };
  footer: {
    rights: string;
    portal: string;
  };
}

export const MESSAGES: Record<Locale, Messages> = {
  ko: {
    nav: {
      home: '홈',
      about: '회사소개',
      news: '뉴스',
      downloads: '자료실',
      contact: '문의',
    },
    hero: {
      eyebrow: '관리 시스템을 하나로',
      headline: '흩어진 업무를\n한 곳에서 관리한다',
      lead: '장례식장 · 헬프데스크 · 프로젝트관리를 하나의 인증과 권한으로 잇습니다. 시스템이 늘어나도 관리하는 곳은 하나입니다.',
      cta: '회사소개 보기',
    },
    common: {
      readMore: '자세히',
      download: '내려받기',
      empty: '등록된 내용이 없습니다.',
      loading: '불러오는 중',
      backToList: '목록으로',
      langLabel: '언어',
    },
    contact: {
      title: '문의',
      lead: '제안 · 도입 문의를 남겨 주시면 담당자가 연락드립니다.',
      email: 'contact@jsini.co.kr',
      form: {
        name: '이름',
        company: '회사명',
        emailField: '이메일',
        phone: '연락처',
        category: '분류',
        subject: '제목',
        message: '내용',
        optional: '선택',
        consent: '개인정보 수집·이용에 동의합니다.',
        consentTitle: '개인정보 수집·이용 동의',
        submit: '보내기',
        sending: '보내는 중',
        done: '문의가 접수되었습니다. 담당자가 확인한 뒤 연락드립니다.',
        failed: '접수하지 못했습니다. 잠시 후 다시 시도해 주십시오.',
        rateLimited: '요청이 너무 잦습니다. 잠시 후 다시 시도해 주십시오.',
        required: '필수 항목을 채워 주십시오.',
      },
    },
    footer: {
      rights: 'JSINI. All rights reserved.',
      portal: '관리 포털',
    },
  },
  en: {
    nav: {
      home: 'Home',
      about: 'Company',
      news: 'News',
      downloads: 'Resources',
      contact: 'Contact',
    },
    hero: {
      eyebrow: 'One place to manage',
      headline: 'Scattered operations,\nmanaged from one place',
      lead: 'Funeral halls, help desk and project management joined under one identity and one permission model. However many systems you add, there is still one place to manage them.',
      cta: 'About the company',
    },
    common: {
      readMore: 'Read more',
      download: 'Download',
      empty: 'Nothing here yet.',
      loading: 'Loading',
      backToList: 'Back to list',
      langLabel: 'Language',
    },
    contact: {
      title: 'Contact',
      lead: 'Leave a note and we will get back to you.',
      email: 'contact@jsini.co.kr',
      form: {
        name: 'Name',
        company: 'Company',
        emailField: 'Email',
        phone: 'Phone',
        category: 'Topic',
        subject: 'Subject',
        message: 'Message',
        optional: 'optional',
        consent: 'I consent to the collection and use of my personal data.',
        consentTitle: 'Consent to collection and use of personal data',
        submit: 'Send',
        sending: 'Sending',
        done: 'Your enquiry has been received. We will be in touch.',
        failed: 'We could not accept it. Please try again in a moment.',
        rateLimited: 'Too many requests. Please try again in a moment.',
        required: 'Please fill in the required fields.',
      },
    },
    footer: {
      rights: 'JSINI. All rights reserved.',
      portal: 'Admin portal',
    },
  },
};

/** 바깥에서 들어온 문자열을 아는 값으로만 좁힌다. */
export function normalizeLocale(value?: string): Locale {
  return value === 'en' ? 'en' : DEFAULT_LOCALE;
}
