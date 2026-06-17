import { expect, it } from 'vitest';

import { updateCSSVariables } from '../update-css-variables';

it('updateCSSVariables should update CSS variables in :root selector', () => {
  // 초기 인라인 스타일시트 내용 시뮬레이션
  const initialStyleContent = ':root { --primaryColor: red; }';
  document.head.innerHTML = `<style id="custom-styles">${initialStyleContent}</style>`;

  // 업데이트할 CSS 변수와 새로운 값
  const updatedVariables = {
    fontSize: '16px',
    primaryColor: 'blue',
    secondaryColor: 'green',
  };

  // CSS 변수를 업데이트하기 위해 함수 호출
  updateCSSVariables(updatedVariables, 'custom-styles');

  // 업데이트된 스타일 내용 가져오기
  const styleElement = document.querySelector('#custom-styles');
  const updatedStyleContent = styleElement ? styleElement.textContent : '';

  // 업데이트된 스타일 내용에 올바른 업데이트 값이 포함되어 있는지 확인
  expect(
    updatedStyleContent?.includes('primaryColor: blue;') &&
      updatedStyleContent?.includes('secondaryColor: green;') &&
      updatedStyleContent?.includes('fontSize: 16px;'),
  ).toBe(true);
});
