<script setup lang="ts">
import { computed } from 'vue';

/**
 * DB 에서 온 본문을 문단과 굵은 글씨까지만 살려서 보여 준다.
 *
 * **`v-html` 을 쓰지 않는다.** 관리 화면에서 쓴 글이라도 HTML 로 넣는 순간 XSS 경로가 된다.
 * 대신 문자열을 조각으로 잘라 템플릿으로 그린다 — Vue 가 알아서 escape 하므로
 * 어떤 값이 들어와도 태그로 해석되지 않는다.
 *
 * 알아보는 것은 둘뿐이다.
 *   · 빈 줄  → 문단 나눔
 *   · `**...**` → 굵게
 *
 * 마크다운 렌더러를 얹지 않은 이유: 링크·이미지·표까지 받으면 정제(sanitize)가 필요해지고,
 * 정적 사이트에서 번들도 커진다. 소개 문구에 필요한 것은 이 둘이다.
 * 더 필요해지면 그때 렌더러와 정제를 **함께** 넣는다.
 */
const props = defineProps<{
  /** 문단 사이 여백 클래스 (기본 `mt-4`) */
  gap?: string;
  text?: null | string;
}>();

/** 한 문단을 굵은 조각과 보통 조각으로 자른다. `**` 개수가 홀수여도 깨지지 않는다. */
function split(paragraph: string) {
  return paragraph.split('**').map((part, index) => ({
    bold: index % 2 === 1,
    key: index,
    text: part,
  }));
}

const paragraphs = computed(() =>
  (props.text ?? '')
    .split(/\n{2,}/)
    .map((p) => p.trim())
    .filter(Boolean)
    .map((p, index) => ({ key: index, parts: split(p) })),
);
</script>

<template>
  <div>
    <p
      v-for="(paragraph, index) in paragraphs"
      :key="paragraph.key"
      class="whitespace-pre-line"
      :class="index === 0 ? '' : (gap ?? 'mt-4')"
    >
      <template v-for="part in paragraph.parts" :key="part.key">
        <strong v-if="part.bold" class="font-bold">{{ part.text }}</strong>
        <template v-else>{{ part.text }}</template>
      </template>
    </p>
  </div>
</template>
