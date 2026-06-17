import type { PageProps } from '../page/types';

export interface ColPageProps extends PageProps {
  /**
   * 왼쪽 너비
   * @default 30
   */
  leftWidth?: number;
  leftMinWidth?: number;
  leftMaxWidth?: number;
  leftCollapsedWidth?: number;
  leftCollapsible?: boolean;
  /**
   * 오른쪽 너비
   * @default 70
   */
  rightWidth?: number;
  rightMinWidth?: number;
  rightCollapsedWidth?: number;
  rightMaxWidth?: number;
  rightCollapsible?: boolean;

  resizable?: boolean;
  splitLine?: boolean;
  splitHandle?: boolean;
}
