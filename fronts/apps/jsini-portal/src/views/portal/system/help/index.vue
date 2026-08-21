<script lang="ts" setup>
import { ref } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, InputNumber, Select } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getHelpDocs, createHelpDoc, updateHelpDoc, deleteHelpDoc } from '#/api/portal/system/help';

const [HelpModal, helpModalApi] = useVbenModal({
  title: '도움말 문서 등록 및 편집',
  destroyOnClose: true,
});

const formModel = ref({
  id: '',
  title: '',
  category: 'SYSTEM', // SYSTEM, BUILDING, BILLING, HELP
  content: '',
  sortOrder: 1,
  status: 'PUBLISHED' as 'PUBLISHED' | 'DRAFT'
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'title', title: '제목', minWidth: 200 },
      {
        field: 'category',
        title: '카테고리',
        minWidth: 120,
        formatter: ({ cellValue }: { cellValue: any }) => {
          if (cellValue === 'SYSTEM') return '시스템 가이드';
          if (cellValue === 'BUILDING') return '건물/장비 관리';
          if (cellValue === 'BILLING') return '과금/통계 안내';
          return '일반 문의 가이드';
        }
      },
      { field: 'sortOrder', title: '정렬 순서', minWidth: 100 },
      {
        field: 'status',
        title: '배포 상태',
        minWidth: 120,
        slots: { default: 'status-tag' }
      },
      { field: 'updatedAt', title: '최종 수정일자', minWidth: 160 },
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
          return await getHelpDocs();
        },
      },
    },
  },
});

function onCreate() {
  formModel.value = {
    id: '',
    title: '',
    category: 'SYSTEM',
    content: '',
    sortOrder: 1,
    status: 'PUBLISHED'
  };
  helpModalApi.open();
}

function onEdit(row: any) {
  formModel.value = { ...row };
  helpModalApi.open();
}

async function onDelete(row: any) {
  try {
    await deleteHelpDoc(row.id);
    message.success('도움말 문서가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

async function handleSave() {
  try {
    if (!formModel.value.title || !formModel.value.content) {
      message.warning('제목과 내용은 필수 사항입니다.');
      return;
    }
    if (formModel.value.id) {
      await updateHelpDoc(formModel.value.id, formModel.value);
      message.success('도움말 정보가 수정되었습니다.');
    } else {
      await createHelpDoc(formModel.value);
      message.success('도움말이 등록되었습니다.');
    }
    helpModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="시스템 매뉴얼 및 도움말 관리 대시보드">
      <template #toolbar-tools>
        <Button v-perm:create type="primary" @click="onCreate">
          <Plus class="size-5 mr-1" />
          신규 도움말 작성
        </Button>
      </template>

      <template #status-tag="{ row }">
        <span
          :class="['px-2 py-1 rounded text-xs font-semibold', row.status === 'PUBLISHED' ? 'bg-green-100 text-green-800' : 'bg-yellow-100 text-yellow-800']"
        >
          {{ row.status === 'PUBLISHED' ? '배포 완료' : '임시 저장' }}
        </span>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="onEdit(row)">수정</Button>
          <Popconfirm title="해당 도움말 문서를 삭제하시겠습니까?" @confirm="onDelete(row)">
            <Button v-perm:delete type="link" size="small" danger>삭제</Button>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <HelpModal @ok="handleSave" class="w-[700px]">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="도움말 카테고리" required>
            <Select v-model:value="formModel.category">
              <Select.Option value="SYSTEM">시스템 가이드</Select.Option>
              <Select.Option value="BUILDING">건물/장비 관리</Select.Option>
              <Select.Option value="BILLING">과금/통계 안내</Select.Option>
              <Select.Option value="HELP">일반 문의 가이드</Select.Option>
            </Select>
          </Form.Item>
          
          <Form.Item label="문서 제목" required>
            <Input v-model:value="formModel.title" placeholder="도움말 제목 입력" />
          </Form.Item>

          <Form.Item label="문서 본문 내용" required>
            <Input.TextArea v-model:value="formModel.content" :rows="8" placeholder="사용자 안내 가이드를 기술하세요" />
          </Form.Item>

          <div class="grid grid-cols-2 gap-4">
            <Form.Item label="출력 정렬 순서">
              <InputNumber v-model:value="formModel.sortOrder" :min="1" style="width: 100%" />
            </Form.Item>
            <Form.Item label="공개 여부">
              <Select v-model:value="formModel.status">
                <Select.Option value="PUBLISHED">배포 완료</Select.Option>
                <Select.Option value="DRAFT">임시 저장 (숨김)</Select.Option>
              </Select>
            </Form.Item>
          </div>
        </Form>
      </div>
    </HelpModal>
  </Page>
</template>
