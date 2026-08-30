<script lang="ts" setup>
import type { LifeBirthdayApi } from '#/api/life/birthday';

import { ref } from 'vue';

import { useVbenModal } from '@vben/common-ui';

import { Avatar, message, Textarea } from 'ant-design-vue';

import { sendBirthdayMessage } from '#/api/life/birthday';

/**
 * [생일 축하 메시지 보내기 팝업]
 *
 * 원본(GHUB BirthdayMessageDialog.vue — CommonModal 기반)을 useVbenModal 로 옮겼다.
 * 팝업 데이터로 생일자(Person)를 받아, 열릴 때 기본 문구를 채워 준다.
 */

const emit = defineEmits<{ (e: 'sent'): void }>();

const person = ref<LifeBirthdayApi.Person | null>(null);
const content = ref('');
const sending = ref(false);

const [Modal, modalApi] = useVbenModal<LifeBirthdayApi.Person>({
  destroyOnClose: true,
  onConfirm: onSend,
  onOpenChange(isOpen) {
    if (!isOpen) return;
    person.value = modalApi.getData() ?? null;
    // 열릴 때마다 기본 문구로 초기화 (원본과 동일)
    content.value = '생일 축하합니다! 행복한 하루 되세요 🎉';
  },
});

async function onSend() {
  if (!person.value) return;
  if (!content.value.trim()) {
    message.warning('메시지 내용을 입력해주세요.');
    return;
  }

  sending.value = true;
  modalApi.lock();
  try {
    await sendBirthdayMessage(person.value.subjectId, content.value);
    message.success('축하 메시지를 보냈습니다!');
    modalApi.close();
    emit('sent');
  } finally {
    sending.value = false;
    modalApi.lock(false);
  }
}
</script>

<template>
  <Modal :confirm-loading="sending" title="생일 축하 메시지 보내기">
    <div class="px-4 pt-2">
      <div
        v-if="person"
        class="mb-4 flex flex-row items-center justify-center gap-4 text-center"
      >
        <Avatar :size="56">
          {{ person.name?.charAt(0) }}
        </Avatar>
        <div class="text-left">
          <p class="text-sm text-muted-foreground">
            {{ person.departmentName || person.companyName || '임직원' }}
          </p>
          <p class="text-lg font-bold">{{ person.name }} 님에게</p>
        </div>
      </div>

      <Textarea
        v-model:value="content"
        :maxlength="200"
        :rows="4"
        placeholder="축하 메시지를 입력하세요..."
        show-count
      />
    </div>
  </Modal>
</template>
