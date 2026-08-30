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
    work: string;
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
  /** 구축·운영 사례 페이지. 시스템 목록 자체는 DB(`work.*`)에서 온다 */
  work: {
    title: string;
    lead: string;
    ctaLead: string;
    /**
     * 그림 아래에 늘 붙는 한 줄.
     *
     * 그림은 **실제 화면의 캡처가 아니라 그것을 본뜬 재현 이미지**다. 고객사 시스템의
     * 화면에는 고인·상주·담당자·설비 운전값 같은 것이 들어 있어 공개 사이트에 올릴 수 없다.
     * 보는 사람이 진짜 캡처로 오해하지 않도록 그림마다 이 문장을 붙인다 — 빼지 않는다.
     */
    mockupNote: string;
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
      work: '구축 사례',
      news: '뉴스',
      downloads: '자료실',
      contact: '문의',
    },
    hero: {
      eyebrow: '납품에서 끝나지 않습니다',
      headline: '만들고,\n계속 함께 갑니다',
      lead: '업무 시스템을 만들어 납품하고, 그 뒤로도 유지보수 · 업그레이드 · 보수 관리를 이어 갑니다. 헬프데스크를 직접 운영해 언제든 닿을 수 있게 두었습니다.',
      cta: '구축 사례 보기',
    },
    work: {
      title: '구축 사례',
      lead: '만들어 납품하고 지금도 함께 운영하는 시스템들입니다. 고객사와의 약속에 따라 이름 대신 분야로 적습니다.',
      ctaLead: '쓰고 계신 시스템도\n같은 방식으로 이어받습니다',
      mockupNote: '실제 화면을 본뜬 재현 이미지입니다. 표시된 자료는 모두 가상입니다.',
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
      email: 'quristyle@gmail.com',
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
      work: 'Work',
      news: 'News',
      downloads: 'Resources',
      contact: 'Contact',
    },
    hero: {
      eyebrow: 'Delivery is not the end',
      headline: 'We build it,\nthen we stay',
      lead: 'We build and deliver business systems — then keep them running, maintained and up to date. We run our own help desk so there is always somewhere to reach us.',
      cta: 'See our work',
    },
    work: {
      title: 'Work',
      lead: 'Systems we built, delivered, and still run alongside our clients. Described by field rather than by name, as agreed with them.',
      ctaLead: 'The system you already run\ncan be taken over the same way',
      mockupNote: 'An illustration modelled on the real screen. Every value shown is fictitious.',
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
      email: 'quristyle@gmail.com',
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
