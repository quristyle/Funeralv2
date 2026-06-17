import type { IconifyIconStructure } from '@vben-core/icons';

import { addIcon } from '@vben-core/icons';

loadSvgIcons();

function parseSvg(svgData: string): IconifyIconStructure {
  const parser = new DOMParser();
  const xmlDoc = parser.parseFromString(svgData, 'image/svg+xml');
  const svgElement = xmlDoc.documentElement;

  // SVG 루트 요소의 핵심 스타일 속성 추출
  const getAttrs = (el: Element, attrs: string[]) =>
    attrs
      .map((attr) =>
        el.hasAttribute(attr) ? `${attr}="${el.getAttribute(attr)}"` : '',
      )
      .filter(Boolean)
      .join(' ');

  const rootAttrs = getAttrs(svgElement, [
    'fill',
    'stroke',
    'fill-rule',
    'stroke-width',
  ]);

  const svgContent = [...svgElement.childNodes]
    .filter((node) => node.nodeType === Node.ELEMENT_NODE)
    .map((node) => new XMLSerializer().serializeToString(node))
    .join('');
  // 루트에 속성이 있는 경우, g 태그로 내용을 감싸고 속성을 상속받음
  const body = rootAttrs ? `<g ${rootAttrs}>${svgContent}</g>` : svgContent;

  const viewBoxValue = svgElement.getAttribute('viewBox') || '';
  const [left, top, width, height] = viewBoxValue.split(' ').map((val) => {
    const num = Number(val);
    return Number.isNaN(num) ? undefined : num;
  });

  return {
    body,
    height,
    left,
    top,
    width,
  };
}

/**
 * 사용자 정의 SVG 이미지를 컴포넌트로 변환
 * @example ./svg/avatar.svg
 * <Icon icon="svg:avatar"></Icon>
 */
async function loadSvgIcons() {
  const svgEagers = import.meta.glob('./icons/**', {
    eager: true,
    query: '?raw',
  });

  await Promise.all(
    Object.entries(svgEagers).map((svg) => {
      const [key, body] = svg as [string, string | { default: string }];

      // ./icons/xxxx.svg => xxxxx (아이콘 이름 추출)
      const start = key.lastIndexOf('/') + 1;
      const end = key.lastIndexOf('.');
      const iconName = key.slice(start, end);

      return addIcon(`svg:${iconName}`, {
        ...parseSvg(typeof body === 'object' ? body.default : body),
      });
    }),
  );
}
