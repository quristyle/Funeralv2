<script lang="ts" setup>
import type { OnActionClickParams, VxeTableGridOptions, } from '#/adapter/vxe-table';

import { Page, useVbenDrawer } from '@vben/common-ui';
import { IconifyIcon, Plus } from '@vben/icons';
import { $t } from '@vben/locales';

import { MenuBadge } from '@vben-core/menu-ui';

import { Button, message, Tooltip } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { deleteMenu, getMenuList, SystemMenuApi } from '#/api/system/menu';

import { useColumns } from './data';
import Form from './modules/form.vue';
import { attachTypeApi } from 'ant-design-vue/es/message';

const [FormDrawer, formDrawerApi] = useVbenDrawer({ connectedComponent: Form, destroyOnClose: true, });

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: useColumns(onActionClick),
    proxyConfig: {
      ajax: {
        query: async (_params) => {
          const list = await getMenuList();
          return { items: list, total: list.length }; // 전역 설정(response.result = 'items')에 맞춰 포장하여 반환
        },
      },
    },
    rowConfig: {
      keyField: 'id',
    },
    treeConfig: {
      rowField: 'id',
      childrenField: 'children', // 자식 노드 배열의 키 이름 명시
      transform: false, // 데이터가 이미 계층형(children 포함)이므로 변환 안 함
    },
  } as VxeTableGridOptions,
});

/**
 * 모든 메뉴 노드를 확장합니다.
 */
function onExpandAll() { gridApi.grid?.setAllTreeExpand(true); }

/**
 * 모든 메뉴 노드를 축소합니다.
 */
function onCollapseAll() { gridApi.grid?.setAllTreeExpand(false); }

function onActionClick({ code, row, }: OnActionClickParams<SystemMenuApi.SystemMenu>) {
  switch (code) {
    case 'append': { onAppend(row); break; }
    case 'delete': { onDelete(row); break; }
    case 'edit': { onEdit(row); break; }
    default: { break; }
  }
}

function onRefresh() { gridApi.query(); }
function onEdit(row: SystemMenuApi.SystemMenu) { formDrawerApi.setData(row).open(); }
function onCreate() { formDrawerApi.setData({}).open(); }
function onAppend(row: SystemMenuApi.SystemMenu) { formDrawerApi.setData({ pid: row.id }).open(); }

function onDelete(row: SystemMenuApi.SystemMenu) {
  deleteMenu(row.id)
    .then(() => {
      message.success({
        content: $t('ui.actionMessage.deleteSuccess', [row.name]),
      });
      onRefresh();
    })
    .catch((error) => {
      console.error(error);
    });
}
</script>
<template>
  <Page auto-content-height>
    <FormDrawer @success="onRefresh" />
    <Grid>
      <template #toolbar-tools>
        <!-- 메뉴 전체 확장/축소 버튼 추가 -->
        <div class="flex gap-2">
          <Tooltip :title="$t('common.expandAll')">
            <Button @click="onExpandAll">
              <template #icon>
                <IconifyIcon icon="ant-design:expand-outlined" />
              </template>
            </Button>
          </Tooltip>
          <Tooltip :title="$t('common.collapseAll')">
            <Button @click="onCollapseAll">
              <template #icon>
                <IconifyIcon icon="ant-design:compress-outlined" />
              </template>
            </Button>
          </Tooltip>
          <Button type="primary" @click="onCreate">
            <Plus class="size-5" />
            {{ $t('ui.actionTitle.create', [$t('system.menu.name')]) }}
          </Button>
        </div>
      </template>
      <template #title="{ row }">
        <div class="flex w-full items-center gap-1">
          <div class="size-5 shrink-0">
            <IconifyIcon
              v-if="row.type === 'button'"
              icon="carbon:security"
              class="size-full"
            />
            <IconifyIcon
              v-else-if="row.meta?.icon"
              :icon="row.meta?.icon || 'carbon:circle-dash'"
              class="size-full"
            />
          </div>
          <span class="flex-auto">{{ $t(row.meta?.title) }}</span>
          <div class="items-center justify-end"></div>
        </div>
        <MenuBadge
          v-if="row.meta?.badgeType"
          class="menu-badge"
          :badge="row.meta.badge"
          :badge-type="row.meta.badgeType"
          :badge-variants="row.meta.badgeVariants"
        />
      </template>
    </Grid>
  </Page>
</template>
<style lang="scss" scoped>
.menu-badge {
  top: 50%;
  right: 0;
  transform: translateY(-50%);

  & > :deep(div) {
    padding-top: 0;
    padding-bottom: 0;
  }
}
</style>
