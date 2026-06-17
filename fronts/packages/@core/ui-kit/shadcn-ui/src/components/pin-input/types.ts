interface PinInputProps {
  class?: any;
  /**
   * 인증번호 길이
   */
  codeLength?: number;
  /**
   * 인증번호 전송 버튼 텍스트
   */
  createText?: (countdown: number) => string;
  /**
   * 비활성화 여부
   */
  disabled?: boolean;
  /**
   * 사용자 정의 인증번호 전송 로직
   * @returns
   */
  handleSendCode?: () => Promise<void>;
  /**
   * 인증번호 전송 버튼 로딩
   */
  loading?: boolean;
  /**
   * 최대 재시도 시간
   */
  maxTime?: number;
}

export type { PinInputProps };
