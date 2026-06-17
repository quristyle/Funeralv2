/**
 * CSS 변수 업데이트 함수
 * @param variables 업데이트할 CSS 변수와 새 값의 매핑
 */
function updateCSSVariables(
  variables: { [key: string]: string },
  id = '__vben-styles__',
): void {
  // 인라인 스타일시트 엘리먼트를 가져오거나 생성
  const styleElement =
    document.querySelector(`#${id}`) || document.createElement('style');

  styleElement.id = id;

  // 업데이트할 CSS 변수의 스타일 텍스트 빌드
  let cssText = ':root {';
  for (const key in variables) {
    if (Object.prototype.hasOwnProperty.call(variables, key)) {
      cssText += `${key}: ${variables[key]};`;
    }
  }
  cssText += '}';

  // 스타일 텍스트를 인라인 스타일시트에 할당
  styleElement.textContent = cssText;

  // 인라인 스타일시트를 문서 헤드에 추가
  if (!document.querySelector(`#${id}`)) {
    setTimeout(() => {
      document.head.append(styleElement);
    });
  }
}

export { updateCSSVariables };
