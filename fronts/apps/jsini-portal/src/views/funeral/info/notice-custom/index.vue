<script lang="ts" setup>
import { ref } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Form, Input, Switch, Badge, Card } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getNotices } from '#/api/funeral/info';

const [NoticeModal, noticeModalApi] = useVbenModal({
  title: '공지사항 및 알림 등록',
  destroyOnClose: true,
});

const showViewModal = ref<boolean>(false);
const viewRecord = ref<any>(null);

const formModel = ref({
  title: '',
  content: '',
  isImportant: false,
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      {
        field: 'isImportant',
        title: '중요도',
        width: 100,
        slots: { default: 'important-badge' }
      },
      { field: 'title', title: '제목', minWidth: 200 },
      { field: 'author', title: '작성자', minWidth: 100 },
      { field: 'createdAt', title: '등록일자', minWidth: 160 },
      {
        field: 'action',
        title: '관리',
        width: 150,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getNotices();
        },
      },
    },
  },
});

function openCreate() {
  formModel.value = {
    title: '',
    content: '',
    isImportant: false,
  };
  noticeModalApi.open();
}

function handleView(row: any) {
  viewRecord.value = row;
  showViewModal.value = true;
}

// 가상 저장 트리거
function handleSave() {
  if (!formModel.value.title || !formModel.value.content) {
    message.warning('제목과 내용은 필수 사항입니다.');
    return;
  }
  message.success('신규 공지사항이 등록되었습니다.');
  noticeModalApi.close();
  gridApi.query();
}
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="관내 긴급 알림 및 공지사항 목록">
      <template #toolbar-tools>
        <Button v-perm:create type="primary" @click="openCreate">
          <Plus class="size-5 mr-1" />
          알림 등록
        </Button>
      </template>

      <template #important-badge="{ row }">
        <Badge
          :status="row.isImportant ? 'error' : 'default'"
          :text="row.isImportant ? '중요' : '일반'"
        />
      </template>

      <template #action="{ row }">
        <div class="flex gap-2">
          <Button type="link" size="small" @click="handleView(row)">상세보기</Button>
        </div>
      </template>
    </Grid>

    <!-- 작성 모달 -->
    <NoticeModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="알림 제목" required>
            <Input v-model:value="formModel.title" placeholder="공지 제목 입력" />
          </Form.Item>
          <Form.Item label="중요도 설정">
            <div class="flex items-center gap-2">
              <Switch v-model:value="formModel.isImportant" />
              <span class="text-xs text-muted-foreground">활성화 시 최상단 고정 및 강조 표기됩니다.</span>
            </div>
          </Form.Item>
          <Form.Item label="알림 세부 본문" required>
            <Input.TextArea v-model:value="formModel.content" :rows="6" placeholder="안내 본문 내용 입력" />
          </Form.Item>
        </Form>
      </div>
    </NoticeModal>

    <!-- 상세보기 팝업 -->
    <Card
      v-if="showViewModal"
      class="fixed inset-0 m-auto w-[500px] h-[400px] z-50 border shadow-2xl flex flex-col justify-between"
      :title="viewRecord?.title"
    >
      <template #extra>
        <Button type="text" @click="showViewModal = false">닫기</Button>
      </template>
      
      <div class="flex-1 p-2 overflow-y-auto text-sm space-y-4">
        <div class="flex justify-between text-xs text-muted-foreground border-b pb-2">
          <span>작성자: {{ viewRecord?.author }}</span>
          <span>등록일: {{ viewRecord?.createdAt }}</span>
        </div>
        <p class="whitespace-pre-wrap leading-relaxed text-foreground">{{ viewRecord?.content }}</p>
      </div>
    </Card>
  </Page>
</template>
