<script lang="ts" setup>
import type { ImprovementRequest } from '#/api/helpdesk';

import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Avatar,
  Button,
  Card,
  Carousel,
  Col,
  Empty,
  Progress,
  Row,
  Spin,
} from 'ant-design-vue';

import {
  getAdminStats,
  getAllAdminStats,
  getCompanyStats,
  searchRequests,
} from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { formatDate, formatDateTime } from '../shared/constants';

/**
 * [요청 모니터]
 *
 * 원본(JinReception moni/RequestMonitor.vue, `/request_monitor`).
 *
 *  1. 나의 접수 진행사항 — 상태별 건수(+비율)와 접수율·완료율 게이지, 미접수 요청 목록
 *  2. 회사별 현황 — 완료율 막대와 상태별 건수
 *  3. 담당자별 현황 — 아바타, 상태 5칸, 접수율·완료율 막대
 *
 * 원본처럼 숫자를 누르면 그 조건으로 걸러진 요청 목록으로 이동한다.
 * 원본은 조건을 pinia 스토어에 담아 넘겼지만, 여기서는 새로고침에도 남도록 쿼리스트링으로 넘긴다.
 */

const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const adminStats = ref<any>(null);
const allAdminStats = ref<any[]>([]);
const companyStats = ref<any[]>([]);
const pendingRequests = ref<ImprovementRequest[]>([]);

/**
 * 상태별 건수 정의. key 는 통계 응답의 필드, code 는 검색에 쓰는 열거형 순번.
 * 원본의 dashboardStatuses 와 같은 구성이다.
 */
const DASHBOARD_STATUSES = [
  { code: 1, color: 'text-orange-500', key: 'inProgressCount', label: '진행' },
  { code: 3, color: 'text-green-600', key: 'completedCount', label: '완료' },
  { code: 2, color: 'text-red-500', key: 'rejectedCount', label: '반려' },
  { code: 5, color: 'text-purple-500', key: 'consultationCount', label: '협의' },
  { code: 6, color: 'text-teal-500', key: 'negotiationCount', label: '논의' },
  { code: 7, color: 'text-green-500', key: 'userCompletedCount', label: '종료' },
];

/** 회사 카드에 표시할 상태 4칸 */
const COMPANY_STATUSES = [
  { code: 0, color: 'text-blue-500', key: 'pendingCount', label: '대기' },
  { code: 1, color: 'text-orange-500', key: 'inProgressCount', label: '진행' },
  { code: 3, color: 'text-green-600', key: 'completedCount', label: '완료' },
  { code: 5, color: 'text-purple-500', key: 'consultationCount', label: '협의/논의' },
];

/** 담당자 카드에 표시할 상태 5칸 */
const ADMIN_STATUSES = [
  { code: 0, color: 'text-blue-500', key: 'pendingCount', label: '대기' },
  { code: 1, color: 'text-orange-500', key: 'inProgressCount', label: '진행' },
  { code: 3, color: 'text-green-600', key: 'completedCount', label: '완료' },
  { code: 5, color: 'text-purple-500', key: 'consultationCount', label: '협의' },
  { code: 6, color: 'text-indigo-500', key: 'negotiationCount', label: '논의' },
];

/** 내 처리 통계 — 접수율·완료율을 계산해 붙인다. 원본 adminStatsComputed 와 같다. */
const myStats = computed(() => {
  const s = adminStats.value;
  if (!s) {
    return {
      acceptanceRate: 0,
      completionRate: 0,
      myTotalHandled: 0,
      totalCompleted: 0,
      totalRequests: 0,
    };
  }

  const myTotalHandled =
    (s.inProgressCount ?? 0) +
    (s.completedCount ?? 0) +
    (s.userCompletedCount ?? 0) +
    (s.rejectedCount ?? 0) +
    (s.consultationCount ?? 0) +
    (s.negotiationCount ?? 0);

  const totalCompleted = (s.completedCount ?? 0) + (s.userCompletedCount ?? 0);

  return {
    ...s,
    acceptanceRate:
      s.totalRequests > 0
        ? Number(((myTotalHandled / s.totalRequests) * 100).toFixed(1))
        : 0,
    completionRate:
      myTotalHandled > 0
        ? Number(((totalCompleted / myTotalHandled) * 100).toFixed(1))
        : 0,
    myTotalHandled,
    totalCompleted,
  };
});

/** 내 처리 건수 대비 비율. 원본 calculateRate. */
function calculateRate(count: number) {
  const total = myStats.value.myTotalHandled;
  return total > 0 ? ((count / total) * 100).toFixed(1) : '0.0';
}

/** 캐러셀이 화면 폭에 따라 몇 장씩 보일지. 원본 responsiveOptions 와 같은 구간. */
const CAROUSEL_BREAKPOINTS = {
  575: { slidesToShow: 1 },
  767: { slidesToShow: 2 },
  1199: { slidesToShow: 3 },
  1400: { slidesToShow: 4 },
} as const;

const carouselSettings = {
  arrows: true,
  dots: false,
  draggable: true,
  responsive: Object.entries(CAROUSEL_BREAKPOINTS).map(([bp, cfg]) => ({
    breakpoint: Number(bp),
    settings: { slidesToScroll: 1, ...cfg },
  })),
  slidesToScroll: 1,
  slidesToShow: 5,
};

// ── 목록으로 이동 ─────────────────────────────────────────

/**
 * 상태(+담당자)로 걸러진 요청 목록을 연다.
 * 담당자를 넘기지 않으면 나 자신으로 본다(원본과 동일).
 */
function goToListByStatus(code: null | number, admin?: any) {
  const query: Record<string, string> = {};
  if (code !== null) query.status = String(code);

  const adminId = admin?.adminId ?? helpdesk.helpdeskUserId;
  if (adminId) query.adminId = String(adminId);

  router.push({ path: '/helpdesk/request/manage', query });
}

/** 회사 + 상태로 걸러진 목록을 연다. */
function goToListByCompanyStatus(company: any, code: number) {
  router.push({
    path: '/helpdesk/request/manage',
    query: {
      companyId: String(company.companyId ?? company.id ?? ''),
      status: String(code),
    },
  });
}

async function loadAll() {
  loading.value = true;
  try {
    const [mine, admins, companies, pending] = await Promise.all([
      getAdminStats(),
      getAllAdminStats(),
      getCompanyStats(),
      // 미접수(대기, 코드 0) 요청. 원본과 같은 정렬·건수.
      searchRequests({
        select:
          'id,title,createdAt,customer.userName,admin,status,customer.company.name,mainPhoto',
        remove: 'customerId,description',
        sorts: [
          { dir: 'asc', field: 'status' },
          { dir: 'desc', field: 'createdAt' },
        ],
        page: 1,
        pageSize: 4,
        status: 0,
      }),
    ]);

    adminStats.value = mine;

    allAdminStats.value = (admins ?? []).map((stat: any) => {
      const totalHandled =
        (stat.inProgressCount ?? 0) +
        (stat.completedCount ?? 0) +
        (stat.rejectedCount ?? 0) +
        (stat.consultationCount ?? 0) +
        (stat.negotiationCount ?? 0);
      return {
        ...stat,
        completionRate:
          totalHandled > 0
            ? Number(((stat.completedCount / totalHandled) * 100).toFixed(1))
            : 0,
        totalHandled,
      };
    });

    companyStats.value = (companies ?? []).map((stat: any) => {
      const total =
        (stat.pendingCount ?? 0) +
        (stat.inProgressCount ?? 0) +
        (stat.completedCount ?? 0) +
        (stat.userCompletedCount ?? 0) +
        (stat.rejectedCount ?? 0) +
        (stat.consultationCount ?? 0) +
        (stat.negotiationCount ?? 0);
      const totalCompleted =
        (stat.completedCount ?? 0) + (stat.userCompletedCount ?? 0);
      return {
        ...stat,
        completionRate:
          total > 0 ? Number(((totalCompleted / total) * 100).toFixed(1)) : 0,
      };
    });

    pendingRequests.value = pending.items;
  } finally {
    loading.value = false;
  }
}

/** 회사 카드의 협의/논의는 두 값을 합쳐 보여준다(원본과 동일). */
function companyCount(stat: any, key: string) {
  if (key === 'consultationCount') {
    return (stat.consultationCount ?? 0) + (stat.negotiationCount ?? 0);
  }
  return stat[key] ?? 0;
}

onMounted(async () => {
  await helpdesk.loadIdentity();
  // 담당자는 연결이 없어도 모니터를 본다. '나의 접수' 칸만 비어 있게 된다.
  if (helpdesk.canUse) await loadAll();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Spin :spinning="loading">
      <!-- 1. 나의 접수 진행사항 -->
      <Card class="mb-3" size="small" title="나의 접수 진행사항">
        <template #extra>
          <Button :loading="loading" size="small" @click="loadAll">
            새로고침
          </Button>
        </template>

        <Row :gutter="[12, 12]">
          <!-- 상태별 건수 -->
          <Col :lg="7" :xs="24">
            <button
              v-for="item in DASHBOARD_STATUSES"
              :key="item.key"
              class="mb-1 flex w-full items-center justify-between rounded border border-border px-3 py-2 text-left last:mb-0 hover:bg-accent"
              type="button"
              @click="goToListByStatus(item.code)"
            >
              <span class="text-sm" :class="item.color">{{ item.label }}</span>
              <span class="text-lg font-bold">
                {{ adminStats?.[item.key] ?? 0 }}
                <span class="text-xs font-normal text-muted-foreground">
                  ({{ calculateRate(adminStats?.[item.key] ?? 0) }}%)
                </span>
              </span>
            </button>
          </Col>

          <!-- 접수율 · 완료율 -->
          <Col :lg="7" :xs="24">
            <Row :gutter="[12, 12]">
              <Col :span="12">
                <div class="flex flex-col items-center">
                  <div class="mb-1 text-sm">나의 접수율</div>
                  <button
                    class="cursor-pointer border-0 bg-transparent p-0"
                    type="button"
                    @click="goToListByStatus(1)"
                  >
                    <Progress
                      :percent="myStats.acceptanceRate"
                      :width="110"
                      type="circle"
                    />
                  </button>
                  <button
                    class="mt-1 cursor-pointer border-0 bg-transparent p-0 text-xs text-muted-foreground"
                    type="button"
                    @click="goToListByStatus(null)"
                  >
                    {{ myStats.myTotalHandled }} / {{ myStats.totalRequests }}
                  </button>
                </div>
              </Col>

              <Col :span="12">
                <div class="flex flex-col items-center">
                  <div class="mb-1 text-sm">나의 완료율</div>
                  <button
                    class="cursor-pointer border-0 bg-transparent p-0"
                    type="button"
                    @click="goToListByStatus(3)"
                  >
                    <Progress
                      :percent="myStats.completionRate"
                      :width="110"
                      stroke-color="#22C55E"
                      type="circle"
                    />
                  </button>
                  <button
                    class="mt-1 cursor-pointer border-0 bg-transparent p-0 text-xs text-muted-foreground"
                    type="button"
                    @click="goToListByStatus(null)"
                  >
                    {{ myStats.totalCompleted }} / {{ myStats.myTotalHandled }}
                  </button>
                </div>
              </Col>
            </Row>
          </Col>

          <!-- 미접수 요청 -->
          <Col :lg="10" :xs="24">
            <div class="mb-1 text-xs text-muted-foreground">미접수 요청</div>

            <Empty
              v-if="pendingRequests.length === 0"
              :image="Empty.PRESENTED_IMAGE_SIMPLE"
              description="미접수 요청이 없습니다."
            />

            <button
              v-for="item in pendingRequests"
              :key="item.id"
              class="flex w-full items-start gap-2 border-b border-border py-2 text-left last:border-b-0 hover:bg-accent"
              type="button"
              @click="router.push(`/helpdesk/request/detail/${item.id}`)"
            >
              <img
                v-if="item.mainPhoto"
                :alt="item.title"
                class="h-16 w-24 shrink-0 rounded object-cover"
                :src="item.mainPhoto"
              />
              <div class="min-w-0 flex-1">
                <div class="truncate text-sm font-medium">{{ item.title }}</div>
                <div class="mt-1 text-xs text-muted-foreground">
                  {{ item.customer?.userName }} ·
                  {{ formatDateTime(item.createdAt) }}
                </div>
              </div>
            </button>
          </Col>
        </Row>
      </Card>

      <!-- 2. 회사별 현황 -->
      <Card class="mb-3" size="small" title="회사별 현황">
        <Empty
          v-if="companyStats.length === 0"
          description="회사별 통계가 없습니다."
        />
        <Carousel v-else v-bind="carouselSettings">
          <div v-for="stat in companyStats" :key="stat.companyId ?? stat.id">
            <div class="mx-1 rounded border border-border p-3">
              <div class="mb-3">
                <div class="truncate text-base font-medium">
                  {{ stat.companyName }}
                </div>
                <div class="text-xs text-muted-foreground">
                  <template v-if="stat.lastPendingDate">
                    마지막 접수: {{ formatDate(stat.lastPendingDate) }}
                  </template>
                  <template v-else>&nbsp;</template>
                </div>
              </div>

              <div class="mb-1 flex items-center justify-between">
                <span class="text-sm font-medium">완료율</span>
                <span class="text-sm font-bold">{{ stat.completionRate }}%</span>
              </div>
              <Progress
                :percent="stat.completionRate"
                :show-info="false"
                size="small"
                stroke-color="#22C55E"
              />

              <div class="mt-3 grid grid-cols-2 gap-1">
                <button
                  v-for="s in COMPANY_STATUSES"
                  :key="s.key"
                  class="flex items-center rounded p-1 text-left hover:bg-accent"
                  type="button"
                  @click="goToListByCompanyStatus(stat, s.code)"
                >
                  <span class="text-xs font-medium" :class="s.color">
                    {{ s.label }}
                  </span>
                  <span class="ml-auto text-sm font-bold">
                    {{ companyCount(stat, s.key) }}
                  </span>
                </button>
              </div>
            </div>
          </div>
        </Carousel>
      </Card>

      <!-- 3. 담당자별 현황 -->
      <Card size="small" title="담당자별 현황">
        <Empty
          v-if="allAdminStats.length === 0"
          description="담당자 통계가 없습니다."
        />
        <Carousel v-else v-bind="carouselSettings">
          <div v-for="stat in allAdminStats" :key="stat.adminId">
            <div class="mx-1 rounded border border-border p-3">
              <div class="mb-3 flex items-center gap-2">
                <Avatar v-if="stat.adminPhoto" :src="stat.adminPhoto" />
                <Avatar v-else>
                  {{ String(stat.adminName ?? '?').charAt(0).toUpperCase() }}
                </Avatar>
                <div class="min-w-0">
                  <div class="truncate font-bold">{{ stat.adminName }}</div>
                  <div class="text-xs text-muted-foreground">
                    {{ stat.totalHandled }}건
                  </div>
                </div>
              </div>

              <div class="mb-3 grid grid-cols-5 gap-1 text-center">
                <button
                  v-for="s in ADMIN_STATUSES"
                  :key="s.key"
                  class="rounded p-1 hover:bg-accent"
                  type="button"
                  @click="goToListByStatus(s.code, stat)"
                >
                  <div class="text-sm font-semibold" :class="s.color">
                    {{ stat[s.key] ?? 0 }}
                  </div>
                  <div class="text-[10px]">{{ s.label }}</div>
                </button>
              </div>

              <div class="mb-2">
                <div class="mb-1 flex justify-between">
                  <span class="text-xs">접수율</span>
                  <span class="text-xs font-bold">
                    {{ stat.acceptanceRate ?? 0 }}%
                  </span>
                </div>
                <Progress
                  :percent="Number(stat.acceptanceRate ?? 0)"
                  :show-info="false"
                  size="small"
                />
              </div>

              <div>
                <div class="mb-1 flex justify-between">
                  <span class="text-xs">완료율</span>
                  <span class="text-xs font-bold">{{ stat.completionRate }}%</span>
                </div>
                <Progress
                  :percent="stat.completionRate"
                  :show-info="false"
                  size="small"
                  stroke-color="#22C55E"
                />
              </div>
            </div>
          </div>
        </Carousel>
      </Card>
    </Spin>
  </Page>
</template>
