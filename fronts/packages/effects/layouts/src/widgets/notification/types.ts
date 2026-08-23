interface NotificationItem {
  id: number | string;
  avatar: string;
  date: string;
  isRead?: boolean;
  message: string;
  title: string;
  /**
   * 이동 링크: 라우트 경로 또는 전체 URL
   * @example '/dashboard' 또는 'https://example.com'
   */
  link?: string;
  query?: Record<string, any>;
  state?: Record<string, any>;
  /** 业务字段 */
  [key: string]: any;
}

export type { NotificationItem };
