<script lang="ts" setup>
import type { ImprovementRequest } from '#/api/helpdesk';

import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Col,
  List,
  ListItem,
  Progress,
  Row,
  Spin,
  Statistic,
  Table,
  Tag,
} from 'ant-design-vue';

import {
  getAdminStats,
  getAllAdminStats,
  getCompanyStats,
  searchRequests,
} from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import { formatDateTime, statusMeta } from '../shared/constants';

/**
 * [요청 모니터]
 *
 * 원본(RequestMonitor.vue)의 세 블록을 그대로 옮겼다.
 *  - 내 처리 현황(접수율·완료율)
 *  - 담당자별 처리 현황
 *  - 고객사별 처리 현황
 *  - 미접수(대기) 요청 목록
 */

const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const adminStats = ref<any>(null);
const allAdminStats = ref<any[]>([]);
const companyStats = ref<any[]>([]);
const pendingRequests = ref<ImprovementRequest[]>([]);

/** 상태별 건수 키와 라벨. 원본의 dashboardStatuses 와 같은 구성이다. */
const STATUS_KEYS = [
  { color: 'processing', key: 'inProgressCount', label: '진행' },
  { color: 'success', key: 'completedCount', label: '완료' },
  { color: 'error', key: 'rejectedCount', label: '반려' },
  { color: 'cyan', key: 'consultationCount', label: '협의' },
  { color: 'geekblue', key: 'negotiationCount', label: '논의' },
  { color: 'green', key: 'userCompletedCount', label: '종료' },
];

/** 내 처리 통계 — 접수율·완료율을 계산해 붙인다. */
const myStats = computed(() => {
  const s = adminStats.value;
  if (!s) {
    return {
      acceptanceRate: '0.0',
      completionRate: '0.0',
      myTotalHandled: 0,
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
        ? ((myTotalHandled / s.totalRequests) * 100).toFixed(1)
        : '0.0',
    completionRate:
      myTotalHandled > 0
        ? ((totalCompleted / myTotalHandled) * 100).toFixed(1)
        : '0.0',
    myTotalHandled,
    totalCompleted,
  };
});

const adminColumns = [
  { dataIndex: 'adminName', key: 'adminName', title: '담당자' },
  { dataIndex: 'inProgressCount', key: 'inProgressCount', title: '진행', width: 70 },
  { dataIndex: 'completedCount', key: 'completedCount', title: '완료', width: 70 },
  { dataIndex: 'rejectedCount', key: 'rejectedCount', title: '반려', width: 70 },
  { dataIndex: 'totalHandled', key: 'totalHandled', title: '처리계', width: 80 },
  { dataIndex: 'completionRate', key: 'completionRate', title: '완료율', width: 120 },
];

const companyColumns = [
  { dataIndex: 'companyName', key: 'companyName', title: '고객사' },
  { dataIndex: 'pendingCount', key: 'pendingCount', title: '대기', width: 70 },
  { dataIndex: 'inProgressCount', key: 'inProgressCount', title: '진행', width: 70 },
  { dataIndex: 'completedCount', key: 'completedCount', title: '완료', width: 70 },
  { dataIndex: 'completionRate', key: 'completionRate', title: '완료율', width: 120 },
];

async function loadAll() {
  loading.value = true;
  try {
    const [mine, admins, companies, pending] = await Promise.all([
      getAdminStats(),
      getAllAdminStats(),
      getCompanyStats(),
      // 미접수(대기, 코드 0) 요청만 뽑는다.
      searchRequests({
        select:
          'id,title,createdAt,customer.userName,admin,status,customer.company.name',
        remove: 'customerId,description',
        sorts: [{ dir: 'desc', field: 'createdAt' }],
        page: 1,
        pageSize: 10,
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
            ? ((stat.completedCount / totalHandled) * 100).toFixed(1)
            : '0.0',
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
          total > 0 ? ((totalCompleted / total) * 100).toFixed(1) : '0.0',
      };
    });

    pendingRequests.value = pending.items;
  } finally {
    loading.value = false;
  }
}

onMounted(async () => {
  await helpdesk.loadIdentity();
  if (helpdesk.helpdeskUserId) await loadAll();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Spin :spinning="loading">
      <Row :gutter="[12, 12]">
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic :value="myStats.myTotalHandled" title="내 처리 건수" />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic :value="myStats.totalRequests" title="전체 요청" />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :precision="1"
              :value="Number(myStats.acceptanceRate)"
              suffix="%"
              title="접수율"
            />
          </Card>
        </Col>
        <Col :lg="6" :xs="12">
          <Card size="small">
            <Statistic
              :precision="1"
              :value="Number(myStats.completionRate)"
              suffix="%"
              title="완료율"
            />
          </Card>
        </Col>
      </Row>

      <Card class="mt-3" size="small" title="내 상태별 처리">
        <Row :gutter="[12, 12]">
          <Col v-for="s in STATUS_KEYS" :key="s.key" :lg="4" :xs="8">
            <div class="text-center">
              <Tag :color="s.color">{{ s.label }}</Tag>
              <div class="mt-1 text-lg font-semibold">
                {{ (adminStats?.[s.key] ?? 0) }}
              </div>
            </div>
          </Col>
        </Row>
      </Card>

      <Row :gutter="[12, 12]" class="mt-3">
        <Col :lg="12" :xs="24">
          <Card :body-style="{ padding: 0 }" size="small" title="담당자별 처리 현황">
            <Table
              :columns="adminColumns"
              :data-source="allAdminStats"
              :pagination="false"
              row-key="adminId"
              size="small"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.key === 'completionRate'">
                  <Progress
                    :percent="Number(record.completionRate)"
                    size="small"
                  />
                </template>
              </template>
            </Table>
          </Card>
        </Col>

        <Col :lg="12" :xs="24">
          <Card :body-style="{ padding: 0 }" size="small" title="고객사별 처리 현황">
            <Table
              :columns="companyColumns"
              :data-source="companyStats"
              :pagination="false"
              row-key="companyId"
              size="small"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.key === 'completionRate'">
                  <Progress
                    :percent="Number(record.completionRate)"
                    size="small"
                  />
                </template>
              </template>
            </Table>
          </Card>
        </Col>
      </Row>

      <Card class="mt-3" size="small" title="미접수 요청">
        <template #extra>
          <Button
            size="small"
            type="link"
            @click="router.push('/helpdesk/request/manage')"
          >
            전체 보기
          </Button>
        </template>

        <List
          :data-source="pendingRequests"
          :locale="{ emptyText: '미접수 요청이 없습니다.' }"
        >
          <template #renderItem="{ item }">
            <ListItem
              class="cursor-pointer"
              @click="router.push(`/helpdesk/request/detail/${item.id}`)"
            >
              <div class="flex w-full items-center gap-3">
                <Tag :color="statusMeta(item.status).color">
                  {{ item.statusName || statusMeta(item.status).label }}
                </Tag>
                <span class="min-w-0 flex-1 truncate">{{ item.title }}</span>
                <span class="text-xs text-muted-foreground">
                  {{ item.customer?.userName }} ·
                  {{ formatDateTime(item.createdAt) }}
                </span>
              </div>
            </ListItem>
          </template>
        </List>
      </Card>
    </Spin>
  </Page>
</template>
