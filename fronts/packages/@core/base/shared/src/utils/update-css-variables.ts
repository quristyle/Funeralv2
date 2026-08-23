/**
 * 更新 CSS 变量的函数
 * @param variables 要更新的 CSS 变量与其新值的映射
 * @param id 内联样式表的 id，便于复用与覆盖
 * @param selector CSS 变量挂载的选择器，默认 `:root`。
 *  对于像 TDesign 这种将变量定义在 `:root[theme-mode='dark']` 等更高优先级选择器下的组件库，
 *  需要传入相同（或更高）优先级的选择器才能正确覆盖。
 */
function updateCSSVariables(
  variables: { [key: string]: string },
  id = '__vben-styles__',
  selector = ':root',
): void {
  // 인라인 스타일시트 엘리먼트를 가져오거나 생성
  const styleElement =
    document.querySelector(`#${id}`) || document.createElement('style');

  styleElement.id = id;

  // 构建要更新的 CSS 变量的样式文本
  let cssText = `${selector} {`;
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
