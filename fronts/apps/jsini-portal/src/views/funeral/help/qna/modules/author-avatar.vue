<script lang="ts" setup>
import { computed, ref, watch } from 'vue';

import { Avatar } from 'ant-design-vue';

/**
 * [작성자 아바타]
 *
 * 사진이 있으면 사진을, 없으면 이름 첫 글자를 동그라미에 그린다.
 * 헬프데스크 댓글(`views/helpdesk/request/modules/comment-item.vue`)과 같은 방식이다.
 *
 * 사진 주소는 서버가 계정에서 읽어 내려준다. 원본은 프로필에 올린 파일이라 클 수 있어서
 * **썸네일 경로로 바꿔 쓴다** — 목록에 답글이 여러 개면 원본을 그만큼 내려받게 된다.
 * 레이아웃 헤더(`layouts/basic.vue`)가 쓰는 것과 같은 규칙이다.
 */

const props = defineProps<{
  name?: null | string;
  /** 프로필 사진 주소. 없으면 이름 첫 글자로 그린다. */
  photo?: null | string;
  size?: number;
}>();

/** 사진을 못 받아 온 경우. 그때는 첫 글자로 되돌린다. */
const broken = ref(false);

// 다른 사람의 아바타로 바뀌면 다시 시도한다.
watch(
  () => props.photo,
  () => {
    broken.value = false;
  },
);

const src = computed(() => {
  if (broken.value) return undefined;

  const raw = props.photo?.trim();
  if (!raw) return undefined;

  return raw.includes('/api/file/download/')
    ? raw.replace('/api/file/download/', '/api/file/thumbnail/')
    : raw;
});

/**
 * 사진이 없을 때 그릴 글자.
 * 조직도(`views/portal/system/company-user/org-chart.vue`)의 `avatarText` 와 같은 규칙이다.
 */
const initial = computed(
  () => (props.name ?? '?').trim().charAt(0).toUpperCase() || '?',
);

/**
 * 이름마다 다른 색을 준다. 같은 사람은 늘 같은 색이라 스레드에서 누가 썼는지
 * 이름을 읽지 않고도 눈에 들어온다.
 */
const tone = computed(() => {
  const palette = [
    '#1677ff',
    '#52c41a',
    '#fa8c16',
    '#eb2f96',
    '#722ed1',
    '#13c2c2',
  ];
  const name = props.name ?? '';
  let sum = 0;
  for (const char of name) sum += char.codePointAt(0) ?? 0;
  return palette[sum % palette.length];
});
</script>

<template>
  <Avatar
    :size="size ?? 28"
    :src="src"
    :style="src ? undefined : { backgroundColor: tone }"
    @error="
      () => {
        // 파일이 사라졌거나 썸네일이 없는 경우. 빈 동그라미보다 첫 글자가 낫다.
        broken = true;
        return false;
      }
    "
  >
    {{ initial }}
  </Avatar>
</template>
