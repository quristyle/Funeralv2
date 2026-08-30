<script lang="ts" setup>
import type { EmbedAttrs, ParsedVideo, PlaybackOptions } from './video-embed';

import { computed, ref, watch } from 'vue';

import { IconifyIcon } from '@vben/icons';

import {
  Button,
  Checkbox,
  InputNumber,
  Modal,
  Segmented,
  Textarea,
  Tooltip,
} from 'ant-design-vue';

import {
  buildYoutubeSrc,
  DEFAULT_PLAYBACK,
  parseVideoInput,
  SERVICE_LABEL,
  SUPPORTED_LABEL,
  toEmbedAttrs,
} from './video-embed';

/**
 * [영상 넣기 창]
 *
 * 요즘 편집기들이 쓰는 방식을 참고했다.
 *   · Notion · CKEditor 5  붙여넣기 우선 — 입력 한 칸이고 버튼을 누르지 않아도 알아본다
 *   · Gutenberg            알아보지 못하면 그 자리에 오류를 적고 [넣기] 를 잠근다
 *   · TinyMCE              크기 칸 + 비율 유지 잠금, 붙여넣는 즉시 미리보기
 *   · YouTube 퍼가기 패널   미리보기 옆에 재생 옵션 체크
 *
 * 그래서 이 창은 이렇게 동작한다.
 *   1. 주소나 삽입 코드를 붙여 넣으면 **버튼을 누르지 않아도** 바로 알아본다
 *   2. 알아본 즉시 실제 재생기로 미리 보여 준다 — 저장하고 나서 확인할 일이 없다
 *   3. 알아보지 못하면 왜 안 되는지 그 자리에 적는다
 *
 * 세로 스크롤을 만들지 않으려고(준수사항 4) 재생 옵션은 한 줄로 접어 두고
 * 미리보기 높이를 고정했다.
 */

const open = defineModel<boolean>('open', { default: false });

const emit = defineEmits<{
  /** 넣기를 눌렀다. 편집기가 이 속성으로 iframe 을 심는다. */
  insert: [attrs: EmbedAttrs];
}>();

/** 사용자가 붙여 넣은 원문 */
const input = ref('');
/** 알아본 결과. null 이면 아직 못 알아봤다. */
const video = ref<null | ParsedVideo>(null);
/** 무엇이 잘못됐는지. 입력이 비어 있으면 null(오류가 아니라 아직 안 쓴 것이다). */
const error = ref<null | string>(null);

const sizeMode = ref<'fixed' | 'responsive'>('responsive');
const width = ref(816);
const height = ref(480);
/** 비율 유지 (TinyMCE 의 constrain proportions) */
const lockRatio = ref(true);

const playback = ref<PlaybackOptions>({ ...DEFAULT_PLAYBACK });
/** 재생 옵션 줄을 펼쳤는지 */
const showOptions = ref(false);

/** YouTube 만 재생 옵션을 다룬다. 다른 서비스는 주소 규칙이 달라 손대지 않는다. */
const isYoutube = computed(() => video.value?.service === 'youtube');

const serviceLabel = computed(() =>
  video.value ? SERVICE_LABEL[video.value.service] : '',
);

/**
 * 미리보기·삽입에 쓸 주소.
 * YouTube 는 재생 옵션을 바꿀 때마다 다시 만든다.
 */
const src = computed(() => {
  const found = video.value;
  if (!found) return '';
  if (found.service === 'youtube' && found.videoId) {
    return buildYoutubeSrc(found.videoId, playback.value);
  }
  return found.src;
});

/**
 * 미리보기에 쓸 주소.
 *
 * 자동 재생·반복은 미리보기에서 빼 둔다. 옵션을 만지는 동안 영상이 계속
 * 다시 시작하면 무엇을 보고 있는지 알 수 없다. 크기와 컨트롤은 그대로 반영한다.
 */
const previewSrc = computed(() => {
  const found = video.value;
  if (!found) return '';
  if (found.service === 'youtube' && found.videoId) {
    return buildYoutubeSrc(found.videoId, {
      ...playback.value,
      autoplay: false,
      loop: false,
    });
  }
  return found.src;
});

const canInsert = computed(() => Boolean(video.value));

/** 자동 재생을 켜면 소리 끔이 강제된다 — 브라우저가 소리 있는 자동 재생을 막는다. */
const muteLocked = computed(() => playback.value.autoplay);

watch(
  () => playback.value.autoplay,
  (on) => {
    if (on) playback.value.mute = true;
  },
);

/** 너비를 바꿀 때 16:9 를 유지한다. */
watch(width, (value) => {
  if (lockRatio.value && value > 0) {
    height.value = Math.round((value * 9) / 16);
  }
});

watch(height, (value) => {
  if (lockRatio.value && value > 0) {
    const next = Math.round((value * 16) / 9);
    if (next !== width.value) width.value = next;
  }
});

/**
 * 입력을 알아본다.
 *
 * 버튼을 누르게 하지 않는다 — 붙여 넣는 것이 곧 실행이다.
 * 알아본 값이 삽입 코드라면 적어 온 크기·재생 옵션을 그대로 이어받는다.
 */
watch(input, (raw) => {
  const text = raw.trim();

  if (!text) {
    video.value = null;
    error.value = null;
    return;
  }

  const found = parseVideoInput(text);

  if (!found) {
    video.value = null;
    error.value = `주소를 알아볼 수 없습니다. ${SUPPORTED_LABEL} 의 주소나 삽입 코드를 넣어 주세요.`;
    return;
  }

  error.value = null;
  video.value = found;
  playback.value = { ...found.playback };

  // 삽입 코드에 크기가 적혀 있으면 그 뜻을 살린다.
  const w = Number.parseInt(found.width ?? '', 10);
  const h = Number.parseInt(found.height ?? '', 10);
  if (found.fromEmbedCode && w > 0) {
    sizeMode.value = 'fixed';
    lockRatio.value = false;
    width.value = w;
    if (h > 0) height.value = h;
  } else {
    sizeMode.value = 'responsive';
    lockRatio.value = true;
  }

  // 옵션이 이미 들어 있는 주소라면 무엇이 켜져 있는지 보이게 펼쳐 준다.
  if (
    found.playback.autoplay ||
    found.playback.loop ||
    found.playback.hideControls ||
    found.playback.start > 0
  ) {
    showOptions.value = true;
  }
});

/** 창을 열 때마다 처음 상태로 돌린다. 지난번에 넣은 영상이 남아 있으면 헷갈린다. */
watch(open, (value) => {
  if (!value) return;
  input.value = '';
  video.value = null;
  error.value = null;
  sizeMode.value = 'responsive';
  width.value = 816;
  height.value = 480;
  lockRatio.value = true;
  playback.value = { ...DEFAULT_PLAYBACK };
  showOptions.value = false;
});

function onInsert() {
  const found = video.value;
  if (!found) return;

  emit('insert', {
    ...toEmbedAttrs(
      { ...found, src: src.value },
      { height: height.value, mode: sizeMode.value, width: width.value },
    ),
  });

  open.value = false;
}
</script>

<template>
  <Modal
    v-model:open="open"
    :width="'min(620px, calc(100vw - 32px))'"
    title="영상 넣기"
    @ok="onInsert"
  >
    <!--
      입력 한 칸. 붙여넣기 우선이라 여기에 눈이 먼저 가야 한다.
      여러 줄을 받는 이유: 삽입 코드는 한 줄에 담기지 않는다.
    -->
    <div class="mb-1 flex items-baseline justify-between">
      <label class="text-sm font-medium" for="video-embed-input">
        영상 주소
      </label>
      <span class="text-muted-foreground text-xs">
        {{ SUPPORTED_LABEL }}
      </span>
    </div>

    <Textarea
      id="video-embed-input"
      v-model:value="input"
      :auto-size="{ minRows: 2, maxRows: 4 }"
      :status="error ? 'error' : undefined"
      placeholder="YouTube 주소를 붙여 넣으세요. 삽입 코드(<iframe ...>)를 그대로 넣어도 됩니다."
      spellcheck="false"
    />

    <!-- 알아보지 못한 이유를 그 자리에 적는다 -->
    <div v-if="error" class="mt-1 flex items-start gap-1 text-xs text-red-500">
      <IconifyIcon class="mt-0.5 size-3.5 shrink-0" icon="lucide:circle-alert" />
      <span>{{ error }}</span>
    </div>

    <!-- 아직 아무것도 안 넣은 상태 -->
    <div
      v-else-if="!video"
      class="border-border text-muted-foreground mt-3 flex h-[188px] flex-col items-center justify-center gap-2 rounded-md border border-dashed text-xs"
    >
      <IconifyIcon class="size-7 opacity-40" icon="lucide:youtube" />
      <span>주소를 붙여 넣으면 여기에서 미리 볼 수 있습니다.</span>
    </div>

    <!-- 알아봤다 — 실제 재생기로 미리 보여 준다 -->
    <template v-else>
      <div class="mt-3 flex items-center gap-2 text-xs">
        <span
          class="bg-primary/10 text-primary rounded px-1.5 py-0.5 font-medium"
        >
          {{ serviceLabel }}
        </span>
        <span v-if="video.videoId" class="text-muted-foreground font-mono">
          {{ video.videoId }}
        </span>
        <span class="flex-1"></span>
        <span class="text-muted-foreground">미리보기</span>
      </div>

      <div
        class="border-border mt-1 overflow-hidden rounded-md border bg-black"
      >
        <!-- 옵션을 바꾸면 주소가 바뀌고, key 가 바뀌어 재생기가 다시 뜬다 -->
        <iframe
          :key="previewSrc"
          allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
          allowfullscreen
          class="block h-[188px] w-full"
          frameborder="0"
          referrerpolicy="strict-origin-when-cross-origin"
          :src="previewSrc"
          title="미리보기"
        ></iframe>
      </div>

      <!-- 크기 -->
      <div class="mt-3 flex flex-wrap items-center gap-3">
        <span class="text-sm font-medium">크기</span>
        <Segmented
          v-model:value="sizeMode"
          :options="[
            { label: '화면 폭에 맞추기', value: 'responsive' },
            { label: '직접 지정', value: 'fixed' },
          ]"
          size="small"
        />

        <template v-if="sizeMode === 'fixed'">
          <InputNumber
            v-model:value="width"
            :min="120"
            :max="1920"
            size="small"
            style="width: 88px"
          />
          <span class="text-muted-foreground text-xs">×</span>
          <InputNumber
            v-model:value="height"
            :min="80"
            :max="1200"
            size="small"
            style="width: 88px"
          />
          <!-- TinyMCE 의 '비율 유지' 를 자물쇠 하나로 둔다 -->
          <Tooltip :title="lockRatio ? '16:9 비율을 유지합니다' : '가로·세로를 따로 정합니다'">
            <Button
              :class="lockRatio ? 'text-primary' : 'text-muted-foreground'"
              size="small"
              type="text"
              @click="lockRatio = !lockRatio"
            >
              <IconifyIcon
                class="size-4"
                :icon="lockRatio ? 'lucide:lock' : 'lucide:lock-open'"
              />
            </Button>
          </Tooltip>
        </template>

        <span
          v-else
          class="text-muted-foreground text-xs"
        >
          좁은 화면에서도 넘치지 않습니다 (16:9)
        </span>
      </div>

      <!-- 재생 옵션 — YouTube 만. 기본은 접어 둔다. -->
      <div v-if="isYoutube" class="mt-2">
        <Button
          class="!px-0"
          size="small"
          type="link"
          @click="showOptions = !showOptions"
        >
          <template #icon>
            <IconifyIcon
              class="mr-1 size-3.5"
              :icon="showOptions ? 'lucide:chevron-down' : 'lucide:chevron-right'"
            />
          </template>
          재생 옵션
        </Button>

        <div
          v-if="showOptions"
          class="border-border mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 rounded-md border px-3 py-2"
        >
          <Checkbox v-model:checked="playback.autoplay">
            <span class="text-xs">자동 재생</span>
          </Checkbox>

          <Tooltip
            :title="
              muteLocked
                ? '자동 재생은 소리를 끈 상태에서만 동작합니다 (브라우저 정책)'
                : ''
            "
          >
            <Checkbox v-model:checked="playback.mute" :disabled="muteLocked">
              <span class="text-xs">소리 끔</span>
            </Checkbox>
          </Tooltip>

          <Checkbox v-model:checked="playback.loop">
            <span class="text-xs">반복 재생</span>
          </Checkbox>

          <Checkbox v-model:checked="playback.hideControls">
            <span class="text-xs">컨트롤 숨기기</span>
          </Checkbox>

          <span class="flex items-center gap-1 text-xs">
            시작
            <InputNumber
              v-model:value="playback.start"
              :min="0"
              :max="86400"
              size="small"
              style="width: 72px"
            />
            초
          </span>
        </div>
      </div>
    </template>

    <template #footer>
      <div class="flex items-center gap-2">
        <span class="text-muted-foreground mr-auto text-xs">
          편집기에 주소를 바로 붙여 넣어도 영상으로 들어갑니다.
        </span>
        <Button @click="open = false">취소</Button>
        <Button :disabled="!canInsert" type="primary" @click="onInsert">
          넣기
        </Button>
      </div>
    </template>
  </Modal>
</template>
