// `@tiptap/core` 를 직접 의존성에 넣지 않는다 — `@tiptap/vue-3` 가 그대로 다시 내보낸다
// (`export * from '@tiptap/core'`). 버전이 갈라질 일이 없어 이쪽이 안전하다.
import { mergeAttributes, Node } from '@tiptap/vue-3';

/**
 * [영상 넣기 노드]
 *
 * tiptap 은 자기 스키마에 없는 태그를 지운다. `<iframe>` 도 그중 하나여서,
 * YouTube 삽입 코드를 붙여 넣으면 아무것도 남지 않는다.
 * 이 노드가 있어야 편집기가 iframe 을 하나의 덩어리로 알아보고
 * 저장·수정 사이를 오갈 때 그대로 살아남는다.
 *
 * `atom` 이라 안쪽에 커서가 들어가지 않고 통째로 선택된다 —
 * 영상 위에 글자를 쓰다가 태그가 깨지는 일이 없다.
 *
 * **어떤 주소를 허용할지는 이 노드가 정하지 않는다.** 서버(`RichTextSanitizer`)가
 * 영상 서비스 목록으로 걸러 낸다. 화면에서 미리 걸러 주는 것은 안내를 위한 것이고,
 * 실제 판정은 서버 한 곳에서 한다.
 */
export const VideoEmbed = Node.create({
  name: 'videoEmbed',
  group: 'block',
  atom: true,
  draggable: true,
  selectable: true,

  addAttributes() {
    return {
      src: { default: null },
      width: { default: null },
      height: { default: null },
      title: { default: null },
      style: { default: null },
      // 아래 셋은 YouTube 삽입 코드가 늘 달고 오는 것들이다.
      // 기본값을 두면 주소만 넣어도 같은 모양이 된다.
      frameborder: { default: '0' },
      allow: {
        default:
          'accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share',
      },
      allowfullscreen: { default: '' },
      referrerpolicy: { default: 'strict-origin-when-cross-origin' },
    };
  },

  parseHTML() {
    return [{ tag: 'iframe[src]' }];
  },

  renderHTML({ HTMLAttributes }) {
    return ['iframe', mergeAttributes(HTMLAttributes)];
  },
});

// ============================================================
// 주소 알아보기
// ============================================================

/** 영상 서비스 구분 */
export type VideoService = 'kakaotv' | 'navertv' | 'other' | 'vimeo' | 'youtube';

/** 화면에 보여 줄 서비스 이름 */
export const SERVICE_LABEL: Record<VideoService, string> = {
  kakaotv: '카카오TV',
  navertv: '네이버TV',
  other: '영상',
  vimeo: 'Vimeo',
  youtube: 'YouTube',
};

/** 넣을 수 있는 서비스 목록. 안내 문구에 쓴다. */
export const SUPPORTED_LABEL = 'YouTube · Vimeo · 네이버TV · 카카오TV';

/**
 * 영상 넣기를 허용하는 호스트.
 * 서버(`RichTextSanitizer.EmbedHosts`)와 같은 목록이다 — **한쪽만 고치지 않는다.**
 */
const EMBED_HOSTS = new Set([
  'www.youtube.com',
  'youtube.com',
  'www.youtube-nocookie.com',
  'youtube-nocookie.com',
  'youtu.be',
  'player.vimeo.com',
  'tv.naver.com',
  'play-tv.kakao.com',
]);

/** YouTube 재생 옵션 */
export interface PlaybackOptions {
  /** 자동 재생. 브라우저가 소리 있는 자동 재생을 막으므로 mute 와 함께 써야 한다. */
  autoplay: boolean;
  /** 컨트롤 숨기기 */
  hideControls: boolean;
  /** 반복 재생 */
  loop: boolean;
  /** 소리 끔 */
  mute: boolean;
  /** 시작 시간(초). 0 이면 넣지 않는다. */
  start: number;
}

export const DEFAULT_PLAYBACK: PlaybackOptions = {
  autoplay: false,
  hideControls: false,
  loop: false,
  mute: false,
  start: 0,
};

/** 알아본 영상 */
export interface ParsedVideo {
  /** 삽입 코드에서 읽은 높이 */
  height?: null | string;
  /** 삽입 코드를 통째로 받은 것인지 (크기를 그대로 살릴지 판단한다) */
  fromEmbedCode: boolean;
  /** 삽입 코드에서 읽은 재생 옵션 (YouTube 만) */
  playback: PlaybackOptions;
  service: VideoService;
  /** 임베드 주소 */
  src: string;
  /** 삽입 코드에서 읽은 style */
  style?: null | string;
  title?: null | string;
  /** YouTube 영상 아이디. 옵션을 다시 붙일 때 쓴다. */
  videoId?: string;
  /** 삽입 코드에서 읽은 너비 */
  width?: null | string;
}

/**
 * 사용자가 붙여 넣은 것을 알아본다.
 *
 * 세 가지 형태를 모두 받는다.
 *   1. 삽입 코드 통째로  `<iframe width="816" ... src="...embed/ID..."></iframe>`
 *   2. 보던 주소         `https://www.youtube.com/watch?v=ID`  ·  `https://youtu.be/ID`
 *   3. 임베드 주소       `https://www.youtube.com/embed/ID`
 *
 * 알아볼 수 없거나 허용 목록 밖이면 null 을 돌려준다.
 */
export function parseVideoInput(raw: string): null | ParsedVideo {
  const input = raw.trim();
  if (!input) return null;

  // 1. 삽입 코드 — 크기·제목·재생 옵션까지 그대로 읽는다.
  //    DOMParser 는 문서를 만들지 않고 파싱만 하므로 안쪽 코드가 실행되지 않는다.
  if (/<iframe/i.test(input)) {
    const el = new DOMParser()
      .parseFromString(input, 'text/html')
      .querySelector('iframe');

    const rawSrc = el?.getAttribute('src')?.trim();
    if (!rawSrc) return null;

    const parsed = readUrl(rawSrc);
    if (!parsed) return null;

    return {
      ...parsed,
      fromEmbedCode: true,
      height: el?.getAttribute('height') ?? null,
      style: el?.getAttribute('style') ?? null,
      title: el?.getAttribute('title') ?? null,
      width: el?.getAttribute('width') ?? null,
    };
  }

  // 2·3. 주소 하나
  const parsed = readUrl(input);
  return parsed ? { ...parsed, fromEmbedCode: false } : null;
}

/**
 * 주소를 임베드 주소로 바꾸고 서비스·영상 아이디·재생 옵션을 읽는다.
 */
function readUrl(
  input: string,
): null | Omit<ParsedVideo, 'fromEmbedCode'> {
  let url: URL;
  try {
    url = new URL(input.startsWith('http') ? input : `https://${input}`);
  } catch {
    return null;
  }

  if (url.protocol !== 'https:' && url.protocol !== 'http:') return null;
  if (!EMBED_HOSTS.has(url.host)) return null;

  const host = url.host;
  const path = url.pathname;

  // ── YouTube ─────────────────────────────────────────────
  if (
    host === 'youtu.be' ||
    host.endsWith('youtube.com') ||
    host.endsWith('youtube-nocookie.com')
  ) {
    const videoId = readYoutubeId(host, path, url);
    if (!videoId) return null;

    return {
      playback: readPlayback(url),
      service: 'youtube',
      src: buildYoutubeSrc(videoId, readPlayback(url)),
      videoId,
    };
  }

  // ── 그 밖 ───────────────────────────────────────────────
  // 임베드 주소를 그대로 받는다. 재생 옵션은 서비스마다 달라서 손대지 않는다.
  return {
    playback: { ...DEFAULT_PLAYBACK },
    service: readService(host),
    src: url.toString(),
  };
}

function readService(host: string): VideoService {
  if (host === 'player.vimeo.com') return 'vimeo';
  if (host === 'tv.naver.com') return 'navertv';
  if (host === 'play-tv.kakao.com') return 'kakaotv';
  return 'other';
}

/** YouTube 주소에서 영상 아이디를 뽑는다. */
function readYoutubeId(host: string, path: string, url: URL): null | string {
  // youtu.be/ID
  if (host === 'youtu.be') return path.slice(1).split('/')[0] || null;

  // /embed/ID  ·  /shorts/ID  ·  /live/ID
  const match = /^\/(?:embed|shorts|live)\/([^/?]+)/.exec(path);
  if (match) return match[1] ?? null;

  // /watch?v=ID
  return url.searchParams.get('v');
}

/** 임베드 주소에 붙어 있던 재생 옵션을 읽는다. */
function readPlayback(url: URL): PlaybackOptions {
  const q = url.searchParams;
  const on = (key: string) => q.get(key) === '1';

  return {
    autoplay: on('autoplay'),
    hideControls: q.get('controls') === '0',
    loop: on('loop'),
    mute: on('mute'),
    start: Number.parseInt(q.get('start') ?? '0', 10) || 0,
  };
}

/**
 * YouTube 임베드 주소를 만든다.
 */
export function buildYoutubeSrc(
  videoId: string,
  options: PlaybackOptions,
): string {
  const q = new URLSearchParams();

  if (options.autoplay) q.set('autoplay', '1');
  // 소리가 있는 자동 재생은 브라우저가 막는다. 자동 재생을 켜면 소리도 끈다.
  if (options.mute || options.autoplay) q.set('mute', '1');
  if (options.hideControls) q.set('controls', '0');
  if (options.start > 0) q.set('start', String(options.start));

  if (options.loop) {
    q.set('loop', '1');
    // YouTube 는 한 편만 반복할 때 playlist 에 같은 아이디를 넣어야 동작한다.
    q.set('playlist', videoId);
  }

  const query = q.toString();
  return `https://www.youtube.com/embed/${videoId}${query ? `?${query}` : ''}`;
}

/** 삽입할 iframe 의 속성 */
export interface EmbedAttrs {
  height?: null | string;
  src: string;
  style?: null | string;
  title?: null | string;
  width?: null | string;
}

/** 화면 폭에 맞추는 크기. 좁은 화면에서도 넘치지 않고 16:9 가 유지된다. */
export const RESPONSIVE_STYLE =
  'width:100%;max-width:816px;aspect-ratio:16/9;height:auto';

/**
 * 알아본 영상을 삽입용 속성으로 바꾼다.
 *
 * @param size `responsive` 는 화면 폭에 맞춘다. `fixed` 는 준 크기를 그대로 쓴다.
 */
export function toEmbedAttrs(
  video: ParsedVideo,
  size: { height?: number | string; mode: 'fixed' | 'responsive'; width?: number | string },
): EmbedAttrs {
  const title = video.title || SERVICE_LABEL[video.service];

  if (size.mode === 'responsive') {
    return { src: video.src, style: RESPONSIVE_STYLE, title };
  }

  return {
    height: size.height ? String(size.height) : null,
    src: video.src,
    // 크기를 직접 지정했으면 넘치는 것만 막는다.
    style: 'max-width:100%',
    title,
    width: size.width ? String(size.width) : null,
  };
}
