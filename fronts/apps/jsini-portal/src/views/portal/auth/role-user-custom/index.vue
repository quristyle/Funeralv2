<script lang="ts" setup>
import { ref, watch, onMounted } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, Card, Row, Col, List, message, Popconfirm, Modal, Transfer } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getRoleUsers, removeRoleFromUsers, assignRoleToUsers } from '#/api/portal/system/role-mapping';
import { getRoleList } from '#/api/portal/system/role';
import { getAccounts } from '#/api/portal/system/account';

const roles = ref<any[]>([]);
const selectedRoleId = ref<string>('');
const showAssignModal = ref<boolean>(false);
const allUsers = ref<any[]>([]);
const targetKeys = ref<string[]>([]);

// 롤 목록 로드
async function fetchRoles() {
  try {
    const data = await getRoleList({});
    roles.value = data || [];
    if (roles.value.length > 0) {
      selectedRoleId.value = roles.value[0].id;
    }
  } catch (error) {
    message.error('롤 목록을 불러오는 중 오류가 발생했습니다.');
  }
}

// 테이블 설정
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { type: 'checkbox', width: 60 },
      { field: 'userName', title: '사용자명', minWidth: 120 },
      { field: 'loginId', title: '아이디', minWidth: 120 },
      { field: 'assignedAt', title: '배정일자', minWidth: 160 },
      { field: 'action', title: '작업', width: 100, slots: { default: 'action' } }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          if (!selectedRoleId.value) return [];
          return await getRoleUsers(selectedRoleId.value);
        },
      },
    },
  },
});

// 롤 선택 변경 시 그리드 갱신
watch(selectedRoleId, () => {
  gridApi.query();
});

// 사용자 제거 처리
async function handleRemove(row: any) {
  try {
    await removeRoleFromUsers(selectedRoleId.value, [row.userId]);
    message.success('사용자가 롤에서 제외되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('제외 작업 실패');
  }
}

// 다중 선택 사용자 제거
async function handleBatchRemove() {
  const records = gridApi.grid?.getCheckboxRecords() || [];
  if (records.length === 0) {
    message.warning('제외할 사용자를 선택해주세요.');
    return;
  }
  try {
    const userIds = records.map((r: any) => r.userId);
    await removeRoleFromUsers(selectedRoleId.value, userIds);
    message.success('선택한 사용자들이 제외되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('제외 작업 실패');
  }
}

// 사용자 배정 모달 열기
async function openAssignModal() {
  if (!selectedRoleId.value) {
    message.warning('롤을 선택해주세요.');
    return;
  }
  try {
    const users = await getAccounts();
    const currentUsers = await getRoleUsers(selectedRoleId.value);
    const currentUserIds = currentUsers.map((cu: any) => cu.userId);

    allUsers.value = users.map((u: any) => ({
      key: u.id,
      title: `${u.userName} (${u.loginId})`,
    }));
    targetKeys.value = currentUserIds;
    showAssignModal.value = true;
  } catch (error) {
    message.error('사용자 목록을 조회할 수 없습니다.');
  }
}

// 사용자 배정 완료
async function handleAssign() {
  try {
    const currentUsers = await getRoleUsers(selectedRoleId.value);
    const currentUserIds = currentUsers.map((cu: any) => cu.userId);
    // 새로 추가된 사용자 추출
    const addedIds = targetKeys.value.filter(id => !currentUserIds.includes(id));
    // 제외된 사용자 추출
    const removedIds = currentUserIds.filter(id => !targetKeys.value.includes(id));

    if (addedIds.length > 0) {
      await assignRoleToUsers(selectedRoleId.value, addedIds);
    }
    if (removedIds.length > 0) {
      await removeRoleFromUsers(selectedRoleId.value, removedIds);
    }

    message.success('배정 정보가 업데이트되었습니다.');
    showAssignModal.value = false;
    gridApi.query();
  } catch (error) {
    message.error('배정 작업 실패');
  }
}

onMounted(() => {
  fetchRoles();
});
</script>

<template>
  <Page auto-content-height>
    <Row :gutter="16" class="h-full">
      <!-- 좌측 롤 선택 영역 -->
      <Col :span="6" class="h-full">
        <Card title="롤 목록" class="h-full" :body-style="{ padding: '12px' }">
          <List size="small" bordered :data-source="roles">
            <template #renderItem="{ item }">
              <List.Item
                :class="['cursor-pointer hover:bg-accent p-2 rounded transition-colors', { 'bg-primary text-primary-foreground hover:bg-primary/90': selectedRoleId === item.id }]"
                @click="selectedRoleId = item.id"
              >
                <div>
                  <div class="font-semibold">{{ item.name }}</div>
                  <div class="text-xs opacity-80">{{ item.code }}</div>
                </div>
              </List.Item>
            </template>
          </List>
        </Card>
      </Col>

      <!-- 우측 사용자 매핑 영역 -->
      <Col :span="18" class="h-full">
        <Grid table-title="롤 배정 사용자 목록">
          <template #toolbar-tools>
            <div class="flex gap-2">
              <Button type="primary" @click="openAssignModal">사용자 배정 설정</Button>
              <Button type="primary" danger @click="handleBatchRemove">선택 사용자 제외</Button>
            </div>
          </template>

          <template #action="{ row }">
            <Popconfirm title="해당 사용자를 롤에서 제외하시겠습니까?" @confirm="handleRemove(row)">
              <Button type="link" size="small" danger>제외</Button>
            </Popconfirm>
          </template>
        </Grid>
      </Col>
    </Row>

    <!-- 배정 모달 -->
    <Modal
      v-model:open="showAssignModal"
      title="롤 사용자 배정 설정"
      @ok="handleAssign"
      width="600px"
    >
      <div class="flex justify-center p-4">
        <Transfer
          v-model:target-keys="targetKeys"
          :data-source="allUsers"
          :render="item => item.title"
          :titles="['미배정 사용자', '배정된 사용자']"
          show-search
        />
      </div>
    </Modal>
  </Page>
</template>
