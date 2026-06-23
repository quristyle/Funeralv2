<script lang="ts" setup>
import { ref, h } from 'vue';
import { Page, useVbenDrawer } from '@vben/common-ui';
import { Card, Tabs, Button, Tooltip, Popconfirm, message } from 'ant-design-vue';
import { Plus, IconifyIcon } from '@vben/icons';
import { useVbenVxeGrid, type VxeTableGridColumns } from '#/adapter/vxe-table';
import { getRoleList, deleteRole } from '#/api/system/role';
import { $t } from '#/locales';
import RoleUserTab from './modules/RoleUserTab.vue';
import RoleMenuTab from './modules/RoleMenuTab.vue';
import Form from './modules/form.vue';

// 현재 선택된 역할 ID
const selectedRoleId = ref<string>('');
const selectedRoleName = ref<string>('');

const [FormDrawer, formDrawerApi] = useVbenDrawer({
  connectedComponent: Form,
  destroyOnClose: true,
});

function onCreate() {
  formDrawerApi.setData({}).open();
}

function onEdit(row: any) {
  formDrawerApi.setData(row).open();
}

function onDelete(row: any) {
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
      if (selectedRoleId.value === row.id) {
        selectedRoleId.value = '';
        selectedRoleName.value = '';
      }
      onRefresh();
    })
    .catch(() => {
      hideLoading();
    });
}

function onRefresh() {
  gridApi.query();
}

// 역할 목록 그리드 컬럼 설정
const columns: VxeTableGridColumns = [
  { field: 'name', title: '역할명', minWidth: 120 },
  { field: 'id', title: '역할 ID', width: 100 },
  {
    title: '작업',
    width: 100,
    align: 'center',
    slots: {
      default: (record: any) => {
        return h('div', { class: 'flex justify-center gap-2' }, [
          h(
            Tooltip,
            { title: $t('common.edit') },
            {
              default: () =>
                h(
                  Button,
                  {
                    size: 'small',
                    type: 'link',
                    onClick: (e: Event) => {
                      e.stopPropagation();
                      onEdit(record.row);
                    },
                  },
                  {
                    icon: () =>
                      h(IconifyIcon, {
                        class: 'size-4',
                        icon: 'lucide:edit',
                      }),
                  },
                ),
            },
          ),
          h(
            Popconfirm,
            {
              getPopupContainer: () => document.body,
              onConfirm: () => onDelete(record.row),
              placement: 'topLeft',
              title: $t('ui.actionMessage.deleteConfirm', [record.row.name]),
            },
            {
              default: () =>
                h(
                  Tooltip,
                  { title: $t('common.delete') },
                  {
                    default: () =>
                      h(
                        Button,
                        {
                          danger: true,
                          size: 'small',
                          type: 'link',
                          onClick: (e: Event) => {
                            e.stopPropagation();
                          },
                        },
                        {
                          icon: () =>
                            h(IconifyIcon, {
                              class: 'size-4',
                              icon: 'lucide:trash-2',
                            }),
                        },
                      ),
                  },
                ),
            },
          ),
        ]);
      },
    },
  },
];

// VXETable 설정
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: columns,
    height: 'auto',
    rowConfig: {
      isCurrent: true,
      keyField: 'id',
    },
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getRoleList({});
        },
      },
    },
  },
  gridEvents: {
    cellClick: ({ row, column }: any) => {
      // 작업 컬럼을 클릭했을 때는 row 선택이 되지 않도록 방지
      if (column.field && row) {
        selectedRoleId.value = row.id;
        selectedRoleName.value = row.name;
      }
    },
  },
});

const activeTabKey = ref('users');
</script>

<template>
  <Page auto-content-height>
    <FormDrawer @success="onRefresh" />
    <div class="grid grid-cols-12 gap-4 h-full">
      <!-- 좌측: 역할 목록 그리드 -->
      <div class="col-span-12 lg:col-span-4 h-full flex flex-col">
        <Card class="flex-1 flex flex-col h-full overflow-hidden" title="역할 목록" :body-style="{ flex: 1, padding: 0, overflow: 'hidden' }">
          <template #extra>
            <Button type="primary" size="small" @click="onCreate">
              <Plus class="size-4" />
              {{ $t('ui.actionTitle.create', [$t('system.role.name')]) }}
            </Button>
          </template>
          <Grid class="h-full w-full" />
        </Card>
      </div>

      <!-- 우측: 매핑 상세 및 탭 설정 -->
      <div class="col-span-12 lg:col-span-8 h-full flex flex-col">
        <Card class="flex-1 flex flex-col h-full overflow-hidden" :title="selectedRoleName ? `[${selectedRoleName}] 권한 설정` : '역할을 선택해 주세요'" :body-style="{ flex: 1, display: 'flex', flexDirection: 'column', overflow: 'hidden', padding: '12px 24px' }">
          <template v-if="selectedRoleId">
            <Tabs v-model:activeKey="activeTabKey" class="flex-1 flex flex-col overflow-hidden custom-tabs">
              <!-- 사용자 지정 탭 -->
              <Tabs.TabPane key="users" tab="사용자 매핑 관리" class="flex-1 flex flex-col overflow-hidden">
                <RoleUserTab :role-id="selectedRoleId" />
              </Tabs.TabPane>
              
              <!-- 메뉴 세부 권한 탭 -->
              <Tabs.TabPane key="menus" tab="메뉴 세부 권한 관리" class="flex-1 flex flex-col overflow-hidden">
                <RoleMenuTab :role-id="selectedRoleId" />
              </Tabs.TabPane>
            </Tabs>
          </template>
          
          <template v-else>
            <div class="flex-1 flex flex-col justify-center items-center text-gray-400 py-20">
              <span class="text-lg font-medium">좌측 목록에서 권한을 설정할 역할을 클릭해 주세요.</span>
            </div>
          </template>
        </Card>
      </div>
    </div>
  </Page>
</template>

<style lang="css" scoped>
.custom-tabs :deep(.ant-tabs-content) {
  height: 100%;
  display: flex;
  flex-direction: column;
}

.custom-tabs :deep(.ant-tabs-tabpane) {
  display: flex;
  flex-direction: column;
  height: 100%;
  overflow: hidden;
}
</style>
