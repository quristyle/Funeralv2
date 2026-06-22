<script lang="ts" setup>
import { ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Card, Tabs } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getRoleList } from '#/api/system/role';
import RoleUserTab from './modules/RoleUserTab.vue';
import RoleMenuTab from './modules/RoleMenuTab.vue';

// 현재 선택된 역할 ID
const selectedRoleId = ref<string>('');
const selectedRoleName = ref<string>('');

// 역할 목록 그리드 컬럼 설정
const columns = [
  { field: 'name', title: '역할명', minWidth: 150 },
  { field: 'id', title: '역할 ID', width: 120 },
];

// VXETable 설정
const [Grid] = useVbenVxeGrid({
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
    cellClick: ({ row }: any) => {
      if (row) {
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
    <div class="grid grid-cols-12 gap-4 h-full">
      <!-- 좌측: 역할 목록 그리드 -->
      <div class="col-span-12 lg:col-span-4 h-full flex flex-col">
        <Card class="flex-1 flex flex-col h-full overflow-hidden" title="역할 목록" :body-style="{ flex: 1, padding: 0, overflow: 'hidden' }">
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
