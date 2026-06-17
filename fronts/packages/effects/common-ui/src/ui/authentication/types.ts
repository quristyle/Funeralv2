interface AuthenticationProps {
  /**
   * @ko_KR 인증번호 로그인 경로
   */
  codeLoginPath?: string;
  /**
   * @ko_KR 비밀번호 찾기 경로
   */
  forgetPasswordPath?: string;

  /**
   * @ko_KR 로딩 처리 상태 여부
   */
  loading?: boolean;

  /**
   * @ko_KR QR 코드 로그인 경로
   */
  qrCodeLoginPath?: string;

  /**
   * @ko_KR 회원가입 경로
   */
  registerPath?: string;

  /**
   * @ko_KR 인증번호 로그인 표시 여부
   */
  showCodeLogin?: boolean;
  /**
   * @ko_KR 비밀번호 찾기 표시 여부
   */
  showForgetPassword?: boolean;

  /**
   * @ko_KR QR 코드 로그인 표시 여부
   */
  showQrcodeLogin?: boolean;

  /**
   * @ko_KR 회원가입 버튼 표시 여부
   */
  showRegister?: boolean;

  /**
   * @ko_KR 계정 저장 표시 여부
   */
  showRememberMe?: boolean;

  /**
   * @ko_KR 소셜 로그인 표시 여부
   */
  showThirdPartyLogin?: boolean;

  /**
   * @ko_KR 로그인 창 부제목
   */
  subTitle?: string;

  /**
   * @ko_KR 로그인 창 제목
   */
  title?: string;
  /**
   * @ko_KR 제출 버튼 텍스트
   */
  submitButtonText?: string;
}

export type { AuthenticationProps };
