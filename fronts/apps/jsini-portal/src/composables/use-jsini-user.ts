import { computed } from 'vue';

import { useUserStore } from '@vben/stores';

/**
 * 로그인한 JSini 포털 계정.
 *
 * 헬프데스크·프로젝트관리처럼 이식해 온 화면들은 원래 각자의 사용자 테이블을 보고
 * "지금 로그인한 사람" 을 그렸다. 계정을 포털로 단일화한 뒤로 그 출처는 하나다 —
 * `GET /auth/user/info` 로 받아 `useUserStore` 에 담긴 이 값이다.
 *
 * 각 MSA 의 내부 사용자 ID 는 기존 데이터(요청 작성자·담당자 등)를 가리킬 때만 쓴다.
 *
 * 서버 응답(`UserInfoDto`)이 프론트 기본 타입(`BasicUserInfo`)보다 필드가 많아
 * 화면마다 `as any` 로 꺼내 쓰고 있었다. 그 캐스팅을 여기 한 곳으로 모은다.
 */
export function useJsiniUser() {
  const userStore = useUserStore();

  const raw = computed<Record<string, any>>(
    () => (userStore.userInfo as any) ?? {},
  );

  return {
    /** 원본 응답. 위에 없는 필드가 필요할 때만 쓴다. */
    raw,

    /** 로그인 아이디 (`scom.accounts.user_id`). 게이트웨이가 `X-User-Id` 로 보내는 값과 같다. */
    loginId: computed<string>(
      () => raw.value.username ?? raw.value.userId ?? '',
    ),
    /** 계정 고유 키 (`scom.accounts.id`) */
    accountId: computed<string>(() => raw.value.id ?? ''),
    /** 표시 이름 */
    userName: computed<string>(
      () => raw.value.realName ?? raw.value.username ?? '',
    ),
    email: computed<string>(() => raw.value.email ?? ''),
    phone: computed<string>(() => raw.value.phone ?? ''),
    companyName: computed<string>(() => raw.value.companyName ?? ''),
    deptName: computed<string>(() => raw.value.deptName ?? ''),
    avatar: computed<string>(() => raw.value.avatar ?? ''),
    /** 배정된 역할 식별자 (`ADMINISTRATOR` 등) */
    roles: computed<string[]>(() => raw.value.roles ?? []),

    /** 사용자 정보를 받아 왔는지 */
    isLoaded: computed<boolean>(() => Boolean(raw.value.username)),
  };
}
