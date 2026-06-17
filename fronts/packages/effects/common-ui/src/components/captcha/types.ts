import type { CSSProperties } from 'vue';

import type { ClassType } from '@vben/types';

export interface CaptchaData {
  /**
   * x
   */
  x: number;
  /**
   * y
   */
  y: number;
  /**
   * 타임스탬프
   */
  t: number;
}
export interface CaptchaPoint extends CaptchaData {
  /**
   * 데이터 인덱스
   */
  i: number;
}
export interface PointSelectionCaptchaCardProps {
  /**
   * 인증 코드 이미지
   */
  captchaImage: string;
  /**
   * 인증 코드 이미지 높이
   * @default '220px'
   */
  height?: number | string;
  /**
   * 수평 패딩
   * @default '12px'
   */
  paddingX?: number | string;
  /**
   * 수직 패딩
   * @default '16px'
   */
  paddingY?: number | string;
  /**
   * 제목
   * @default '이미지를 순서대로 클릭하세요'
   */
  title?: string;
  /**
   * 인증 코드 이미지 너비
   * @default '300px'
   */
  width?: number | string;
}

export interface PointSelectionCaptchaProps extends PointSelectionCaptchaCardProps {
  /**
   * 확인 버튼 표시 여부
   * @default false
   */
  showConfirm?: boolean;
  /**
   * 힌트 이미지
   * @default ''
   */
  hintImage?: string;
  /**
   * 힌트 텍스트
   * @default ''
   */
  hintText?: string;
}

export interface SliderCaptchaProps {
  class?: ClassType;
  /**
   * @description 슬라이더 스타일
   * @default {}
   */
  actionStyle?: CSSProperties;

  /**
   * @description 슬라이더 바 스타일
   * @default {}
   */
  barStyle?: CSSProperties;

  /**
   * @description 콘텐츠 스타일
   * @default {}
   */
  contentStyle?: CSSProperties;

  /**
   * @description 컴포넌트 스타일
   * @default {}
   */
  wrapperStyle?: CSSProperties;

  /**
   * @description 슬롯 사용 여부, 연동 컴포넌트용 (회전 인증 컴포넌트 참고)
   * @default false
   */
  isSlot?: boolean;

  /**
   * @description 인증 성공 메시지
   * @default '인증 통과'
   */
  successText?: string;

  /**
   * @description 안내 문구
   * @default '슬라이더를 밀어서 드래그하세요'
   */
  text?: string;
}

export interface SliderRotateCaptchaProps {
  /**
   * @description 회전 각도 오차
   * @default 20
   */
  diffDegree?: number;

  /**
   * @description 이미지 크기
   * @default 260
   */
  imageSize?: number;

  /**
   * @description 이미지 스타일
   * @default {}
   */
  imageWrapperStyle?: CSSProperties;

  /**
   * @description 최대 회전 각도
   * @default 270
   */
  maxDegree?: number;

  /**
   * @description 최소 회전 각도
   * @default 90
   */
  minDegree?: number;

  /**
   * @description 이미지 경로
   */
  src?: string;
  /**
   * @description 기본 안내 문구
   */
  defaultTip?: string;
}

export interface SliderTranslateCaptchaProps {
  /**
   * @description 퍼즐 너비
   * @default 420
   */
  canvasWidth?: number;
  /**
   * @description 퍼즐 높이
   * @default 280
   */
  canvasHeight?: number;
  /**
   * @description 조각의 사각형 길이
   * @default 42
   */
  squareLength?: number;
  /**
   * @description 조각의 원형 반지름
   * @default 10
   */
  circleRadius?: number;
  /**
   * @description 이미지 경로
   */
  src?: string;
  /**
   * @description 허용 최대 오차
   * @default 3
   */
  diffDistance?: number;
  /**
   * @description 기본 안내 문구
   */
  defaultTip?: string;
}

export interface CaptchaVerifyPassingData {
  isPassing: boolean;
  time: number | string;
}

export interface SliderCaptchaActionType {
  resume: () => void;
}

export interface SliderRotateVerifyPassingData {
  event: MouseEvent | TouchEvent;
  moveDistance: number;
  moveX: number;
}
