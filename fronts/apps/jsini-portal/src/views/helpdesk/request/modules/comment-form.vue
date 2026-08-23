<script lang="ts" setup>
import { ref } from 'vue';

import { Button, message } from 'ant-design-vue';

import { RichEditor } from '#/components/rich-editor';

/** 댓글 · 답글 입력 폼 */
withDefaults(
  defineProps<{ placeholder?: string; submitLabel?: string }>(),
  { placeholder: '댓글을 입력하세요. 이미지는 붙여넣기로 첨부할 수 있습니다.', submitLabel: '댓글 등록' },
);

const emit = defineEmits<{ submit: [text: string] }>();

const text = ref('');
const inputRef = ref<InstanceType<typeof RichEditor> | null>(null);
const submitting = ref(false);

async function onSubmit() {
  if (!text.value.trim()) {
    message.warning('내용을 입력하세요.');
    return;
  }

  submitting.value = true;
  try {
    emit('submit', text.value);
    inputRef.value?.clear();
  } finally {
    submitting.value = false;
  }
}
</script>

<template>
  <div>
    <!-- 댓글은 짧게 쓰는 자리라 도구 모음을 한 줄짜리로 줄인다.
         글자를 선택하면 뜨는 서식 메뉴와 이미지 붙여넣기는 그대로 쓸 수 있다. -->
    <RichEditor
      ref="inputRef"
      v-model="text"
      :min-height="90"
      :placeholder="placeholder"
      biz-type="helpdesk-comment"
      toolbar="compact"
    />
    <div class="mt-2 flex justify-end">
      <Button :loading="submitting" size="small" type="primary" @click="onSubmit">
        {{ submitLabel }}
      </Button>
    </div>
  </div>
</template>
