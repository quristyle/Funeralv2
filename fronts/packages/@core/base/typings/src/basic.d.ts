interface BasicOption {
  label: string;
  value: string;
}

type SelectOption = BasicOption;

type TabOption = BasicOption;

interface BasicUserInfo {
  /**
   * 아바타
   */
  avatar: string;
  /**
   * 사용자 닉네임
   */
  realName: string;
  /**
   * 사용자 역할
   */
  roles?: string[];
  /**
   * 사용자 ID
   */
  userId: string;
  /**
   * 사용자 이름
   */
  username: string;
}

type ClassType =
  | Array<ClassType>
  | boolean
  | null
  | object
  | string
  | undefined;

export type { BasicOption, BasicUserInfo, ClassType, SelectOption, TabOption };
