export interface VisibleDomRect {
  bottom: number;
  height: number;
  left: number;
  right: number;
  top: number;
  width: number;
}

/**
 * 엘리먼트의 가시성 정보 가져오기
 * @param element
 */
export function getElementVisibleRect(
  element?: HTMLElement | null | undefined,
): VisibleDomRect {
  if (!element) {
    return {
      bottom: 0,
      height: 0,
      left: 0,
      right: 0,
      top: 0,
      width: 0,
    };
  }
  const rect = element.getBoundingClientRect();
  const viewHeight = Math.max(
    document.documentElement.clientHeight,
    window.innerHeight,
  );

  const top = Math.max(rect.top, 0);
  const bottom = Math.min(rect.bottom, viewHeight);

  const viewWidth = Math.max(
    document.documentElement.clientWidth,
    window.innerWidth,
  );

  const left = Math.max(rect.left, 0);
  const right = Math.min(rect.right, viewWidth);

  // 엘리먼트가 완전히 보이지 않으면 빈 사각형 반환
  if (top >= viewHeight || bottom <= 0 || left >= viewWidth || right <= 0) {
    return {
      bottom: 0,
      height: 0,
      left: 0,
      right: 0,
      top: 0,
      width: 0,
    };
  }

  return {
    bottom,
    height: Math.max(0, bottom - top),
    left,
    right,
    top,
    width: Math.max(0, right - left),
  };
}

export function getScrollbarWidth() {
  const scrollDiv = document.createElement('div');

  scrollDiv.style.visibility = 'hidden';
  scrollDiv.style.overflow = 'scroll';
  scrollDiv.style.position = 'absolute';
  scrollDiv.style.top = '-9999px';

  document.body.append(scrollDiv);

  const innerDiv = document.createElement('div');
  scrollDiv.append(innerDiv);

  const scrollbarWidth = scrollDiv.offsetWidth - innerDiv.offsetWidth;

  scrollDiv.remove();
  return scrollbarWidth;
}

export function needsScrollbar() {
  const doc = document.documentElement;
  const body = document.body;

  // body의 overflow-y 스타일 확인
  const overflowY = window.getComputedStyle(body).overflowY;

  // 스크롤바가 필요한 스타일이 명시적으로 설정된 경우
  if (overflowY === 'scroll' || overflowY === 'auto') {
    return doc.scrollHeight > window.innerHeight;
  }

  // 그 외의 경우, scrollHeight와 innerHeight를 비교하여 판단
  return doc.scrollHeight > window.innerHeight;
}

export function triggerWindowResize(): void {
  // 새로운 resize 이벤트 생성
  const resizeEvent = new Event('resize');

  // window의 resize 이벤트 트리거
  window.dispatchEvent(resizeEvent);
}
