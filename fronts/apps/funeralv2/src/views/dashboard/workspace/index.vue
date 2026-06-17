<script lang="ts" setup>
import type {
  WorkbenchProjectItem,
  WorkbenchQuickNavItem,
  WorkbenchTodoItem,
  WorkbenchTrendItem,
} from '@vben/common-ui';

import { ref } from 'vue';
import { useRouter } from 'vue-router';

import {
  AnalysisChartCard,
  WorkbenchHeader,
  WorkbenchProject,
  WorkbenchQuickNav,
  WorkbenchTodo,
  WorkbenchTrends,
} from '@vben/common-ui';
import { preferences } from '@vben/preferences';
import { useUserStore } from '@vben/stores';
import { openWindow } from '@vben/utils';

import AnalyticsVisitsSource from '../analytics/analytics-visits-source.vue';

const userStore = useUserStore();

// 이것은 예시 데이터이며, 실제 프로젝트에서는 실제 상황에 맞춰 조정이 필요합니다.
// url은 내부 라우트일 수도 있으며, navTo 메서드에서 이를 식별하여 내부 페이지 이동을 처리합니다.
// 예: url: /dashboard/workspace
const projectItems: WorkbenchProjectItem[] = [
  {
    color: '',
    content: '기회를 기다리지 말고, 기회를 만드세요.',
    date: '2021-04-01',
    group: '오픈 소스 팀',
    icon: 'carbon:logo-github',
    title: 'Github',
    url: 'https://github.com',
  },
  {
    color: '#3fb27f',
    content: '현재의 당신이 미래의 당신을 결정합니다.',
    date: '2021-04-01',
    group: '알고리즘 팀',
    icon: 'ion:logo-vue',
    title: 'Vue',
    url: 'https://vuejs.org',
  },
  {
    color: '#e18525',
    content: '노력보다 더 중요한 재능은 없습니다.',
    date: '2021-04-01',
    group: '근무 중 휴식',
    icon: 'ion:logo-html5',
    title: 'Html5',
    url: 'https://developer.mozilla.org/zh-CN/docs/Web/HTML',
  },
  {
    color: '#bf0c2c',
    content: '열정과 욕망은 모든 난관을 돌파할 수 있습니다.',
    date: '2021-04-01',
    group: 'UI',
    icon: 'ion:logo-angular',
    title: 'Angular',
    url: 'https://angular.io',
  },
  {
    color: '#00d8ff',
    content: '건강한 신체는 목표 달성의 초석입니다.',
    date: '2021-04-01',
    group: '기술 전문가',
    icon: 'bx:bxl-react',
    title: 'React',
    url: 'https://reactjs.org',
  },
  {
    color: '#EBD94E',
    content: '길은 걸어가는 것이지, 공상하는 것이 아닙니다.',
    date: '2021-04-01',
    group: '아키텍처 팀',
    icon: 'ion:logo-javascript',
    title: 'Js',
    url: 'https://developer.mozilla.org/zh-CN/docs/Web/JavaScript',
  },
];

// 마찬가지로, 여기의 url은 http로 시작하는 외부 링크를 사용할 수도 있습니다.
const quickNavItems: WorkbenchQuickNavItem[] = [
  {
    color: '#1fdaca',
    icon: 'ion:home-outline',
    title: '홈',
    url: '/',
  },
  {
    color: '#bf0c2c',
    icon: 'ion:grid-outline',
    title: '대시보드',
    url: '/dashboard',
  },
  {
    color: '#e18525',
    icon: 'ion:layers-outline',
    title: '컴포넌트',
    url: '/demos/features/icons',
  },
  {
    color: '#3fb27f',
    icon: 'ion:settings-outline',
    title: '시스템 관리',
    url: '/demos/features/login-expired', // 여기의 URL은 예시이며, 실제 프로젝트 상황에 맞춰 조정이 필요합니다.
  },
  {
    color: '#4daf1bc9',
    icon: 'ion:key-outline',
    title: '권한 관리',
    url: '/demos/access/page-control',
  },
  {
    color: '#00d8ff',
    icon: 'ion:bar-chart-outline',
    title: '차트',
    url: '/analytics',
  },
];

const todoItems = ref<WorkbenchTodoItem[]>([
  {
    completed: false,
    content: `Git 저장소에 최근 커밋된 프론트엔드 코드를 검토하여 코드 품질과 표준을 보장합니다.`,
    date: '2024-07-30 11:00:00',
    title: '프론트엔드 코드 커밋 검토',
  },
  {
    completed: true,
    content: `시스템 성능을 확인 및 최적화하여 CPU 사용률을 낮춥니다.`,
    date: '2024-07-30 11:00:00',
    title: '시스템 성능 최적화',
  },
  {
    completed: false,
    content: `시스템 보안 검사를 수행하여 보안 취약점이나 무단 액세스가 없는지 확인합니다. `,
    date: '2024-07-30 11:00:00',
    title: '보안 검사',
  },
  {
    completed: false,
    content: `프로젝트의 모든 npm 종속성 패키지를 업데이트하여 최신 버전을 사용하도록 합니다.`,
    date: '2024-07-30 11:00:00',
    title: '프로젝트 종속성 업데이트',
  },
  {
    completed: false,
    content: `사용자가 보고한 페이지 UI 표시 문제를 수정하여 여러 브라우저에서 일관되게 표시되도록 합니다. `,
    date: '2024-07-30 11:00:00',
    title: 'UI 표시 문제 수정',
  },
]);
const trendItems: WorkbenchTrendItem[] = [
  {
    avatar: 'svg:avatar-1',
    content: `<a>오픈 소스 팀</a>에서 <a>Vue</a> 프로젝트를 생성했습니다.`,
    date: '방금',
    title: '윌리엄',
  },
  {
    avatar: 'svg:avatar-2',
    content: `<a>윌리엄</a>님을 팔로우했습니다. `,
    date: '1시간 전',
    title: '아이번',
  },
  {
    avatar: 'svg:avatar-3',
    content: `<a>개인 활동</a>을 게시했습니다. `,
    date: '1일 전',
    title: '크리스',
  },
  {
    avatar: 'svg:avatar-4',
    content: `<a>Vite 플러그인 작성 방법</a> 기사를 게시했습니다. `,
    date: '2일 전',
    title: 'Vben',
  },
  {
    avatar: 'svg:avatar-1',
    content: `<a>잭</a>의 질문 <a>프로젝트 최적화는 어떻게 하나요?</a>에 답변했습니다.`,
    date: '3일 전',
    title: '피터',
  },
  {
    avatar: 'svg:avatar-2',
    content: `<a>프로젝트 실행 방법</a> 질문을 닫았습니다. `,
    date: '1주 전',
    title: '잭',
  },
  {
    avatar: 'svg:avatar-3',
    content: `<a>개인 활동</a>을 게시했습니다. `,
    date: '1주 전',
    title: '윌리엄',
  },
  {
    avatar: 'svg:avatar-4',
    content: `<a>Github</a>에 코드를 푸시했습니다.`,
    date: '2021-04-01 20:00',
    title: '윌리엄',
  },
  {
    avatar: 'svg:avatar-4',
    content: `<a>Admin Vben 사용 방법</a> 기사를 게시했습니다. `,
    date: '2021-03-01 20:00',
    title: 'Vben',
  },
];

const router = useRouter();

// 이것은 예시 메서드이며, 실제 프로젝트 상황에 맞춰 조정이 필요합니다.
// This is a sample method, adjust according to the actual project requirements
function navTo(nav: WorkbenchProjectItem | WorkbenchQuickNavItem) {
  if (nav.url?.startsWith('http')) {
    openWindow(nav.url);
    return;
  }
  if (nav.url?.startsWith('/')) {
    router.push(nav.url).catch((error) => {
      console.error('Navigation failed:', error);
    });
  } else {
    console.warn(`Unknown URL for navigation item: ${nav.title} -> ${nav.url}`);
  }
}
</script>

<template>
  <div class="p-5">
    <WorkbenchHeader
      :avatar="userStore.userInfo?.avatar || preferences.app.defaultAvatar"
    >
      <template #title>
        좋은 아침입니다, {{ userStore.userInfo?.realName }}님, 오늘의 업무를 시작해 보세요!
      </template>
      <template #description> 오늘 맑음, 20℃ - 32℃! </template>
    </WorkbenchHeader>

    <div class="mt-5 flex flex-col lg:flex-row">
      <div class="mr-4 w-full lg:w-3/5">
        <WorkbenchProject :items="projectItems" title="프로젝트" @click="navTo" />
        <WorkbenchTrends :items="trendItems" class="mt-5" title="최신 활동" />
      </div>
      <div class="w-full lg:w-2/5">
        <WorkbenchQuickNav
          :items="quickNavItems"
          class="mt-5 lg:mt-0"
          title="빠른 탐색"
          @click="navTo"
        />
        <WorkbenchTodo :items="todoItems" class="mt-5" title="할 일" />
        <AnalysisChartCard class="mt-5" title="방문 소스">
          <AnalyticsVisitsSource />
        </AnalysisChartCard>
      </div>
    </div>
  </div>
</template>
