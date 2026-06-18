<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { Page, useVbenVxeGrid } from '@vben/common-ui';
import { Button, Card, Col, Row, Space, Popconfirm, message } from 'ant-design-vue';
import { PlusOutlined, EditOutlined, DeleteOutlined } from '@vben/icons';
import { groupGridOptions, codeGridOptions } from './data';
import { 
  getCommonCodeGroups, 
  getCommonCodes, 
  deleteCommonCode 
} from '#/api/system/common-code';
import GroupForm from './modules/group-form.vue';
import CodeForm from './modules/code-form.vue';

const currentGroup = ref<any>(null);
const groupFormRef = ref();
const codeFormRef = ref();

/**
 * 그룹 그리드 설정
 */
const [GroupGrid, groupGridApi] = useVbenVxeGrid({
  gridOptions: groupGridOptions,
  onRowClick: ({ row }) => {
    currentGroup.value = row;
    loadCodes();
  },
});

/**
 * 코드 그리드 설정
 */
const [CodeGrid, codeGridApi] = useVbenVxeGrid({
  gridOptions: codeGridOptions,
});

/**
 * 그룹 데이터 로드
 */
async function loadGroups() {
  groupGridApi.setLoading(true);
  try {
    const data = await getCommonCodeGroups();
    groupGridApi.setGridOptions({ data });
    if (data.length > 0 && !currentGroup.value) {
      currentGroup.value = data[0];
      loadCodes();
    }
  } finally {
    groupGridApi.setLoading(false);
  }
}

/**
 * 코드 데이터 로드
 */
async function loadCodes() {
  if (!currentGroup.value) return;
  codeGridApi.setLoading(true);
  try {
    const data = await getCommonCodes(
      currentGroup.value.groupCode, 
      currentGroup.value.isHierarchical
    );
    codeGridApi.setGridOptions({ data });
  } finally {
    codeGridApi.setLoading(false);
  }
}

/**
 * 삭제 처리
 */
async function handleDelete(id: string) {
  await deleteCommonCode(id);
  message.success('코드가 삭제되었습니다.');
  loadCodes();
}

onMounted(() => {
  loadGroups();
});
</script>

<template>
  <Page title="공통코드 관리" description="시스템에서 사용하는 공통코드를 관리합니다.">
    <Row :gutter="16">
      <!-- 왼쪽: 코드 그룹 목록 -->
      <Col :span="8">
        <Card title="코드 그룹" :bordered="false">
          <template #extra>
            <Button type="primary" :icon="h(PlusOutlined)" @click="groupFormRef.openModal()">
              추가
            </Button>
          </template>
          <GroupGrid />
        </Card>
      </Col>

      <!-- 오른쪽: 세부 코드 목록 -->
      <Col :span="16">
        <Card :title="currentGroup ? `[${currentGroup.groupName}] 코드 목록` : '코드 목록'" :bordered="false">
          <template #extra v-if="currentGroup">
            <Button type="primary" :icon="h(PlusOutlined)" @click="codeFormRef.openModal(currentGroup.id)">
              코드 추가
            </Button>
          </template>
          
          <CodeGrid v-if="currentGroup">
            <template #action="{ row }">
              <Space>
                <Button 
                  v-if="currentGroup.isHierarchical"
                  type="link" 
                  size="small" 
                  @click="codeFormRef.openModal(currentGroup.id, null, row.id)"
                >
                  하위추가
                </Button>
                <Button type="link" size="small" @click="codeFormRef.openModal(currentGroup.id, row)">
                  <EditOutlined />
                </Button>
                <Popconfirm title="정말 삭제하시겠습니까?" @confirm="handleDelete(row.id)">
                  <Button type="link" size="small" danger>
                    <DeleteOutlined />
                  </Button>
                </Popconfirm>
              </Space>
            </template>
          </CodeGrid>
          <div v-else class="flex h-64 items-center justify-center text-gray-400">
            왼쪽에서 그룹을 선택해주세요.
          </div>
        </Card>
      </Col>
    </Row>

    <!-- 모달 컴포넌트 -->
    <GroupForm ref="groupFormRef" @success="loadGroups" />
    <CodeForm ref="codeFormRef" @success="loadCodes" />
  </Page>
</template>

<style scoped>
:deep(.vxe-grid) {
  height: 600px;
}
</style>
