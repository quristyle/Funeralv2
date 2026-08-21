<script lang="ts" setup>
import type { ImprovementRequest } from '#/api/helpdesk';

import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

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
 * [요청 처리 — 전체 요청 목록]
 *
 * 원본(JinReception reqs/MngRequest.vue, `/mng_request`).
 *
 * 서버 검색은 DynamicFilterHelper 규약을 따른다: `title_or_like`, `status`,
 * `customer.companyId` 처럼 "필드_연산자" 키를 본문에 담아 POST 한다.
 *
 * 원본은 조회 조건과 페이지를 pinia 스토어에 담고 라우터 `keepAlive` 로 화면을
 * 살려둬서 상세를 보고 돌아와도 조건이 남아 있었다. 여기서는 같은 목적을
 * 주소창 쿼리로 달성한다 — 뒤로 가기는 물론 새로고침·즐겨찾기에도 조건이 남는다.
 */

const route = useRoute();
const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const rows = ref<ImprovementRequest[]>([]);

/** 사진이 깨질 때 예전 도메인으로 한 번 더 받아본다(원본과 동일). */
const PHOTO_FALLBACK_HOST = 'https://help.jin114.co.kr';

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
  pageSizeOptions: ['10', '20', '50'],
  total: 0,
  showTotal: (total: number) => `총 ${total}건`,
});

/** 정렬 상태. 원본 기본값과 같이 최신 작성일 순으로 시작한다. */
const sort = reactive<{ dir: 'asc' | 'desc'; field: string }>({
  dir: 'desc',
  field: 'createdAt',
});

/**
 * 접수자 셀렉트 목록.
 * 관리자는 전체를, 고객은 같은 회사 사람만 고를 수 있다.
 * (원본은 모두에게 전체 고객 목록을 노출했는데, 그건 다른 회사 직원 이름까지
 *  보이는 문제가 있어 회사 범위로 좁혔다.)
 */
const customerSelectOptions = computed(() => {
  if (helpdesk.isAdmin) return helpdesk.customerOptions;

  return [
    { label: '전체', value: null },
    ...helpdesk.customers
      .filter((c) => c.companyId === helpdesk.companyId)
      .map((c) => ({ label: c.userName, value: c.id })),
  ];
});

const columns = computed(() => {
  const sorted = (field: string) => {
    const asc = 'ascend' as const;
    const desc = 'descend' as const;
    return {
      sorter: true,
      sortOrder: sort.field === field ? (sort.dir === 'asc' ? asc : desc) : null,
    };
  };

  const base = [
    {
      dataIndex: 'title',
      key: 'title',
      title: '제목',
      ellipsis: true,
      ...sorted('title'),
    },
    {
      dataIndex: 'createdAt',
      key: 'createdAt',
      title: '작성일',
      width: 150,
      ...sorted('createdAt'),
    },
    {
      dataIndex: 'status',
      key: 'status',
      title: '상태',
      width: 90,
      ...sorted('status'),
    },
    {
      dataIndex: ['customer', 'userName'],
      key: 'customer',
      title: '작성자',
      width: 110,
      ...sorted('customer.userName'),
    },
    {
      dataIndex: ['admin', 'userName'],
      key: 'admin',
      title: '접수자',
      width: 110,
      ...sorted('admin.userName'),
    },
    {
      dataIndex: 'completededAt',
      key: 'completededAt',
      title: '완료일',
      width: 150,
      ...sorted('completededAt'),
    },
  ];

  // 회사 컬럼은 관리자에게만 의미가 있다. 고객은 자기 회사 건만 보기 때문.
  if (helpdesk.isAdmin) {
    base.push({
      dataIndex: ['company', 'name'],
      key: 'company',
      title: '회사',
      width: 120,
      ...sorted('customer.company.name'),
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
    sorts: [{ dir: sort.dir, field: sort.field }],
    page: pagination.current,
    pageSize: pagination.pageSize,
  };

  const keyword = filters.keyword.trim();
  payload.title_or_like = keyword || null;
  payload.description_or_like = keyword || null;
  payload.status = filters.status ?? null;
  payload.customerId = filters.customerId ?? null;

  if (helpdesk.isAdmin) {
    // '대기'(코드 0) 조회에서는 담당자 조건을 걸지 않는다. 아직 담당자가 없는 건들이라
    // 담당자 필터를 함께 걸면 결과가 항상 비게 된다.
    payload.adminId = filters.status === 0 ? null : (filters.adminId ?? null);
    payload['customer.id'] = filters.customerId ?? null;
    payload['customer.companyId'] = filters.companyId ?? null;
  } else {
    // 고객은 본인 회사 건만 조회한다.
    payload['customer.companyId'] = helpdesk.companyId ?? null;
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
  syncQuery();
  loadData();
}

/**
 * 조건이 바뀔 때마다 서버를 두드리지 않도록 300ms 묶어서 보낸다(원본 debounce 와 동일).
 */
let searchTimer: null | ReturnType<typeof setTimeout> = null;
function triggerSearch() {
  if (searchTimer) clearTimeout(searchTimer);
  searchTimer = setTimeout(() => search(), 300);
}
onUnmounted(() => {
  if (searchTimer) clearTimeout(searchTimer);
});

/**
 * 모바일 '더 보기'. 다음 페이지를 이어 붙인다(원본 loadMore 와 같은 동작).
 */
async function loadMore() {
  if (!helpdesk.helpdeskUserId) return;

  loading.value = true;
  try {
    pagination.current += 1;
    const page = await searchRequests(buildPayload());
    rows.value = [...rows.value, ...page.items];
    pagination.total = page.totalCount;
  } finally {
    loading.value = false;
  }
}

/** 페이지 이동과 컬럼 정렬을 함께 받는다. */
function onTableChange(
  pag: { current?: number; pageSize?: number },
  _filters: unknown,
  sorter: any,
) {
  pagination.current = pag.current ?? 1;
  pagination.pageSize = pag.pageSize ?? 20;

  if (sorter?.order) {
    // 서버에 보낼 필드명은 컬럼 키에서 되짚는다.
    sort.field = resolveSortField(sorter.column?.key);
    sort.dir = sorter.order === 'ascend' ? 'asc' : 'desc';
  } else {
    sort.field = 'createdAt';
    sort.dir = 'desc';
  }

  syncQuery();
  loadData();
}

/** 컬럼 키 → 서버 정렬 필드 */
function resolveSortField(key?: string) {
  switch (key) {
    case 'admin': {
      return 'admin.userName';
    }
    case 'company': {
      return 'customer.company.name';
    }
    case 'customer': {
      return 'customer.userName';
    }
    default: {
      return key ?? 'createdAt';
    }
  }
}

function openDetail(row: ImprovementRequest) {
  router.push(`/helpdesk/request/detail/${row.id}`);
}

/** 사진이 404 면 예전 도메인에서 한 번만 다시 받아본다. */
function onPhotoError(event: Event, path?: string) {
  const img = event.target as HTMLImageElement;
  img.onerror = null;
  if (path && !path.startsWith('http')) {
    img.src = `${PHOTO_FALLBACK_HOST}${path}`;
  } else {
    img.style.display = 'none';
  }
}

// 키워드를 포함한 모든 조건 변화에 반응한다(원본 filterValues watch 와 같은 범위).
let watchReady = false;
watch(
  () => [
    filters.keyword,
    filters.status,
    filters.adminId,
    filters.companyId,
    filters.customerId,
  ],
  () => {
    if (watchReady) triggerSearch();
  },
);

/**
 * 조회 조건·페이지를 주소창에 반영한다. 히스토리를 더럽히지 않도록 replace 를 쓴다.
 */
function syncQuery() {
  const query: Record<string, string> = {};
  if (filters.keyword.trim()) query.q = filters.keyword.trim();
  if (filters.status !== undefined && filters.status !== null) {
    query.status = String(filters.status);
  }
  if (helpdesk.isAdmin) {
    if (filters.adminId) query.adminId = String(filters.adminId);
    if (filters.companyId) query.companyId = String(filters.companyId);
    if (filters.customerId) query.customerId = String(filters.customerId);
  }
  if (pagination.current > 1) query.page = String(pagination.current);
  if (pagination.pageSize !== 20) query.size = String(pagination.pageSize);
  if (sort.field !== 'createdAt' || sort.dir !== 'desc') {
    query.sort = `${sort.field},${sort.dir}`;
  }

  router.replace({ path: route.path, query });
}

/**
 * 주소창의 조건을 읽어 들인다.
 * 요청 모니터에서 넘어올 때도 같은 경로로 조건이 전달된다.
 *   ?status=1&adminId=4&companyId=1
 */
function applyQueryFilters() {
  const { adminId, companyId, customerId, page, q, size, sort: s, status } =
    route.query;

  if (typeof q === 'string') filters.keyword = q;
  if (status !== undefined && status !== '') filters.status = Number(status);
  if (adminId) filters.adminId = Number(adminId);
  if (companyId) filters.companyId = Number(companyId);
  if (customerId) filters.customerId = Number(customerId);
  if (page) pagination.current = Number(page);
  if (size) pagination.pageSize = Number(size);

  if (typeof s === 'string' && s.includes(',')) {
    const [field, dir] = s.split(',');
    if (field) sort.field = field;
    sort.dir = dir === 'asc' ? 'asc' : 'desc';
  }
}

onMounted(async () => {
  await helpdesk.loadIdentity();
  if (!helpdesk.helpdeskUserId) return;

  // 접수자 셀렉트는 고객에게도 보이므로 조직 목록은 항상 필요하다.
  await helpdesk.loadOrganizations();

  if (!helpdesk.isAdmin) {
    filters.companyId = helpdesk.companyId;
  }

  applyQueryFilters();
  await loadData();
  watchReady = true;
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
            v-model:value="filters.customerId"
            :options="customerSelectOptions"
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
            placeholder="관리자"
            show-search
            style="width: 160px"
          />
          <Button :loading="loading" type="primary" @click="search">
            조회
          </Button>
        </Space>
      </Card>

      <!-- 데스크탑: 표 -->
      <Card :body-style="{ padding: 0 }" class="hidden md:block" size="small">
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
              <div class="flex items-center gap-2">
                <img
                  v-if="record.mainPhoto"
                  :alt="record.title"
                  class="h-10 w-16 shrink-0 rounded object-cover"
                  :src="record.mainPhoto"
                  @error="onPhotoError($event, record.mainPhoto)"
                />
                <span class="min-w-0 flex-1 truncate font-medium">
                  {{ record.title }}
                </span>
                <Tag v-if="record.attachmentCount > 0">
                  첨부 {{ record.attachmentCount }}
                </Tag>
              </div>
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

      <!-- 모바일: 카드 목록. 원본도 좁은 화면에서는 카드로 바꿔 보여줬다. -->
      <Card :body-style="{ padding: '8px' }" class="md:hidden" size="small">
        <Empty v-if="rows.length === 0" description="조회된 요청이 없습니다." />

        <button
          v-for="row in rows"
          :key="row.id"
          class="mb-2 flex w-full gap-2 rounded border border-border p-2 text-left last:mb-0 hover:bg-accent"
          type="button"
          @click="openDetail(row)"
        >
          <img
            v-if="row.mainPhoto"
            :alt="row.title"
            class="h-14 w-20 shrink-0 rounded object-cover"
            :src="row.mainPhoto"
            @error="onPhotoError($event, row.mainPhoto)"
          />
          <div class="min-w-0 flex-1">
            <!-- 배지 줄: 작성자 / 접수자 / 상태 / 완료일 / 작성일 -->
            <div class="mb-1 flex flex-wrap items-center gap-1">
              <Tag>{{ row.customer?.userName }}</Tag>
              <Tag v-if="statusMeta(row.status).code !== 0" color="processing">
                {{ row.admin?.userName }}
              </Tag>
              <Tag :color="statusMeta(row.status).color">
                {{ row.statusName || statusMeta(row.status).label }}
              </Tag>
              <Tag
                v-if="row.completededAt"
                :color="statusMeta(row.status).color"
              >
                {{ formatDateTime(row.completededAt) }}
              </Tag>
              <span class="ml-auto text-xs text-muted-foreground">
                {{ formatDateTime(row.createdAt) }}
              </span>
            </div>
            <div class="truncate text-sm font-medium">{{ row.title }}</div>
          </div>
        </button>

        <!-- 모바일 더보기: 원본의 loadMore 대응 -->
        <Button
          v-if="rows.length < pagination.total"
          :loading="loading"
          block
          class="mt-2"
          @click="loadMore"
        >
          더 보기 ({{ rows.length }} / {{ pagination.total }})
        </Button>
      </Card>
    </template>
  </Page>
</template>
