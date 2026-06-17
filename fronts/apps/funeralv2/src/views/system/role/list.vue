<script lang="ts" setup>
import type { Recordable } from '@vben/types';

import type {
  OnActionClickParams,
  VxeTableGridOptions,
} from '#/adapter/vxe-table';
import type { SystemRoleApi } from '#/api';

import { Page, useVbenDrawer } from '@vben/common-ui';
import { Plus } from '@vben/icons';

import { Button, message, Modal } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { deleteRole, getRoleList, updateRole } from '#/api';
import { $t } from '#/locales';

import { useColumns, useGridFormSchema } from './data';
import Form from './modules/form.vue';

const [FormDrawer, formDrawerApi] = useVbenDrawer({
  connectedComponent: Form,
  destroyOnClose: true,
});

const [Grid, gridApi] = useVbenVxeGrid({
  formOptions: {
    fieldMappingTime: [['createTime', ['startTime', 'endTime']]],
    schema: useGridFormSchema(),
    submitOnChange: true,
  },
  gridOptions: {
    columns: useColumns(onActionClick, onStatusChange),
    height: 'auto',
    keepSource: true,
    proxyConfig: {
      ajax: {
        query: async ({ page }, formValues) => {
          return await getRoleList({
            page: page.currentPage,
            pageSize: page.pageSize,
            ...formValues,
          });
        },
      },
    },
    rowConfig: {
      keyField: 'id',
    },

    toolbarConfig: {
      custom: true,
      export: false,
      refresh: true,
      search: true,
      zoom: true,
    },
  } as VxeTableGridOptions<SystemRoleApi.SystemRole>,
});

function onActionClick(e: OnActionClickParams<SystemRoleApi.SystemRole>) {
  switch (e.code) {
    case 'delete': {
      onDelete(e.row);
      break;
    }
    case 'edit': {
      onEdit(e.row);
      break;
    }
  }
}

/**
 * Antd의 Modal.confirm을 Promise로 래핑하여 비동기 함수에서 호출하기 편리하게 만듭니다.
 * @param content 팁 내용
 * @param title 팁 제목
 */
function confirm(content: string, title: string) {
  return new Promise((reslove, reject) => {
    Modal.confirm({
      content,
      onCancel() {
        reject(new Error('취소됨'));
      },
      onOk() {
        reslove(true);
      },
      title,
    });
  });
}

/**
 * 상태 스위치가 곧 변경됩니다.
 * @param newStatus 변경될 상태 값
 * @param row 행 데이터
 * @returns false를 반환하면 변경을 중지하고, 다른 값(undefined, true)을 반환하면 변경을 허용합니다.
 */
async function onStatusChange(
  newStatus: number,
  row: SystemRoleApi.SystemRole,
) {
  const status: Recordable<string> = {
    0: '비활성화',
    1: '활성화',
  };
  try {
    await confirm(
      `${row.name}의 상태를 【${status[newStatus.toString()]}】(으)로 전환하시겠습니까?`,
      `상태 전환`,
    );
    await updateRole(row.id, { status: newStatus });
    return true;
  } catch {
    return false;
  }
}

function onEdit(row: SystemRoleApi.SystemRole) {
  formDrawerApi.setData(row).open();
}

function onDelete(row: SystemRoleApi.SystemRole) {
  const hideLoading = message.loading({
    content: $t('ui.actionMessage.deleting', [row.name]),
    duration: 0,
    key: 'action_process_msg',
  });
  deleteRole(row.id)
    .then(() => {
      message.success({
        content: $t('ui.actionMessage.deleteSuccess', [row.name]),
        key: 'action_process_msg',
      });
      onRefresh();
    })
    .catch(() => {
      hideLoading();
    });
}

function onRefresh() {
  gridApi.query();
}

function onCreate() {
  formDrawerApi.setData({}).open();
}
</script>
<template>
  <Page auto-content-height>
    <FormDrawer @success="onRefresh" />
    <Grid :table-title="$t('system.role.list')">
      <template #toolbar-tools>
        <Button type="primary" @click="onCreate">
          <Plus class="size-5" />
          {{ $t('ui.actionTitle.create', [$t('system.role.name')]) }}
        </Button>
      </template>
    </Grid>
  </Page>
</template>
