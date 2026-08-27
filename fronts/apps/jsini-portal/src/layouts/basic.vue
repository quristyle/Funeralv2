<script lang="ts" setup>
import type { NotificationItem } from '@vben/layouts';

import { computed, onBeforeMount, ref, watch, provide } from 'vue';
import { useRouter } from 'vue-router';

import { AuthenticationLoginExpiredModal } from '@vben/common-ui';
import { VBEN_DOC_URL, VBEN_GITHUB_URL } from '@vben/constants';
import { useWatermark } from '@vben/hooks';
import { BookOpenText, CircleHelp, SvgGithubIcon } from '@vben/icons';
import {
  BasicLayout,
  LockScreen,
  Notification,
  UserDropdown,
} from '@vben/layouts';
import { preferences } from '@vben/preferences';
import { useAccessStore, useTabbarStore, useUserStore } from '@vben/stores';
import { openWindow } from '@vben/utils';

import { message } from 'ant-design-vue';

// 헤더의 ✨ 아이콘이 켜면 레이아웃이 본문 오른쪽에 그린다. 이 화면은 antd 를 쓰므로
// 프레임워크 패키지가 아니라 앱에 두고 슬롯으로 넣는다.
import AiChatContent from '#/components/ai-chat/ai-chat-content.vue';
import { $t } from '#/locales';
import { refreshAccessMenus } from '#/router/access';
import { useAuthStore } from '#/store';
import { useMenuFavoriteStore } from '#/store/menu-favorite';
import LoginForm from '#/views/_core/authentication/login.vue';

const { setMenuList } = useTabbarStore();
setMenuList([
  'close',
  'affix',
  'maximize',
  'reload',
  'open-in-new-window',
  // 즐겨찾기는 상태에 따라 한 쪽만 나타난다. 두 키를 모두 등록해야 한다
  // (use-tabbar.ts 가 이 목록에 있는 키만 보여준다).
  'favorite-add',
  'favorite-remove',
  'close-left',
  'close-right',
  'close-other',
  'close-all',
]);

const notifications = ref<NotificationItem[]>([
  {
    id: 1,
    avatar: 'https://avatar.vercel.sh/vercel.svg?text=VB',
    date: '3시간 전',
    isRead: true,
    message: '설명 메시지 설명 메시지 설명 메시지',
    title: '14개의 새 주간 보고서를 받았습니다',
  },
  {
    id: 2,
    avatar: 'https://avatar.vercel.sh/1',
    date: '방금 전',
    isRead: false,
    message: '설명 메시지 설명 메시지 설명 메시지',
    title: '주피엔유님이 답글을 남겼습니다',
  },
  {
    id: 3,
    avatar: 'https://avatar.vercel.sh/1',
    date: '2024-01-01',
    isRead: false,
    message: '설명 메시지 설명 메시지 설명 메시지',
    title: '취리리님이 댓글을 남겼습니다',
  },
  {
    id: 4,
    avatar: 'https://avatar.vercel.sh/satori',
    date: '1일 전',
    isRead: false,
    message: '설명 메시지 설명 메시지 설명 메시지',
    title: '할 일 알림',
  },
  {
    id: 5,
    avatar: 'https://avatar.vercel.sh/satori',
    date: '1일 전',
    isRead: false,
    message: '설명 메시지 설명 메시지 설명 메시지',
    title: '워크스페이스 이동 예시',
    link: '/workspace',
  },
  {
    id: 6,
    avatar: 'https://avatar.vercel.sh/satori',
    date: '1일 전',
    isRead: false,
    message: '설명 메시지 설명 메시지 설명 메시지',
    title: '외부 링크 이동 예시',
    link: 'https://doc.vben.pro',
  },
]);

const router = useRouter();

/**
 * 사이드바의 메뉴 리로드 버튼이 쓸 핸들러.
 *
 * 레이아웃(`@vben/layouts`)은 프레임워크 패키지라 앱의 라우트 표나 스토어를
 * 직접 알지 못한다. 그래서 실제 갱신은 앱이 맡고 레이아웃은 주입받아 부른다
 * (위 `AI_CHAT_STREAM_API` 와 같은 방식이다).
 *
 * 주입하지 않으면 레이아웃이 예전 동작(전체 새로고침)으로 물러난다.
 */
provide('MENU_RELOAD_HANDLER', async () => {
  try {
    await refreshAccessMenus(router);
    message.success('메뉴를 다시 읽었습니다.');
  } catch {
    message.error('메뉴를 다시 읽지 못했습니다.');
  }
});

/**
 * 즐겨찾기 창구 — 탭 오른쪽 메뉴가 쓴다.
 *
 * 레이아웃은 프레임워크 패키지라 앱의 API·스토어를 알지 못한다.
 * 위 `MENU_RELOAD_HANDLER` 와 같은 방식으로 배선한다.
 *
 * 담고 나면 사이드바 즐겨찾기 묶음이 곧바로 바뀐다 — 같은 스토어를 보기 때문이다.
 */
const menuFavoriteStore = useMenuFavoriteStore();

provide('TAB_FAVORITE_HANDLER', {
  add: async (path: string) => {
    try {
      await menuFavoriteStore.add(path);
      message.success('즐겨찾기에 추가했습니다.');
    } catch {
      message.error('즐겨찾기에 추가하지 못했습니다.');
    }
  },
  isFavorite: (path: string) => menuFavoriteStore.isFavorite(path),
  remove: async (path: string) => {
    try {
      await menuFavoriteStore.remove(path);
      message.success('즐겨찾기에서 제거했습니다.');
    } catch {
      message.error('즐겨찾기에서 제거하지 못했습니다.');
    }
  },
});

/**
 * 사이드바 즐겨찾기 묶음. 레이아웃이 이 값을 사이드바 맨 위에 얹는다.
 *
 * 라우트가 아니라 즐겨찾기 목록에서 바로 만든다. 제목은 다국어 키일 수 있어
 * 레이아웃이 다른 메뉴와 같은 규칙(`$tIfKey`)으로 번역한다.
 */
provide(
  'SIDEBAR_EXTRA_MENUS',
  computed(() => {
    if (menuFavoriteStore.favorites.length === 0) return [];

    return [
      {
        badgeType: undefined,
        children: menuFavoriteStore.favorites.map((f) => ({
          icon: f.icon ?? 'lucide:star',
          name: f.title || f.name,
          path: f.path,
        })),
        icon: 'lucide:star',
        name: '즐겨찾기',
        // 실제 라우트가 아닌 묶음이다. 눌러도 이동하지 않고 펼침만 한다.
        path: '__favorites__',
      },
    ];
  }),
);

const userStore = useUserStore();
const authStore = useAuthStore();
const accessStore = useAccessStore();
const { destroyWatermark, updateWatermark } = useWatermark();
const showDot = computed(() =>
  notifications.value.some((item) => !item.isRead),
);

const menus = computed(() => [
  {
    handler: () => {
      router.push({ name: 'Profile' });
    },
    icon: 'lucide:user',
    text: $t('page.auth.profile'),
  },
  {
    handler: () => {
      openWindow(VBEN_DOC_URL, {
        target: '_blank',
      });
    },
    icon: BookOpenText,
    text: $t('ui.widgets.document'),
  },
  {
    handler: () => {
      openWindow(VBEN_GITHUB_URL, {
        target: '_blank',
      });
    },
    icon: SvgGithubIcon,
    text: 'GitHub',
  },
  {
    handler: () => {
      openWindow(`${VBEN_GITHUB_URL}/issues`, {
        target: '_blank',
      });
    },
    icon: CircleHelp,
    text: $t('ui.widgets.qa'),
  },
]);

const avatar = computed(() => {
  const rawAvatar = userStore.userInfo?.avatar ?? preferences.app.defaultAvatar;
  if (rawAvatar && rawAvatar.includes('/api/file/download/')) {
    return rawAvatar.replace('/api/file/download/', '/api/file/thumbnail/');
  }
  return rawAvatar;
});

async function handleLogout() {
  await authStore.logout(false);
}

function handleNoticeClear() {
  notifications.value = [];
}

function markRead(id: number | string) {
  const item = notifications.value.find((item) => item.id === id);
  if (item) {
    item.isRead = true;
  }
}

function remove(id: number | string) {
  notifications.value = notifications.value.filter((item) => item.id !== id);
}

function handleMakeAll() {
  notifications.value.forEach((item) => (item.isRead = true));
}

function handleClickLogo() {}

watch(
  () => ({
    enable: preferences.app.watermark,
    content: preferences.app.watermarkContent,
  }),
  async ({ enable, content }) => {
    if (enable) {
      await updateWatermark({
        content:
          content ||
          `${userStore.userInfo?.username} - ${userStore.userInfo?.realName}`,
      });
    } else {
      destroyWatermark();
    }
  },
  {
    immediate: true,
  },
);

onBeforeMount(() => {
  if (preferences.app.watermark) {
    destroyWatermark();
  }

  // 즐겨찾기를 미리 받아 둔다. 탭 오른쪽 메뉴가 '추가/제거' 중 무엇을 보여줄지
  // 판단하려면 목록이 있어야 하고, 사이드바 묶음도 이 값으로 그린다.
  // 실패해도 스토어가 조용히 빈 목록으로 남긴다 — 곁들이는 기능이라 화면을 막지 않는다.
  menuFavoriteStore.load();
});
</script>

<template>
  <BasicLayout
    @clear-preferences-and-logout="handleLogout"
    @click-logo="handleClickLogo"
  >
    <template #user-dropdown>
      <UserDropdown
        :avatar
        :menus
        :text="userStore.userInfo?.realName"
        :description="userStore.userInfo?.email"
        trigger="both"
        @logout="handleLogout"
      />
    </template>
    <template #notification>
      <Notification
        :dot="showDot"
        :notifications="notifications"
        @clear="handleNoticeClear"
        @read="(item) => item.id && markRead(item.id)"
        @remove="(item) => item.id && remove(item.id)"
        @make-all="handleMakeAll"
      />
    </template>
    <template #extra>
      <AuthenticationLoginExpiredModal
        v-model:open="accessStore.loginExpired"
        :avatar
      >
        <LoginForm />
      </AuthenticationLoginExpiredModal>
    </template>
    <template #lock-screen>
      <LockScreen :avatar @to-login="handleLogout" />
    </template>
    <!-- 본문 오른쪽의 AI 채팅. 레이아웃은 열림 여부와 폭만 잡고 내용은 여기서 넣는다. -->
    <template #ai-chat>
      <AiChatContent />
    </template>
  </BasicLayout>
</template>
