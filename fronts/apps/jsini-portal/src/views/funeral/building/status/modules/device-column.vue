<script lang="ts" setup>
/**
 * 공용 장비 카드 — 호실에 매이지 않은 장비 한 대.
 *
 * 입구 안내 · 로비 키오스크처럼 **건물이나 층에 붙은 장비**는 호실이 없어서
 * 예전에는 보드 아래에 작은 줄로 붙어 있었다. 그러면 호실 카드와 눈높이가 달라
 * 같은 화면에서 함께 훑기 어렵고, 그 장비가 꺼져 있어도 눈에 잘 안 띄었다.
 * 호실과 같은 카드로 그려 **한 격자에서 같이 보이게** 한다.
 *
 * 호실 카드와 다른 점은 고인이 없다는 것뿐이라, 영정 자리에는 장비 아이콘을 넣어
 * 카드 높이를 맞춘다.
 */
import { computed } from 'vue';

import { IconifyIcon } from '@vben/icons';

import DeviceRow from './device-row.vue';

const props = defineProps<{
  device: any;
  videos?: any[];
  musics?: any[];
  /** 층에 붙은 장비면 층 이름을 곁들인다. */
  scopeLabel?: string;
}>();

defineEmits<{
  (e: 'update-media', payload: { deviceId: string; type: 'music' | 'video'; mediaId: string }): void;
  (e: 'show-detail', deviceId: string): void;
}>();

const online = computed(() => props.device.status === 'ONLINE');

/** 상태 띠 — 호실 카드와 같은 규칙(빨강=끊김). */
const accentClass = computed(() =>
  online.value ? 'border-l-emerald-500' : 'border-l-red-500',
);

const typeIcon: Record<string, string> = {
  FUNERAL_PORTRAIT: 'mdi:image-frame',
  MULTIMEDIA: 'mdi:play-box-outline',
  ROOM_GUIDE: 'mdi:sign-direction',
  ENTRANCE_GUIDE: 'mdi:door-sliding',
  KIOSK: 'mdi:tablet-dashboard',
};
</script>

<template>
  <div
    class="flex flex-col gap-1.5 border border-l-[3px] border-dashed border-border/80 bg-card p-2 transition-colors hover:border-primary/50"
    :class="accentClass"
  >
    <!-- 머리 — 호실 카드의 호실명 자리에 '공용' 표시가 온다 -->
    <div class="flex items-center justify-between gap-1">
      <span class="truncate text-base font-bold text-foreground">
        {{ device.shortName || device.name }}
      </span>
      <span
        class="shrink-0 rounded bg-muted px-1.5 py-0.5 text-sm text-muted-foreground"
      >
        {{ scopeLabel ?? '건물 공용' }}
      </span>
    </div>

    <!-- 영정 자리 — 장비 아이콘으로 채워 호실 카드와 높이를 맞춘다 -->
    <div
      class="flex aspect-[4/5] w-full flex-col items-center justify-center gap-2 rounded border border-border bg-muted/40"
    >
      <IconifyIcon
        :icon="typeIcon[device.deviceType] ?? 'mdi:monitor'"
        class="size-12"
        :class="online ? 'text-primary/50' : 'text-red-500/40'"
      />
      <span
        class="text-sm font-medium"
        :class="online ? 'text-emerald-600' : 'text-red-500'"
      >
        {{ online ? '온라인' : '오프라인' }}
      </span>
    </div>

    <div class="text-sm leading-snug text-muted-foreground">
      <div class="truncate">호실 배정 없음</div>
      <div class="truncate">코드 {{ device.code }}</div>
    </div>

    <!-- 장비 줄 — 호실 카드가 쓰는 것과 같은 부품이다 -->
    <div class="mt-auto border-t border-border/40 pt-1.5">
      <DeviceRow
        :device="device"
        :videos="videos"
        :musics="musics"
        @update-media="(p) => $emit('update-media', p)"
        @show-detail="(id) => $emit('show-detail', id)"
      />
    </div>
  </div>
</template>
