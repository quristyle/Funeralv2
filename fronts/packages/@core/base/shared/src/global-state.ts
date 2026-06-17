/**
 * 전역적으로 재사용되는 변수, 컴포넌트, 설정으로 각 모듈 간에 공유됨
 * 싱글톤 패턴으로 구현되었으며, 사용자 정보와 같이 요청에 따라 달라지는 정보는 요청의 영향을 받지 않도록 주의해야 합니다. 향후 SSR 요구사항이 생겨도 영향을 주지 않습니다.
 */

interface ComponentsState {
  [key: string]: any;
}

interface MessageState {
  copyPreferencesSuccess?: (title: string, content?: string) => void;
}

export interface IGlobalSharedState {
  components: ComponentsState;
  message: MessageState;
}

class GlobalShareState {
  #components: ComponentsState = {};
  #message: MessageState = {};

  /**
   * 프레임워크 내부의 각 시나리오별 메시지 프롬프트 정의
   */
  public defineMessage({ copyPreferencesSuccess }: MessageState) {
    this.#message = {
      copyPreferencesSuccess,
    };
  }

  public getComponents(): ComponentsState {
    return this.#components;
  }

  public getMessage(): MessageState {
    return this.#message;
  }

  public setComponents(value: ComponentsState) {
    this.#components = value;
  }
}

export const globalShareState = new GlobalShareState();

