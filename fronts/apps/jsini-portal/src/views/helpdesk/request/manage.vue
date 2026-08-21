<script lang="ts" setup>
import type { ImprovementRequest } from '#/api/helpdesk';

import { computed, onMounted, reactive, ref, watch } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Empty,
  Input,
  Select,
  Space,
  Table,
  Tag,
} from 'ant-design-vue';

import { searchRequests } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import {
  formatDateTime,
  REQUEST_STATUS_OPTIONS,
  statusMeta,
} from '../shared/constants';

/**
 * [요청 처리 — 관리자용 전체 요청 목록]
 *
 * 원본(JinReception MngRequest.vue)의 조회 조건과 컬럼 구성을 그대로 옮겼다.
 * 서버 검색은 DynamicFilterHelper 규약을 따른다: `title_or_like`, `status`,
 * `customer.companyId` 처럼 "필드_연산자" 키를 본문에 담아 POST 한다.
 */

const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const rows = ref<ImprovementRequest[]>([]);

/** 조회 조건 */
// 셀렉트의 '전체'는 undefined 로 둔다. 서버에는 payload 를 만들 때 null 로 바꿔 보낸다.
const filters = reactive<{
  adminId: number | undefined;
  companyId: number | undefined;
  customerId: number | undefined;
  keyword: string;
  status: number | undefined;
}>({
  adminId: undefined,
  companyId: undefined,
  customerId: undefined,
  keyword: '',
  status: undefined,
});

/** 페이징 상태 */
const pagination = reactive({
  current: 1,
  pageSize: 20,
  showSizeChanger: true,
  pageSizeOptions: ['10', '20', '50', '100'],
  total: 0,
  showTotal: (total: number) => `총 ${total}건`,
});

const columns = computed(() => {
  const base = [
    { dataIndex: 'title', key: 'title', title: '제목', ellipsis: true },
    { dataIndex: 'createdAt', key: 'createdAt', title: '작성일', width: 150 },
    { dataIndex: 'status', key: 'status', title: '상태', width: 90 },
    {
      dataIndex: ['customer', 'userName'],
      key: 'customer',
      title: '작성자',
      width: 110,
    },
    {
      dataIndex: ['admin', 'userName'],
      key: 'admin',
      title: '접수자',
      width: 110,
    },
    {
      dataIndex: 'completededAt',
      key: 'completededAt',
      title: '완료일',
      width: 150,
    },
  ];

  // 회사 컬럼은 관리자에게만 의미가 있다. 고객은 자기 회사 건만 보기 때문.
  if (helpdesk.isAdmin) {
    base.push({
      dataIndex: ['company', 'name'],
      key: 'company',
      title: '회사',
      width: 120,
    } as any);
  }
  return base;
});

/**
 * 검색 조건을 서버 규약(필드_연산자)에 맞춰 만든다.
 * 값이 null 이면 키를 빼지 않고 그대로 보낸다 — 원본과 동일하게 서버가 null 을 "조건 없음"으로 처리한다.
 */
function buildPayload() {
  const payload: Record<string, any> = {
    select:
      'id,title,createdAt,status,customer,admin,customer.company,mainPhoto,completededAt',
    remove: 'description,admin.photo',
    sorts: [{ dir: 'desc', field: 'createdAt' }],
    page: pagination.current,
    pageSize: pagination.pageSize,
  };

  const keyword = filters.keyword.trim();
  payload.title_or_like = keyword || null;
  payload.description_or_like = keyword || null;
  payload.status = filters.status ?? null;

  if (helpdesk.isAdmin) {
    // '대기'(코드 0) 조회에서는 담당자 조건을 걸지 않는다. 아직 담당자가 없는 건들이라
    // 담당자 필터를 함께 걸면 결과가 항상 비게 된다.
    payload.adminId = filters.status === 0 ? null : (filters.adminId ?? null);
    payload['customer.id'] = filters.customerId ?? null;
    payload['customer.companyId'] = filters.companyId ?? null;
  } else {
    // 고객은 본인 회사 건만 조회한다.
    payload['customer.companyId'] = helpdesk.companyId ?? null;
    payload.customerId = helpdesk.helpdeskUserId ?? null;
  }

  return payload;
}

/** 목록을 조회한다. */
async function loadData() {
  if (!helpdesk.helpdeskUserId) return;

  loading.value = true;
  try {
    const page = await searchRequests(buildPayload());
    rows.value = page.items;
    pagination.total = page.totalCount;
  } finally {
    loading.value = false;
  }
}

/** 조회 버튼 · 조건 변경 시 첫 페이지부터 다시 조회한다. */
function search() {
  pagination.current = 1;
  loadData();
}

function onTableChange(pag: { current?: number; pageSize?: number }) {
  pagination.current = pag.current ?? 1;
  pagination.pageSize = pag.pageSize ?? 20;
  loadData();
}

function openDetail(row: ImprovementRequest) {
  router.push(`/helpdesk/request/detail/${row.id}`);
}

// 조건이 바뀌면 조회한다. 입력 중 매 글자마다 호출되지 않도록 키워드는 엔터·버튼으로만 조회한다.
watch(
  () => [filters.status, filters.adminId, filters.companyId, filters.customerId],
  () => search(),
);

onMounted(async () => {
  await helpdesk.loadIdentity();
  if (!helpdesk.helpdeskUserId) return;

  if (helpdesk.isAdmin) {
    await helpdesk.loadOrganizations();
  } else {
    filters.companyId = helpdesk.companyId;
    filters.customerId = helpdesk.helpdeskUserId;
  }
  await loadData();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <template v-if="helpdesk.helpdeskUserId">
      <Card class="mb-3" size="small">
        <Space wrap>
          <Input
            v-model:value="filters.keyword"
            allow-clear
            placeholder="제목 + 본문"
            style="width: 220px"
            @press-enter="search"
          />
          <Select
            v-model:value="filters.status"
            :options="REQUEST_STATUS_OPTIONS"
            placeholder="상태"
            style="width: 120px"
          />
          <Select
            v-if="helpdesk.isAdmin"
            v-model:value="filters.companyId"
            :options="helpdesk.companyOptions"
            option-filter-prop="label"
            placeholder="회사"
            show-search
            style="width: 160px"
          />
          <Select
            v-if="helpdesk.isAdmin"
            v-model:value="filters.customerId"
            :options="helpdesk.customerOptions"
            option-filter-prop="label"
            placeholder="접수자"
            show-search
            style="width: 160px"
          />
          <Select
            v-if="helpdesk.isAdmin"
            v-model:value="filters.adminId"
            :options="helpdesk.adminOptions"
            option-filter-prop="label"
            placeholder="담당자"
            show-search
            style="width: 160px"
          />
          <Button :loading="loading" type="primary" @click="search">
            조회
          </Button>
        </Space>
      </Card>

      <Card :body-style="{ padding: 0 }" size="small">
        <Table
          :columns="columns"
          :custom-row="
            (record: ImprovementRequest) => ({
              onClick: () => openDetail(record),
              style: 'cursor: pointer',
            })
          "
          :data-source="rows"
          :loading="loading"
          :pagination="pagination"
          :scroll="{ x: 900 }"
          row-key="id"
          size="small"
          @change="onTableChange"
        >
          <template #emptyText>
            <Empty description="조회된 요청이 없습니다." />
          </template>

          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'title'">
              <span class="font-medium">{{ record.title }}</span>
              <Tag v-if="record.attachmentCount > 0" class="ml-2">
                첨부 {{ record.attachmentCount }}
              </Tag>
            </template>

            <template v-else-if="column.key === 'status'">
              <Tag :color="statusMeta(record.status).color">
                {{ record.statusName || statusMeta(record.status).label }}
              </Tag>
            </template>

            <template v-else-if="column.key === 'createdAt'">
              {{ formatDateTime(record.createdAt) }}
            </template>

            <template v-else-if="column.key === 'completededAt'">
              {{ formatDateTime(record.completededAt) }}
            </template>
          </template>
        </Table>
      </Card>
    </template>
  </Page>
</template>
