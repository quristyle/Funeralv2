<script lang="ts" setup>
import type {
  OnActionClickParams,
  VxeTableGridOptions,
} from '#/adapter/vxe-table';
import type { SystemDeptApi } from '#/api/portal/system/dept';

import { ref } from 'vue';

import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';

import { Button, message } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import BizSelect from '#/components/BizSelect.vue';
import { deleteDept, getDeptList } from '#/api/portal/system/dept';
import { $t } from '#/locales';

import { useColumns } from './data';
import Form from './modules/form.vue';

const selectedCompanyId = ref<string | undefined>(undefined);

function onCompanyChange() {
  refreshGrid();
}

const [FormModal, formModalApi] = useVbenModal({
  connectedComponent: Form,
  destroyOnClose: true,
});

/**
 * 부서 편집
 * @param row
 */
function onEdit(row: SystemDeptApi.SystemDept) {
  formModalApi.setData(row).open();
}

/**
 * 하위 부서 추가
 * @param row
 */
function onAppend(row: SystemDeptApi.SystemDept) {
  formModalApi.setData({ pid: row.id, companyId: selectedCompanyId.value }).open();
}

/**
 * 새 부서 생성
 */
function onCreate() {
  formModalApi.setData({ companyId: selectedCompanyId.value }).open();
}

/**
 * 부서 삭제
 * @param row
 */
function onDelete(row: SystemDeptApi.SystemDept) {
  deleteDept(row.id)
    .then(() => {
      message.success({
        content: $t('ui.actionMessage.deleteSuccess', [row.name]),
      });
      refreshGrid();
    })
    .catch((error) => {
      console.error(error);
    });
}

/**
 * 테이블 작업 버튼의 콜백 함수
 */
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
    height: 'auto',
    keepSource: true,
    pagerConfig: {
      enabled: false,
    },
    proxyConfig: {
      ajax: {
        query: async (_params) => {
          return await getDeptList(selectedCompanyId.value);
        },
      },
    },
    toolbarConfig: {
      custom: true,
      export: false,
      refresh: true,
      zoom: true,
    },
    treeConfig: {
      parentField: 'pid',
      rowField: 'id',
      transform: false,
    },
  } as VxeTableGridOptions,
});

/**
 * 테이블 새로고침
 */
function refreshGrid() {
  gridApi.query();
}
</script>
<template>
  <Page auto-content-height content-class="page-fill-last">
    <FormModal @success="refreshGrid" />
    <div class="mb-4 flex items-center gap-4 bg-card p-4 rounded-lg shadow-sm border border-border">
      <span class="text-sm font-medium">회사 선택 :</span>
      <BizSelect
        v-model:value="selectedCompanyId"
        type="company"
        auto-select-first
        show-all
        placeholder="회사를 선택해주세요"
        class="w-64"
        show-search
        option-filter-prop="label"
        @change="onCompanyChange"
      />
    </div>
    <Grid table-title="부서 목록">
      <template #toolbar-tools>
        <Button v-perm:create type="primary" @click="onCreate">
          <Plus class="size-5" />
          {{ $t('ui.actionTitle.create', [$t('system.dept.name')]) }}
        </Button>
      </template>
    </Grid>
  </Page>
</template>
