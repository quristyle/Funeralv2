<script lang="ts" setup>
import type {
  OnActionClickParams,
  VxeTableGridOptions,
} from '#/adapter/vxe-table';
import type { SystemCompanyApi } from '#/api/portal/system/company';
import type { SystemDeptApi } from '#/api/portal/system/dept';

import { computed, onMounted, ref } from 'vue';

import { Page, useVbenModal } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import { Card, Empty, Input, message, Spin } from 'ant-design-vue';
import GridIconButton from '#/components/GridIconButton.vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getCompanyList } from '#/api/portal/system/company';
import { deleteDept, getDeptList } from '#/api/portal/system/dept';
import { $t } from '#/locales';

import { useColumns } from './data';
import Form from './modules/form.vue';

/**
 * [부서 관리]
 *
 * 회사가 14개인데 예전에는 위쪽 드롭다운 하나로 회사를 골랐다. 그래서 여러 회사의
 * 부서를 훑으려면 **고르고 → 펼치고 → 다시 고르고** 를 반복해야 했고,
 * 지금 어느 회사를 보고 있는지도 드롭다운을 확인해야 알 수 있었다.
 *
 * 왼쪽에 회사 목록을 놓고 오른쪽에 그 회사의 부서 트리를 둔다. 한 번 눌러 바로 옮겨 가고,
 * 무엇을 보고 있는지 늘 보인다. `/company/user` · `/system/role-map` 이 이미 쓰는 모양이라
 * 관리 화면들 사이에서 조작이 일관된다.
 *
 * 회사 목록에는 **부서 수와 사용자 수**를 함께 보여 준다. 어느 회사를 봐야 하는지
 * 고르기 전에 규모를 알 수 있고, "부서는 있는데 사람이 없는 회사" 가 바로 드러난다.
 *
 * '전체' 를 고르면 모든 회사의 부서를 한 번에 본다. 그때만 회사명 열을 보여 준다 —
 * 한 회사를 보고 있을 때는 모든 행에 같은 값이 들어가 자리만 차지한다.
 */

/** 전체 보기를 나타내는 값. 빈 문자열이면 '아직 안 골랐다' 와 헷갈린다. */
const ALL = '__ALL__';

const companies = ref<SystemCompanyApi.SystemCompany[]>([]);
const loadingCompanies = ref(false);
const selectedCompanyId = ref<string>(ALL);
const companyKeyword = ref('');

const filteredCompanies = computed(() => {
  const kw = companyKeyword.value.trim().toLowerCase();
  if (!kw) return companies.value;
  return companies.value.filter((c) =>
    [c.name, c.shortName].some((v) => (v ?? '').toLowerCase().includes(kw)),
  );
});

/** 지금 보고 있는 회사. 전체일 때는 undefined 다. */
const currentCompany = computed(() =>
  selectedCompanyId.value === ALL
    ? undefined
    : companies.value.find((c) => c.id === selectedCompanyId.value),
);

const isAll = computed(() => selectedCompanyId.value === ALL);

/** 전체 합계. '전체' 줄에 붙여 규모를 먼저 보여 준다. */
const totals = computed(() => ({
  depts: companies.value.reduce((s, c) => s + (c.deptCount ?? 0), 0),
  users: companies.value.reduce((s, c) => s + (c.userCount ?? 0), 0),
}));

/**
 * 목록 응답에서 배열을 꺼낸다.
 *
 * AuthServer 의 응답 필터는 목록을 `{ result: [...], page: {...} }` 로 감싸 보낸다.
 * `company.ts` 의 반환 타입은 `{ items, total }` 로 적혀 있는데 **실제 응답과 다르다.**
 * 타입만 믿고 `res.items` 를 꺼내면 undefined 가 되고, 통째로 쓰면 배열이 아니라
 * 객체가 들어와 `reduce` 에서 터진다(실제로 그렇게 터졌다).
 *
 * 그래서 모양을 가리지 않고 꺼내고, **배열인지 확인한 뒤에만** 쓴다.
 * (타입 선언을 고치는 것이 맞지만 다른 화면들이 그 타입을 함께 쓰고 있어
 *  이 화면에서 건드리지 않는다.)
 */
function pickList<T>(res: any): T[] {
  const raw = res?.result ?? res?.items ?? res?.data?.result ?? res;
  return Array.isArray(raw) ? raw : [];
}

async function loadCompanies() {
  loadingCompanies.value = true;
  try {
    companies.value = pickList<SystemCompanyApi.SystemCompany>(
      await getCompanyList(),
    );
  } catch (error) {
    console.error(error);
    message.error('회사 목록을 불러오지 못했습니다.');
  } finally {
    loadingCompanies.value = false;
  }
}

function selectCompany(id: string) {
  if (selectedCompanyId.value === id) return;
  selectedCompanyId.value = id;
  refreshGrid();
}

const [FormModal, formModalApi] = useVbenModal({
  connectedComponent: Form,
  destroyOnClose: true,
});

function onEdit(row: SystemDeptApi.SystemDept) {
  formModalApi.setData(row).open();
}

function onAppend(row: SystemDeptApi.SystemDept) {
  // 하위 부서는 부모와 같은 회사에 붙는다. '전체' 로 보고 있어도 행의 회사를 따른다.
  formModalApi.setData({ pid: row.id, companyId: row.companyId }).open();
}

function onCreate() {
  // '전체' 에서는 어느 회사에 만들지 알 수 없으므로 비워 두고 폼에서 고르게 한다.
  formModalApi.setData({ companyId: currentCompany.value?.id }).open();
}

function onDelete(row: SystemDeptApi.SystemDept) {
  deleteDept(row.id)
    .then(() => {
      message.success({
        content: $t('ui.actionMessage.deleteSuccess', [row.name]),
      });
      // 부서를 지우면 회사 목록의 부서 수도 달라진다. 둘을 함께 새로 받는다.
      refreshAll();
    })
    .catch((error) => {
      console.error(error);
    });
}

function onActionClick({
  code,
  row,
}: OnActionClickParams<SystemDeptApi.SystemDept>) {
  switch (code) {
    case 'append': {
      onAppend(row);
      break;
    }
    case 'delete': {
      onDelete(row);
      break;
    }
    case 'edit': {
      onEdit(row);
      break;
    }
  }
}

const [Grid, gridApi] = useVbenVxeGrid({
  gridEvents: {},
  gridOptions: {
    columns: useColumns(onActionClick),
    /**
     * 아래 도구줄의 [추가] 아이콘을 위쪽 [부서 추가] 단추와 같은 곳에 연결한다.
     * 공통 그리드는 화면마다 무엇을 추가하는지 모르므로 그 함수를 여기서 준다.
     */
    gridFeatures: { onCreate },
    height: 'auto',
    keepSource: true,
    pagerConfig: {
      enabled: false,
    },
    proxyConfig: {
      ajax: {
        query: async (_params) => {
          // '전체' 는 allCompanies 로 분명히 알린다.
          // 회사 인자를 비우면 서버가 **로그인한 사람의 회사**로 좁힌다 — '전체' 가 아니다.
          // (예전 화면의 '전체' 가 실제로는 자기 회사만 보여 주고 있었던 이유다.)
          return isAll.value
            ? await getDeptList(undefined, true)
            : await getDeptList(selectedCompanyId.value);
        },
      },
    },
    // 보이는 컬럼 · 재조회 · 전체화면은 **아래 도구줄**로 옮겼다
    // (adapter/vxe-grid-features.ts). 여기 남겨 두면 위아래로 두 벌이 된다.
    // 위쪽 도구줄 자체는 그대로다 — [부서 추가] 단추와 제목이 사는 자리다.
    treeConfig: {
      parentField: 'pid',
      rowField: 'id',
      transform: false,
    },
  } as VxeTableGridOptions,
});

function refreshGrid() {
  gridApi.query();
  // 한 회사만 볼 때는 회사명 열이 모든 행에 같은 값이라 자리만 차지한다.
  gridApi.setGridOptions({
    columns: useColumns(onActionClick, isAll.value),
  });
}

/** 부서를 고치면 회사 목록의 숫자도 바뀐다. 둘을 함께 새로 받는다. */
async function refreshAll() {
  await loadCompanies();
  gridApi.query();
}

onMounted(async () => {
  await loadCompanies();
  refreshGrid();
});
</script>

<template>
  <Page auto-content-height>
    <FormModal @success="refreshAll" />

    <div class="flex h-full gap-3">
      <!-- 왼쪽: 회사 목록 -->
      <Card
        class="flex w-[260px] shrink-0 flex-col"
        :body-style="{
          padding: '8px',
          flex: 1,
          minHeight: 0,
          display: 'flex',
          flexDirection: 'column',
        }"
        size="small"
        title="회사"
      >
        <Input
          v-model:value="companyKeyword"
          allow-clear
          class="mb-2"
          placeholder="회사명 검색"
          size="small"
        >
          <template #prefix>
            <IconifyIcon class="text-muted-foreground" icon="lucide:search" />
          </template>
        </Input>

        <Spin :spinning="loadingCompanies">
          <div class="min-h-0 flex-1 overflow-auto">
            <!-- 전체 보기 -->
            <button
              class="hover:bg-accent mb-1 flex w-full items-center justify-between rounded px-2 py-1.5 text-left text-sm transition-colors"
              :class="
                isAll ? 'bg-primary text-primary-foreground hover:bg-primary' : ''
              "
              type="button"
              @click="selectCompany(ALL)"
            >
              <span class="font-medium">전체</span>
              <span class="shrink-0 text-xs opacity-80">
                부서 {{ totals.depts }} · 사용자 {{ totals.users }}
              </span>
            </button>

            <Empty
              v-if="filteredCompanies.length === 0"
              :description="
                companyKeyword ? '검색 결과가 없습니다.' : '등록된 회사가 없습니다.'
              "
              :image="Empty.PRESENTED_IMAGE_SIMPLE"
            />

            <button
              v-for="company in filteredCompanies"
              :key="company.id"
              class="hover:bg-accent flex w-full flex-col items-start gap-0.5 rounded px-2 py-1.5 text-left transition-colors"
              :class="
                selectedCompanyId === company.id
                  ? 'bg-primary text-primary-foreground hover:bg-primary'
                  : ''
              "
              type="button"
              @click="selectCompany(company.id)"
            >
              <span class="w-full truncate text-sm font-medium">
                {{ company.name }}
              </span>
              <span class="text-xs opacity-80">
                부서 {{ company.deptCount ?? 0 }} · 사용자
                {{ company.userCount ?? 0 }}
              </span>
            </button>
          </div>
        </Spin>
      </Card>

      <!-- 오른쪽: 부서 트리 -->
      <div class="min-w-0 flex-1">
        <Grid
          :table-title="
            isAll ? '부서 목록 (전체 회사)' : `부서 목록 — ${currentCompany?.name ?? ''}`
          "
        >
          <template #toolbar-tools>
            <GridIconButton
              v-perm:create
              :title="$t('ui.actionTitle.create', [$t('system.dept.name')])"
              icon="vxe-icon-add"
              @click="onCreate"
            />
          </template>
        </Grid>
      </div>
    </div>
  </Page>
</template>
