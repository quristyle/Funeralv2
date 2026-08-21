<script lang="ts" setup>
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute, useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';

import {
  Button,
  Card,
  Form,
  FormItem,
  Input,
  message,
  Space,
  Spin,
} from 'ant-design-vue';

import { createNotice, getNotice, updateNotice } from '#/api/helpdesk';

import RichTextInput from '../request/modules/rich-text-input.vue';
import HelpdeskAccountNotice from '../shared/account-notice.vue';

/**
 * [공지 작성 · 수정]
 *
 * 원본(NoticeForm.vue)은 빈 스텁이었고 목록 화면에서 인라인으로만 편집할 수 있었다.
 * 라우트가 있으니 제대로 된 작성 화면으로 구현했다.
 * 경로의 id 가 'new' 이거나 없으면 신규 작성이다.
 */

const route = useRoute();
const router = useRouter();

const loading = ref(false);
const saving = ref(false);

const noticeId = computed(() => {
  const raw = route.params.id;
  return raw && raw !== 'new' ? Number(raw) : undefined;
});
const isEdit = computed(() => noticeId.value !== undefined);

const form = reactive({ content: '', title: '' });

async function load() {
  if (!noticeId.value) return;

  loading.value = true;
  try {
    const data = await getNotice(noticeId.value);
    form.title = data?.title ?? '';
    form.content = data?.content ?? '';
  } finally {
    loading.value = false;
  }
}

async function save() {
  if (!form.title.trim()) {
    message.warning('제목을 입력하세요.');
    return;
  }

  saving.value = true;
  try {
    if (isEdit.value) {
      await updateNotice(noticeId.value!, { ...form });
      message.success('공지를 수정했습니다.');
      router.replace(`/helpdesk/notice/view/${noticeId.value}`);
    } else {
      const created = await createNotice({ ...form });
      message.success('공지를 등록했습니다.');
      router.replace(
        created?.id
          ? `/helpdesk/notice/view/${created.id}`
          : '/helpdesk/notice/list',
      );
    }
  } finally {
    saving.value = false;
  }
}

onMounted(load);
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Spin :spinning="loading">
      <Card :title="isEdit ? '공지 수정' : '공지 작성'" size="small">
        <template #extra>
          <Space>
            <Button size="small" @click="router.back()">취소</Button>
            <Button
              :loading="saving"
              size="small"
              type="primary"
              @click="save"
            >
              저장
            </Button>
          </Space>
        </template>

        <Form layout="vertical">
          <FormItem label="제목" required>
            <Input v-model:value="form.title" placeholder="제목을 입력하세요" />
          </FormItem>
          <FormItem label="내용">
            <RichTextInput
              v-model="form.content"
              :min-height="320"
              placeholder="내용을 입력하세요. 이미지는 붙여넣기로 넣을 수 있습니다."
            />
          </FormItem>
        </Form>
      </Card>
    </Spin>
  </Page>
</template>
