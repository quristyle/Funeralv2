<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Plus } from '@vben/icons';
import { Button, message, Form, Input, Select, Collapse, CollapsePanel, Popconfirm } from 'ant-design-vue';
import { getFaqs, createFaq, updateFaq, deleteFaq } from '#/api/funeral/help';

const activeKey = ref<string[]>([]);
const list = ref<any[]>([]);
const loading = ref<boolean>(false);

const [FaqModal, faqModalApi] = useVbenModal({
  title: '자주 묻는 질문(FAQ) 설정',
  destroyOnClose: true,
});

const formModel = ref({
  id: '',
  question: '',
  answer: '',
  category: 'GENERAL', // GENERAL, SYSTEM, BILLING
  sortOrder: 1
});

async function fetchFaqs() {
  loading.value = true;
  try {
    const data = await getFaqs();
    list.value = data || [];
  } catch (error) {
    message.error('FAQ 리스트 조회 실패');
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  formModel.value = { id: '', question: '', answer: '', category: 'GENERAL', sortOrder: 1 };
  faqModalApi.open();
}

function onEdit(item: any) {
  formModel.value = { ...item };
  faqModalApi.open();
}

async function onDelete(id: string) {
  try {
    await deleteFaq(id);
    message.success('FAQ가 삭제되었습니다.');
    fetchFaqs();
  } catch (error) {
    message.error('삭제 실패');
  }
}

async function handleSave() {
  try {
    if (!formModel.value.question || !formModel.value.answer) {
      message.warning('질문과 답변은 필수 기입 사항입니다.');
      return;
    }
    if (formModel.value.id) {
      await updateFaq(formModel.value.id, formModel.value);
      message.success('FAQ 정보가 수정되었습니다.');
    } else {
      await createFaq(formModel.value);
      message.success('FAQ가 성공적으로 등록되었습니다.');
    }
    faqModalApi.close();
    fetchFaqs();
  } catch (error) {
    message.error('저장 실패');
  }
}

onMounted(() => {
  fetchFaqs();
});
</script>

<template>
  <Page auto-content-height>
    <div class="mb-4 bg-card p-4 rounded border flex justify-between items-center">
      <span class="font-bold text-sm">자주 묻는 질문 (FAQ) 아코디언 설정</span>
      <Button v-perm:create type="primary" @click="openCreate">
        <Plus class="size-5 mr-1" />
        FAQ 항목 추가
      </Button>
    </div>

    <!-- FAQ 아코디언 리스트 -->
    <Collapse v-model:activeKey="activeKey" class="bg-card">
      <CollapsePanel v-for="item in list" :key="item.id">
        <template #header>
          <div class="flex items-center justify-between w-full pr-4 text-sm font-semibold">
            <span>
              <span class="text-primary font-bold mr-2">[{{ item.category }}]</span>
              {{ item.question }}
            </span>
          </div>
        </template>

        <!-- 아코디언 본문 내용 및 관리자 액션 제공 -->
        <div class="p-4 space-y-4 text-xs">
          <div class="bg-muted p-3 rounded leading-relaxed text-foreground font-semibold font-sans whitespace-pre-wrap">
            {{ item.answer }}
          </div>
          
          <div class="flex gap-2 justify-end">
            <Button v-perm:update size="small" type="default" @click.stop="onEdit(item)">수정</Button>
            <Popconfirm title="해당 FAQ 항목을 삭제하시겠습니까?" @confirm="onDelete(item.id)">
              <Button v-perm:delete size="small" type="primary" danger @click.stop>삭제</Button>
            </Popconfirm>
          </div>
        </div>
      </CollapsePanel>
    </Collapse>

    <FaqModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="분류 카테고리" required>
            <Select v-model:value="formModel.category">
              <Select.Option value="GENERAL">일반 사항</Select.Option>
              <Select.Option value="SYSTEM">시스템 장애/사용법</Select.Option>
              <Select.Option value="BILLING">요금 수납/정산</Select.Option>
            </Select>
          </Form.Item>
          <Form.Item label="자주 묻는 질문(Q)" required>
            <Input v-model:value="formModel.question" placeholder="질문 항목 입력" />
          </Form.Item>
          <Form.Item label="답변 내용(A)" required>
            <Input.TextArea v-model:value="formModel.answer" :rows="5" placeholder="답변 텍스트 입력" />
          </Form.Item>
        </Form>
      </div>
    </FaqModal>
  </Page>
</template>
