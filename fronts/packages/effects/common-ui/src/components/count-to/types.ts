import type { CubicBezierPoints, EasingFunction } from '@vueuse/core';

import type { StyleValue } from 'vue';

import { TransitionPresets as TransitionPresetsData } from '@vueuse/core';

export type TransitionPresets = keyof typeof TransitionPresetsData;

export const TransitionPresetsKeys = Object.keys(
  TransitionPresetsData,
) as TransitionPresets[];

export interface CountToProps {
  /** 시작 값 */
  startVal?: number;
  /** 최종 값 */
  endVal: number;
  /** 애니메이션 비활성화 여부 */
  disabled?: boolean;
  /** 애니메이션 시작 지연 시간 */
  delay?: number;
  /** 지속 시간  */
  duration?: number;
  /** 소수점 자리수  */
  decimals?: number;
  /** 소수점 기호  */
  decimal?: string;
  /** 구분자  */
  separator?: string;
  /** 접두사  */
  prefix?: string;
  /** 접미사  */
  suffix?: string;
  /** 전환 효과  */
  transition?: CubicBezierPoints | EasingFunction | TransitionPresets;
  /** 정수 부분 클래스명 */
  mainClass?: string;
  /** 소수 부분 클래스명 */
  decimalClass?: string;
  /** 접두사 부분 클래스명 */
  prefixClass?: string;
  /** 접미사 부분 클래스명 */
  suffixClass?: string;

  /** 정수 부분 스타일 */
  mainStyle?: StyleValue;
  /** 소수 부분 스타일 */
  decimalStyle?: StyleValue;
  /** 접두사 부분 스타일 */
  prefixStyle?: StyleValue;
  /** 접미사 부분 스타일 */
  suffixStyle?: StyleValue;
}
