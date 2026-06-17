interface FallbackProps {
  /**
   * 설명
   */
  description?: string;
  /**
   *  @ko_KR 홈 라우트 주소
   *  @default /
   */
  homePath?: string;
  /**
   * @ko_KR 기본 표시 이미지
   * @default pageNotFoundSvg
   */
  image?: string;
  /**
   *  @ko_KR 내장 타입
   */
  status?: '403' | '404' | '500' | 'coming-soon' | 'offline';
  /**
   *  @ko_KR 페이지 안내 문구
   */
  title?: string;
}
export type { FallbackProps };
