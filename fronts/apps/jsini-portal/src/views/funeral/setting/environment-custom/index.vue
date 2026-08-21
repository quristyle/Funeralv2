<script lang="ts" setup>
import { ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, message, Input } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getEnvironmentSettings, updateEnvironmentSetting } from '#/api/setting';

const editKey = ref<string>('');
const editValue = ref<string>('');

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'groupName', title: '설정 그룹', width: 150 },
      { field: 'key', title: '설정 변수 키', minWidth: 180 },
      {
        field: 'value',
        title: '설정 값',
        minWidth: 200,
        slots: { default: 'value-edit' }
      },
      { field: 'description', title: '설명', minWidth: 250 },
      { field: 'updatedAt', title: '최종 갱신일자', width: 160 },
      {
        field: 'action',
        title: '수정',
        width: 150,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getEnvironmentSettings();
        },
      },
    },
  },
});

function handleStartEdit(row: any) {
  editKey.value = row.key;
  editValue.value = row.value;
}

function handleCancelEdit() {
  editKey.value = '';
  editValue.value = '';
}

async function handleSaveEdit(row: any) {
  try {
    if (!editValue.value) {
      message.warning('설정 값을 채워주세요.');
      return;
    }
    await updateEnvironmentSetting(row.key, editValue.value);
    message.success(`${row.key} 설정 값이 수정되었습니다.`);
    editKey.value = '';
    gridApi.query();
  } catch (error) {
    message.error('설정 값 변경 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <div class="mb-4 bg-card p-4 rounded border flex justify-between items-center">
      <span class="font-semibold text-sm">⚙️ 시스템 전역 설정 변수(Configuration) 조정 센터</span>
      <Button type="primary" @click="gridApi.query()">설정 새로고침</Button>
    </div>

    <Grid table-title="서버 환경 변수 및 임계 설정 목록">
      <template #value-edit="{ row }">
        <div v-if="editKey === row.key">
          <Input v-model:value="editValue" size="small" style="width: 100%" />
        </div>
        <span v-else class="font-mono text-xs">{{ row.value }}</span>
      </template>

      <template #action="{ row }">
        <div v-if="editKey === row.key" class="flex gap-2">
          <Button type="link" size="small" @click="handleSaveEdit(row)">저장</Button>
          <Button type="link" size="small" @click="handleCancelEdit">취소</Button>
        </div>
        <Button v-else type="link" size="small" @click="handleStartEdit(row)">값 수정</Button>
      </template>
    </Grid>
  </Page>
</template>
