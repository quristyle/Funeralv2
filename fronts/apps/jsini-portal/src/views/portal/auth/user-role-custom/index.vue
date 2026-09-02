<script lang="ts" setup>
import { ref, watch, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Card, Row, Col, List, message, Modal, Checkbox } from 'ant-design-vue';
import GridIconButton from '#/components/GridIconButton.vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getUserRoles, assignUserRoles } from '#/api/portal/system/role-mapping';
import { getAccounts } from '#/api/portal/system/account';
import { getRoleList } from '#/api/portal/system/role';

const users = ref<any[]>([]);
const selectedUserId = ref<string>('');
const showRoleModal = ref<boolean>(false);
const allRoles = ref<any[]>([]);
const checkedRoleIds = ref<string[]>([]);

// 사용자 목록 로드
async function fetchUsers() {
  try {
    const data = await getAccounts();
    users.value = data || [];
    if (users.value.length > 0 && users.value[0]?.id) {
      selectedUserId.value = users.value[0].id;
    }
  } catch (error) {
    message.error('사용자 목록을 불러오는 중 오류가 발생했습니다.');
  }
}

// 테이블 설정
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'roleName', title: '롤 명칭', minWidth: 150 },
      { field: 'roleId', title: '롤 코드', minWidth: 150 },
      { field: 'assignedAt', title: '배정일자', minWidth: 180 },
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          if (!selectedUserId.value) return [];
          return await getUserRoles(selectedUserId.value);
        },
      },
    },
  },
});

// 사용자 선택 변경 시 롤 목록 재조회
watch(selectedUserId, () => {
  gridApi.query();
});

// 롤 설정 모달 열기
async function openRoleModal() {
  if (!selectedUserId.value) {
    message.warning('사용자를 선택해주세요.');
    return;
  }
  try {
    const rolesData = await getRoleList({});
    const currentRoles = await getUserRoles(selectedUserId.value);
    
    allRoles.value = rolesData || [];
    checkedRoleIds.value = currentRoles.map((cr: any) => cr.roleId);
    showRoleModal.value = true;
  } catch (error) {
    message.error('롤 목록 정보를 가져올 수 없습니다.');
  }
}

// 롤 배정 저장
async function handleSaveRoles() {
  try {
    await assignUserRoles(selectedUserId.value, checkedRoleIds.value);
    message.success('롤 배정 정보가 저장되었습니다.');
    showRoleModal.value = false;
    gridApi.query();
  } catch (error) {
    message.error('롤 저장 작업 실패');
  }
}

onMounted(() => {
  fetchUsers();
});
</script>

<template>
  <Page auto-content-height>
    <Row :gutter="16" class="h-full">
      <!-- 좌측 사용자 선택 영역 -->
      <Col :span="8" class="h-full">
        <Card title="사용자 계정 목록" class="h-full" :body-style="{ padding: '12px' }">
          <List size="small" bordered :data-source="users">
            <template #renderItem="{ item }">
              <List.Item
                :class="['cursor-pointer hover:bg-accent p-2 rounded transition-colors', { 'bg-primary text-primary-foreground hover:bg-primary/90': selectedUserId === item.id }]"
                @click="selectedUserId = item.id"
              >
                <div>
                  <div class="font-semibold">{{ item.userName }}</div>
                  <div class="text-xs opacity-80">{{ item.loginId }} ({{ item.deptName || '부서 없음' }})</div>
                </div>
              </List.Item>
            </template>
          </List>
        </Card>
      </Col>

      <!-- 우측 사용자의 지정 롤 목록 영역 -->
      <Col :span="16" class="h-full">
        <Grid table-title="배정된 롤 권한 목록">
          <template #toolbar-tools>
            <GridIconButton
              icon="vxe-icon-setting"
              title="사용자 롤 권한 설정"
              @click="openRoleModal"
            />
          </template>
        </Grid>
      </Col>
    </Row>

    <!-- 롤 권한 설정 모달 -->
    <Modal
      v-model:open="showRoleModal"
      title="사용자 롤 권한 설정"
      @ok="handleSaveRoles"
      width="450px"
    >
      <div class="p-6">
        <Checkbox.Group v-model:value="checkedRoleIds" class="flex flex-col gap-3">
          <Checkbox v-for="role in allRoles" :key="role.id" :value="role.id">
            <span class="font-semibold">{{ role.name }}</span>
            <span class="text-xs text-muted-foreground ml-2">({{ role.code }})</span>
          </Checkbox>
        </Checkbox.Group>
      </div>
    </Modal>
  </Page>
</template>
