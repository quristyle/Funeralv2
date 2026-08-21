<script lang="ts" setup>
import { ref } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Form, Input, Card, Descriptions } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getInquiries, createInquiry } from '#/api/funeral/help';

const [InquiryModal, inquiryModalApi] = useVbenModal({
  title: '1:1 문의사항 작성',
  destroyOnClose: true,
});

const showDetailModal = ref<boolean>(false);
const detailRecord = ref<any>(null);

const formModel = ref({
  title: '',
  content: '',
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'title', title: '문의 제목', minWidth: 200 },
      { field: 'authorName', title: '작성자', minWidth: 100 },
      { field: 'createdAt', title: '작성일시', minWidth: 160 },
      {
        field: 'status',
        title: '처리 상태',
        minWidth: 120,
        slots: { default: 'status-tag' }
      },
      {
        field: 'action',
        title: '상세',
        width: 120,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    height: 'auto',
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getInquiries();
        },
      },
    },
  },
});

function handleOpenCreate() {
  formModel.value = { title: '', content: '' };
  inquiryModalApi.open();
}

function handleView(row: any) {
  detailRecord.value = row;
  showDetailModal.value = true;
}

async function handleSave() {
  try {
    if (!formModel.value.title || !formModel.value.content) {
      message.warning('제목과 내용을 모두 기입해 주세요.');
      return;
    }
    await createInquiry(formModel.value);
    message.success('문의사항이 안전하게 접수되었습니다. 담당자 확인 후 답변 드리겠습니다.');
    inquiryModalApi.close();
    gridApi.query();
  } catch (error) {
    message.error('접수 실패');
  }
}
</script>

<template>
  <Page auto-content-height>
    <Grid table-title="1:1 고객 기술 문의 내역">
      <template #toolbar-tools>
        <Button type="primary" @click="handleOpenCreate">
          <Plus class="size-5 mr-1" />
          신규 문의 접수
        </Button>
      </template>

      <template #status-tag="{ row }">
        <span
          v-if="row.status === 'ANSWERED'"
          class="px-2 py-1 rounded text-xs font-semibold bg-green-100 text-green-800"
        >
          답변 완료
        </span>
        <span
          v-else
          class="px-2 py-1 rounded text-xs font-semibold bg-yellow-100 text-yellow-800"
        >
          접수/대기
        </span>
      </template>

      <template #action="{ row }">
        <Button type="link" size="small" @click="handleView(row)">답변 보기</Button>
      </template>
    </Grid>

    <!-- 문의 작성 모달 -->
    <InquiryModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="문의 사항 제목" required>
            <Input v-model:value="formModel.title" placeholder="어떤 부분이 불편하신가요?" />
          </Form.Item>
          <Form.Item label="상세 접수 내용" required>
            <Input.TextArea v-model:value="formModel.content" :rows="6" placeholder="상세 장애 현상 및 동작 오류 환경을 기술해 주세요." />
          </Form.Item>
        </Form>
      </div>
    </InquiryModal>

    <!-- 답변 확인 레이오프 카드 -->
    <Card
      v-if="showDetailModal"
      class="fixed inset-0 m-auto w-[550px] h-[450px] z-50 border shadow-2xl flex flex-col justify-between"
      :title="detailRecord?.title"
    >
      <template #extra>
        <Button type="text" @click="showDetailModal = false">닫기</Button>
      </template>
      
      <div class="flex-1 p-2 overflow-y-auto space-y-4">
        <Descriptions :column="1" bordered size="small">
          <Descriptions.Item label="질문 내용">{{ detailRecord?.content }}</Descriptions.Item>
          <Descriptions.Item label="처리상태">
            {{ detailRecord?.status === 'ANSWERED' ? '답변 완료' : '대기중' }}
          </Descriptions.Item>
          <Descriptions.Item label="관리자 답변" v-if="detailRecord?.status === 'ANSWERED'">
            <div class="bg-muted p-2 rounded text-xs leading-relaxed text-foreground font-semibold font-sans">
              {{ detailRecord?.answer }}
            </div>
          </Descriptions.Item>
        </Descriptions>
      </div>
    </Card>
  </Page>
</template>
