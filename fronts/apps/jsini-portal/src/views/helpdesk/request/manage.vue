<script lang="ts" setup>
import type { ImprovementRequest } from '#/api/helpdesk';

import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import { Button, Card, Empty, Input, Select, Space, Tag } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { searchRequests } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';
import {
  formatDateTime,
  REQUEST_STATUS_OPTIONS,
  REQUEST_STATUSES,
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
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **가져오기 방식은 그대로다** — 페이지도 정렬도 서버가 한다.
 * 그래서 머리글 필터줄은 **지금 화면에 올라온 페이지 안에서만** 걸린다.
 * 전체에서 걸러야 하는 조건(키워드 · 상태 · 회사 · 접수자)은 위의 조회 줄이 맡는다.
 *
 * 모바일 카드 목록은 그대로 두었다. 그리드가 채워 주는 `rows` 를 같이 쓴다
 * (좁은 화면에서는 그리드가 숨겨지지만 조회는 그대로 돈다).
 * ------------------------------------------------------------
 */

const route = useRoute();
const router = useRouter();
const helpdesk = useHelpdeskStore();

const loading = ref(false);
const rows = ref<ImprovementRequest[]>([]);

/**
 * 조회 준비가 끝났는가.
 *
 * 그리드는 뜨자마자 스스로 한 번 조회한다. 신원·조직·주소창 조건을 다 읽은 뒤에야
 * 그리드를 붙여서(`v-if`), 그 첫 조회 한 번이 곧 제대로 된 조회가 되게 한다.
 */
const ready = ref(false);

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

/** 페이징 상태. 그리드가 준 값을 받아 두고 모바일 '더 보기'도 같이 쓴다. */
const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
});

/** 정렬 상태. 원본 기본값과 같이 최신 작성일 순으로 시작한다. */
const sort = reactive<{ dir: 'asc' | 'desc'; field: string }>({
  dir: 'desc',
  field: 'createdAt',
});

/**
 * 첫 조회 한 번은 주소창이 준 정렬을 그대로 쓴다.
 * 그리드 이름줄에는 아직 화살표가 서 있지 않아 vxe 가 정렬을 비워 주기 때문이다.
 */
let useUrlSort = true;

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

/** 상태 칸의 필터는 고르는 칸이다. 저장된 값은 열거형 이름(`Pending`)이다. */
const STATUS_FILTER_OPTIONS = REQUEST_STATUSES.map((s) => ({
  label: s.label,
  value: s.value,
}));

const columns = computed(() => {
  const base: any[] = [
    {
      field: 'title',
      minWidth: 260,
      params: { filterText: (row: any) => row.title ?? '' },
      slots: { default: 'title' },
      title: '제목',
    },
    {
      field: 'createdAt',
      params: { filterText: (row: any) => formatDateTime(row.createdAt) },
      slots: { default: 'createdAt' },
      title: '작성일',
      width: 150,
    },
    {
      field: 'status',
      params: { filterOptions: STATUS_FILTER_OPTIONS },
      slots: { default: 'status' },
      title: '상태',
      width: 90,
    },
    {
      field: 'customer.userName',
      title: '작성자',
      width: 110,
    },
    {
      field: 'admin.userName',
      title: '접수자',
      width: 110,
    },
    {
      field: 'completededAt',
      params: { filterText: (row: any) => formatDateTime(row.completededAt) },
      slots: { default: 'completededAt' },
      title: '완료일',
      width: 150,
    },
  ];

  // 회사 컬럼은 관리자에게만 의미가 있다. 고객은 자기 회사 건만 보기 때문.
  if (helpdesk.isAdmin) {
    base.push({ field: 'company.name', title: '회사', width: 120 });
  }
  return base;
});

/**
 * 그리드 컬럼 이름 → 서버 정렬 필드.
 *
 * 서버가 받는 이름과 표에 그리는 경로가 다른 것은 회사 하나뿐이다.
 * (원본 `resolveSortField` 가 하던 일이다.)
 */
function resolveSortField(field?: string) {
  if (!field) return 'createdAt';
  return field === 'company.name' ? 'customer.company.name' : field;
}

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

const [Grid, gridApi] = useVbenVxeGrid({
  gridEvents: {
    // 원본 `custom-row` 의 행 클릭. 상세로 들어간다.
    cellClick: ({ row }: any) => openDetail(row),
  },
  gridOptions: {
    columns: columns.value,
    emptyText: '조회된 요청이 없습니다.',
    height: 'auto',
    pagerConfig: { enabled: true, pageSize: 20 },
    rowClassName: () => 'cursor-pointer',
    rowConfig: { keyField: 'id' },
    // 정렬은 서버가 한다. 한 페이지만 올라와 있어 화면에서 세우면 그 페이지만 선다.
    sortConfig: { multiple: false, remote: true },
    proxyConfig: {
      ajax: {
        query: async ({ page, sorts }: any) => {
          // 담당자는 연결이 없어도 전체 요청을 조회한다. 조회 범위는 조건이 정하고
          // 헬프데스크 내부 ID 를 쓰지 않는다.
          if (!helpdesk.canUse) return { page: { total: 0 }, result: [] };

          pagination.current = page?.currentPage ?? 1;
          pagination.pageSize = page?.pageSize ?? 20;

          const picked = sorts?.[0];
          if (picked?.field) {
            sort.field = resolveSortField(picked.field);
            sort.dir = picked.order === 'asc' ? 'asc' : 'desc';
          } else if (!useUrlSort) {
            // 이름줄에서 정렬을 풀었다. 원본과 같이 기본값으로 돌아간다.
            sort.field = 'createdAt';
            sort.dir = 'desc';
          }
          useUrlSort = false;

          loading.value = true;
          try {
            const result = await searchRequests(buildPayload());
            rows.value = result.items;
            pagination.total = result.totalCount;
            syncQuery();
            return { page: { total: result.totalCount }, result: result.items };
          } finally {
            loading.value = false;
          }
        },
      },
    },
  },
});

/** 컬럼은 관리자 여부가 정해진 뒤에야 확정된다. 바뀌면 다시 심는다. */
watch(columns, (next) => gridApi.setGridOptions({ columns: next }));

/** 조회 버튼 · 조건 변경 시 첫 페이지부터 다시 조회한다. */
function search() {
  pagination.current = 1;
  // `reload` 는 vxe 의 페이저를 1쪽으로 되돌린 뒤 조회한다.
  gridApi.reload();
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
 * 좁은 화면에는 그리드가 없으므로 여기서 직접 조회한다.
 */
async function loadMore() {
  if (!helpdesk.canUse) return;

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
  if (!helpdesk.canUse) return;

  // 접수자 셀렉트는 고객에게도 보이므로 조직 목록은 항상 필요하다.
  await helpdesk.loadOrganizations();

  if (!helpdesk.isAdmin) {
    filters.companyId = helpdesk.companyId;
  }

  applyQueryFilters();
  // 주소창이 준 쪽 번호·쪽 크기를 페이저에 먼저 넘겨 준다.
  gridApi.setGridOptions({
    pagerConfig: {
      currentPage: pagination.current,
      enabled: true,
      pageSize: pagination.pageSize,
    },
  });
  // 여기서 그리드가 붙고, 그리드가 스스로 첫 조회를 한다.
  ready.value = true;
  watchReady = true;
});
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <HelpdeskAccountNotice />

    <template v-if="helpdesk.canUse">
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

      <!--
        모바일: 카드 목록. 원본도 좁은 화면에서는 카드로 바꿔 보여줬다.
        표보다 **먼저** 둔다 — `page-fill-last` 는 마지막 자식에게 높이를 주는데
        데스크톱에서 남은 높이를 채워야 하는 것은 표이기 때문이다.
      -->
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

      <!-- 데스크탑: 표. 조건이 다 갖춰진 뒤에 붙인다(위 `ready` 설명). -->
      <Grid v-if="ready" class="hidden md:block">
        <template #title="{ row }">
          <div class="flex items-center gap-2">
            <img
              v-if="row.mainPhoto"
              :alt="row.title"
              class="h-10 w-16 shrink-0 rounded object-cover"
              :src="row.mainPhoto"
              @error="onPhotoError($event, row.mainPhoto)"
            />
            <span class="min-w-0 flex-1 truncate font-medium">
              {{ row.title }}
            </span>
            <Tag v-if="row.attachmentCount > 0">
              첨부 {{ row.attachmentCount }}
            </Tag>
          </div>
        </template>

        <template #status="{ row }">
          <Tag :color="statusMeta(row.status).color">
            {{ row.statusName || statusMeta(row.status).label }}
          </Tag>
        </template>

        <template #createdAt="{ row }">
          {{ formatDateTime(row.createdAt) }}
        </template>

        <template #completededAt="{ row }">
          {{ formatDateTime(row.completededAt) }}
        </template>
      </Grid>
    </template>
  </Page>
</template>
