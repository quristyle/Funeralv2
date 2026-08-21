<script lang="ts" setup>
import { onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Form,
  FormItem,
  Input,
  message,
  Result,
  Textarea,
} from 'ant-design-vue';

import { sendContactUs } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

/**
 * [문의하기]
 *
 * 원본(ContactUs.vue). 로그인한 사용자의 이름·이메일을 미리 채워준다.
 */

const helpdesk = useHelpdeskStore();

const sending = ref(false);
const sent = ref(false);

const form = reactive({ email: '', message: '', name: '', subject: '' });
const errors = reactive<Record<string, string>>({});

/** 필수값과 이메일 형식을 확인한다. */
function validate() {
  Object.keys(errors).forEach((k) => delete errors[k]);

  if (!form.name.trim()) errors.name = '성함을 입력하세요.';
  if (!form.email.trim()) {
    errors.email = '이메일을 입력하세요.';
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(form.email)) {
    errors.email = '올바른 이메일 형식이 아닙니다.';
  }
  if (!form.subject.trim()) errors.subject = '제목을 입력하세요.';
  if (!form.message.trim()) errors.message = '문의 내용을 입력하세요.';

  return Object.keys(errors).length === 0;
}

async function onSubmit() {
  if (!validate()) return;

  sending.value = true;
  try {
    await sendContactUs({ ...form });
    sent.value = true;
    message.success('문의를 접수했습니다.');
  } finally {
    sending.value = false;
  }
}

function writeAgain() {
  sent.value = false;
  form.subject = '';
  form.message = '';
}

onMounted(async () => {
  await helpdesk.loadIdentity();
  if (helpdesk.identity?.userName) form.name = helpdesk.identity.userName;
});
</script>

<template>
  <Page auto-content-height>
    <Card size="small" title="문의하기">
      <Result
        v-if="sent"
        status="success"
        sub-title="담당자가 확인 후 회신드립니다."
        title="문의가 접수되었습니다"
      >
        <template #extra>
          <Button type="primary" @click="writeAgain">추가 문의하기</Button>
        </template>
      </Result>

      <Form v-else layout="vertical" style="max-width: 560px">
        <FormItem
          :help="errors.name"
          :validate-status="errors.name ? 'error' : undefined"
          label="성함"
          required
        >
          <Input v-model:value="form.name" placeholder="성함을 입력하세요" />
        </FormItem>

        <FormItem
          :help="errors.email"
          :validate-status="errors.email ? 'error' : undefined"
          label="이메일"
          required
        >
          <Input v-model:value="form.email" placeholder="mail@example.com" />
        </FormItem>

        <FormItem
          :help="errors.subject"
          :validate-status="errors.subject ? 'error' : undefined"
          label="제목"
          required
        >
          <Input v-model:value="form.subject" placeholder="제목을 입력하세요" />
        </FormItem>

        <FormItem
          :help="errors.message"
          :validate-status="errors.message ? 'error' : undefined"
          label="문의 내용"
          required
        >
          <Textarea
            v-model:value="form.message"
            :rows="6"
            placeholder="문의하실 내용을 적어주세요"
          />
        </FormItem>

        <Button :loading="sending" type="primary" @click="onSubmit">
          문의 보내기
        </Button>
      </Form>
    </Card>
  </Page>
</template>
