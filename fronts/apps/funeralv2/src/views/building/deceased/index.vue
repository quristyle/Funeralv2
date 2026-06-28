<script lang="ts" setup>
import { ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Tag } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getDeceasedList, deleteDeceased } from '#/api/building';
import DeceasedFormModal from './modules/deceased-form-modal.vue';

const formModalRef = ref<InstanceType<typeof DeceasedFormModal> | null>(null);

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '고인명', minWidth: 100 },
      {
        field: 'gender',
        title: '성별',
        minWidth: 80,
        formatter: ({ cellValue }: { cellValue: any }) => (cellValue === 'MALE' ? '남성' : '여성')
      },
      { field: 'age', title: '연세', minWidth: 80, formatter: ({ cellValue }: { cellValue: any }) => `${cellValue}세` },
      { field: 'religion', title: '종교', minWidth: 100 },
      { field: 'roomName', title: '배정 빈소', minWidth: 120 },
      { field: 'deathDate', title: '작고 일시', minWidth: 160, formatter: ({ cellValue }: { cellValue: any }) => formatDate(cellValue) },
      {
        field: 'status',
        title: '장례 상태',
        minWidth: 120,
        slots: { default: 'status-tag' }
      },
      {
        field: 'action',
        title: '작업',
        width: 150,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getDeceasedList();
        },
      },
    },
  },
});

function onCreate() {
  if (formModalRef.value) {
    formModalRef.value.open();
  }
}

function onEdit(row: any) {
  if (formModalRef.value) {
    formModalRef.value.open(row);
  }
}

async function onDelete(row: any) {
  try {
    await deleteDeceased(row.id);
    message.success('고인 정보가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

function formatDate(dateStr?: string) {
  if (!dateStr) return '-';
  try {
    return new Date(dateStr).toLocaleString('ko-KR');
  } catch {
    return dateStr;
  }
}
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="장례식장 고인(Deceased) 등록 목록">
      <template #toolbar-tools>
        <Button type="primary" @click="onCreate">
          <Plus class="size-5 mr-1" />
          신규 고인 등록
        </Button>
      </template>

      <template #status-tag="{ row }">
        <Tag v-if="row.status === 'IN_HOSPITAL'" color="processing">장례 진행중</Tag>
        <Tag v-else-if="row.status === 'DISCHARGED'" color="warning">발인 완료</Tag>
        <Tag v-else color="success">정산 완료</Tag>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onEdit(row)">수정</Button>
          <Popconfirm title="해당 고인 데이터를 영구 삭제하시겠습니까?" @confirm="onDelete(row)">
            <Button type="link" size="small" danger>삭제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <!-- 고인 정보 입력 폼 모달 (독립 분리 컴포넌트) -->
    <DeceasedFormModal ref="formModalRef" @saved="gridApi.query()" />
  </Page>
</template>
