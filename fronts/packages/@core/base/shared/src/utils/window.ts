interface OpenWindowOptions {
  noopener?: boolean;
  noreferrer?: boolean;
  target?: '_blank' | '_parent' | '_self' | '_top' | string;
}

/**
 * 새 창에서 URL 열기.
 *
 * @param url - 열고자 하는 웹사이트 주소.
 * @param options - 창 열기 옵션.
 */
function openWindow(url: string, options: OpenWindowOptions = {}): void {
  // 구조 분해 및 기본값 설정
  const { noopener = true, noreferrer = true, target = '_blank' } = options;

  // 옵션을 기반으로 속성 문자열 생성
  const features = [noopener && 'noopener=yes', noreferrer && 'noreferrer=yes']
    .filter(Boolean)
    .join(',');

  // 창 열기
  window.open(url, target, features);
}

/**
 * 새 창에서 라우트 열기.
 * @param path
 */
function openRouteInNewWindow(path: string) {
  const { hash, origin } = location;
  const fullPath = path.startsWith('/') ? path : `/${path}`;
  const url = `${origin}${hash && !fullPath.startsWith('/#') ? '/#' : ''}${fullPath}`;
  openWindow(url, { target: '_blank' });
}

export { openRouteInNewWindow, openWindow };
