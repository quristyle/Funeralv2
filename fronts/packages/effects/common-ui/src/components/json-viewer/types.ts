export interface JsonViewerProps {
  /** 표시할 구조적 데이터 */
  value: any;
  /** 확장 깊이 */
  expandDepth?: number;
  /** 복사 가능 여부 */
  copyable?: boolean;
  /** 정렬 여부 */
  sort?: boolean;
  /** 테두리 표시 */
  boxed?: boolean;
  /** 테마 */
  theme?: string;
  /** 확장 여부 */
  expanded?: boolean;
  /** 시간 포맷팅 함수 */
  timeformat?: (time: Date | number | string) => string;
  /** 미리보기 모드 */
  previewMode?: boolean;
  /** 배열 인덱스 표시 */
  showArrayIndex?: boolean;
  /** 큰따옴표 표시 */
  showDoubleQuotes?: boolean;
}

export interface JsonViewerAction {
  action: string;
  text: string;
  trigger: HTMLElement;
}

export interface JsonViewerValue {
  value: any;
  path: string;
  depth: number;
  el: HTMLElement;
}

export interface JsonViewerToggle {
  /** 마우스 이벤트 */
  event: MouseEvent;
  /** 현재 확장 상태 */
  open: boolean;
}
