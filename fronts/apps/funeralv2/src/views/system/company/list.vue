<script lang="ts" setup>
import type { OnActionClickParams, VxeTableGridOptions, } from '#/adapter/vxe-table';
import type { SystemCompanyApi } from '#/api/system/company';

import { Page, useVbenDrawer } from '@vben/common-ui';
import { Plus } from '@vben/icons';

import { Button, message } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { deleteCompany, getCompanyList, updateCompany } from '#/api/system/company';
import { $t } from '#/locales';

import { useColumns } from './data';
import Form from './modules/form.vue';

const [FormDrawer, formDrawerApi] = useVbenDrawer({ connectedComponent: Form, destroyOnClose: true, });

/**
 * 회사 편집
 * @param row
 */
function onEdit(row: SystemCompanyApi.SystemCompany) { formDrawerApi.setData(row).open(); }

/**
 * 새 회사 생성
 */
function onCreate() { formDrawerApi.setData({}).open(); }

/**
 * 회사 삭제
 * @param row
 */
function onDelete(row: SystemCompanyApi.SystemCompany) {
  deleteCompany(row.id)
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
 * 회사 수정 완료 시 서버로 전송
 * @param row
 */
async function onEditClosed({ row }: any) {
  const grid = gridApi.grid;
  if (!grid) return;

  // 데이터가 실제로 변경되었는지 확인
  if (!grid.isUpdateByRow(row)) { return; }

  const { id, name, businessNumber, representative, status, remark } = row;
  try {
    await updateCompany(id, { name, businessNumber, representative, status, remark, });
    message.success($t('ui.actionMessage.updateSuccess', [name]));
    // 저장 후 변경 상태 마크 제거
    grid.reloadRow(row, null);
  } catch (error) {
    console.error(error);
    // 에러 시 원래 데이터로 복구
    grid.revertData(row);
  }
}

/**
 * 테이블 작업 버튼의 콜백 함수
 */
function onActionClick({ code, row, }: OnActionClickParams<SystemCompanyApi.SystemCompany>) {
  switch (code) {
    case 'delete': { onDelete(row); break; }
    case 'edit': { onEdit(row); break; }
  }
}

const [Grid, gridApi] = useVbenVxeGrid({
  gridEvents: { editClosed: onEditClosed, },
  gridOptions: {
    columns: useColumns(onActionClick),
    proxyConfig: {
      ajax: {
        query: async (_params) => {
          return await getCompanyList();
        },
      },
    }
  } as VxeTableGridOptions,
});

/**
 * 테이블 새로고침
 */
function refreshGrid() { gridApi.query(); }
</script>

<template>
  <Page auto-content-height>
    <FormDrawer @success="refreshGrid" />
    <Grid :table-title="$t('system.company.listTitle')" >
      <template #toolbar-tools>
        <Button type="primary" @click="onCreate">
          <Plus class="size-5" />
          {{ $t('ui.actionTitle.create', [$t('system.company.name')]) }}
        </Button>
      </template>
    </Grid>
  </Page>
</template>
