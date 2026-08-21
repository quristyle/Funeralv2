<script lang="ts" setup>
import { ref } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Form, Input, Switch, Card } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getQnas, createQna } from '#/api/funeral/help';

const [QnaModal, qnaModalApi] = useVbenModal({
  title: '새 Q&A 질문 등록',
  destroyOnClose: true,
});

const showViewModal = ref<boolean>(false);
const viewRecord = ref<any>(null);

const formModel = ref({
  question: '',
  isPublic: true,
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'question', title: '질문 본문', minWidth: 250 },
      {
        field: 'isPublic',
        title: '공개 구분',
        width: 120,
        formatter: ({ cellValue }: { cellValue: any }) => (cellValue ? '공개 질문' : '비공개')
      },
      { field: 'authorName', title: '작성자', width: 120 },
      { field: 'createdAt', title: '등록일자', width: 160 },
      {
        field: 'action',
        title: '답변 확인',
        width: 150,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getQnas();
        },
      },
    },
  },
});

function handleOpenCreate() {
  formModel.value = { question: '', isPublic: true };
  qnaModalApi.open();
}

function handleView(row: any) {
  if (!row.isPublic) {
    message.warning('비공개 질문은 권한이 있는 본인만 볼 수 있습니다.');
  }
  viewRecord.value = row;
  showViewModal.value = true;
}

async function handleSave() {
  try {
    if (!formModel.value.question) {
      message.warning('질문을 입력해주세요.');
      return;
    }
    await createQna(formModel.value);
    message.success('질문이 등록되었습니다. 빠른 시일 내에 운영팀에서 답변을 남기겠습니다.');
    qnaModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('등록 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="자유 Q&A 질문게시판">
      <template #toolbar-tools>
        <Button type="primary" @click="handleOpenCreate">
          <Plus class="size-5 mr-1" />
          질문 올리기
        </Button>
      </template>

      <template #action="{ row }">
        <Button type="link" size="small" @click="handleView(row)">답변 조회</Button>
      </template>
    </Grid>

    <QnaModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="질문 내용" required>
            <Input.TextArea v-model:value="formModel.question" :rows="5" placeholder="궁금하신 사항을 자유롭게 입력해 주세요." />
          </Form.Item>
          <Form.Item label="공개 여부">
            <div class="flex items-center gap-2">
              <Switch v-model:value="formModel.isPublic" />
              <span class="text-xs text-muted-foreground">비공개 설정 시 작성자와 운영자만 열람이 가능합니다.</span>
            </div>
          </Form.Item>
        </Form>
      </div>
    </QnaModal>

    <!-- Q&A 보기 카드 -->
    <Card
      v-if="showViewModal"
      class="fixed inset-0 m-auto w-[550px] h-[350px] z-50 border shadow-2xl flex flex-col justify-between"
      title="Q&A 답변 상세조회"
    >
      <template #extra>
        <Button type="text" @click="showViewModal = false">닫기</Button>
      </template>
      
      <div class="flex-1 p-2 overflow-y-auto space-y-4 text-sm">
        <div class="border-b pb-2">
          <div class="font-bold text-primary">Q. 질문 내용</div>
          <div class="text-foreground mt-1">{{ viewRecord?.question }}</div>
        </div>

        <div class="pt-2">
          <div class="font-bold text-green-600">A. 운영자 답변</div>
          <div class="bg-muted p-3 rounded mt-2 text-xs leading-relaxed text-foreground font-semibold font-sans">
            {{ viewRecord?.answer || '현재 검토 중인 질문입니다. 성심껏 답변 준비 중에 있습니다.' }}
          </div>
        </div>
      </div>
    </Card>
  </Page>
</template>
