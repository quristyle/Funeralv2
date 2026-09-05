<script lang="ts" setup>
import { computed } from 'vue';
import { IconifyIcon } from '@vben/icons';
import type { BuildingApi } from '#/api/funeral/building';

/**
 * 화면 표시 미리보기 (49번 문서 D-DV4).
 *
 * 여백 · 정렬 · 방향은 저장하면 곧바로 운영 중인 장비 화면이 다시 그려진다
 * (`DeviceAttributeService` 가 `DeviceChanged` 를 방송한다). 슬라이더를 맞추는
 * 동안 조문객이 보는 화면이 계속 깜박이면 안 되므로, **초안을 먼저 여기에 그린다.**
 * 여기서 맞춘 뒤 [장비에 적용] 을 눌러야 장비로 나간다.
 *
 * 실제 플레이어 화면을 그대로 흉내 내지는 않는다. 이 미리보기가 답하는 질문은
 * 하나다 — "여백과 정렬을 이렇게 두면 사진이 어디에 놓이는가".
 */
const props = defineProps<{
  attr: BuildingApi.DeviceAttribute | null;
  deviceType?: string;
  /** 미리보기에 얹을 이름. 실제 배정된 고인이 없으면 견본 이름을 쓴다. */
  deceasedName?: string;
}>();

/** 세로형 장비는 9:16 으로 세운다. */
const screenRatio = computed(() =>
  props.attr?.displayOrientation === 'PORTRAIT' ? '9 / 16' : '16 / 9',
);

/** 전체 화면 여백 — 값이 % 라서 그대로 padding 에 넣는다. */
const screenPadding = computed(() => {
  const a = props.attr;
  if (!a) return {};
  return {
    paddingTop: `${a.displayPaddingTop ?? 0}%`,
    paddingBottom: `${a.displayPaddingBottom ?? 0}%`,
    paddingLeft: `${a.displayPaddingLeft ?? 0}%`,
    paddingRight: `${a.displayPaddingRight ?? 0}%`,
  };
});

/** 영정사진 여백 — 사진 칸 안쪽으로 한 번 더 들어간다. */
const memorialPadding = computed(() => {
  const a = props.attr;
  if (!a) return {};
  return {
    paddingTop: `${a.memorialPaddingTop ?? 0}%`,
    paddingBottom: `${a.memorialPaddingBottom ?? 0}%`,
    paddingLeft: `${a.memorialPaddingLeft ?? 0}%`,
    paddingRight: `${a.memorialPaddingRight ?? 0}%`,
  };
});

const alignItems = computed(() => {
  switch (props.attr?.photoVerticalAlignment) {
    case 'BOTTOM': return 'flex-end';
    case 'CENTER': return 'center';
    default: return 'flex-start';
  }
});

const justifyContent = computed(() => {
  switch (props.attr?.photoHorizontalAlignment) {
    case 'RIGHT': return 'flex-end';
    case 'LEFT': return 'flex-start';
    default: return 'center';
  }
});

/**
 * 화면 표현(회전)은 그림으로 흉내 내지 않고 글로 적는다. 미리보기를 90도 돌리면
 * 미리보기 상자 자체가 뒤집혀 여백을 맞추기 더 어려워진다.
 */
const rotationLabel = computed(() => {
  switch (props.attr?.portraitOrientation) {
    case 'VERTICAL_LEFT': return '좌 90도';
    case 'VERTICAL_RIGHT': return '우 90도';
    case 'INVERTED': return '180도';
    default: return null;
  }
});

const showName = computed(() => props.attr?.isDeceasedNameVisible !== false);
const previewName = computed(() => props.deceasedName || '홍길동');
</script>

<template>
  <div class="flex flex-col gap-2">
    <div class="rounded-lg border border-border bg-muted/30 p-2">
      <!-- 화면 바깥 테두리 = 장비 모니터 -->
      <div
        class="mx-auto w-full overflow-hidden rounded bg-neutral-900"
        :style="{ aspectRatio: screenRatio, maxHeight: '320px' }"
      >
        <!-- 전체 화면 여백 -->
        <div class="flex h-full w-full flex-col" :style="screenPadding">
          <!-- 배경 이미지를 쓰면 바탕이 밝아진다는 것만 보여 준다 -->
          <div
            class="flex h-full w-full flex-col overflow-hidden rounded-sm"
            :class="attr?.isBackgroundImageEnabled ? 'bg-neutral-700' : 'bg-neutral-800'"
          >
            <!-- 영정사진 장비 -->
            <template v-if="deviceType === 'FUNERAL_PORTRAIT'">
              <div
                v-if="attr?.isMemorialPhotoEnabled"
                class="flex min-h-0 flex-1"
                :style="{ ...memorialPadding, alignItems, justifyContent }"
              >
                <div
                  class="flex items-center justify-center bg-neutral-600 text-neutral-400"
                  :style="
                    attr?.isMemorialPhotoKeepAspectRatio
                      ? { width: '38%', aspectRatio: '3 / 4' }
                      : { width: '100%', height: '100%' }
                  "
                >
                  <IconifyIcon icon="lucide:user" class="size-6" />
                </div>
              </div>
              <div v-else class="flex min-h-0 flex-1 items-center justify-center text-[10px] text-neutral-500">
                영정사진 표시 꺼짐
              </div>
              <div v-if="showName" class="shrink-0 pb-1 text-center text-[11px] text-neutral-100">
                故 {{ previewName }}
              </div>
              <div
                v-if="attr?.isFamilyContactVisible"
                class="shrink-0 pb-1 text-center text-[9px] text-neutral-400"
              >
                유족 연락처 010-0000-0000
              </div>
            </template>

            <!-- 층별 안내판 -->
            <template v-else-if="deviceType === 'ROOM_GUIDE'">
              <div class="flex min-h-0 flex-1 flex-col gap-1 p-2">
                <div
                  v-for="n in 4"
                  :key="n"
                  class="flex items-center gap-2 rounded-sm bg-neutral-700/70 px-2 py-1"
                >
                  <span class="text-[9px] text-neutral-300">{{ n }}호</span>
                  <span
                    v-if="attr?.isRoomAssignmentVisible"
                    class="text-[9px] text-neutral-100"
                  >
                    故 {{ previewName }}
                  </span>
                  <span v-else class="text-[9px] text-neutral-500">배정 숨김</span>
                </div>
              </div>
            </template>

            <!-- 입구 안내 · 키오스크 -->
            <template v-else-if="deviceType === 'KIOSK' || deviceType === 'ENTRANCE_GUIDE'">
              <div class="flex min-h-0 flex-1 flex-col items-center justify-center gap-2 p-2">
                <div class="text-center text-[10px] text-neutral-100">
                  {{ attr?.entranceGreeting || '삼가 고인의 명복을 빕니다.' }}
                </div>
                <div v-if="attr?.isBuildingMapVisible" class="h-10 w-2/3 rounded-sm bg-neutral-700"></div>
                <div v-if="attr?.isQrCodeVisible" class="size-6 rounded-sm bg-neutral-500"></div>
              </div>
              <div
                v-if="attr?.isNoticeVisible"
                class="shrink-0 bg-neutral-700/80 px-2 py-0.5 text-[9px] text-neutral-200"
              >
                공지사항이 흐릅니다 · 속도 {{ attr?.noticeScrollSpeed ?? 2 }}
              </div>
            </template>

            <!-- 멀티미디어 그 밖 -->
            <template v-else>
              <div class="flex min-h-0 flex-1 items-center justify-center">
                <IconifyIcon
                  :icon="attr?.isVideoEnabled ? 'lucide:play-circle' : 'lucide:monitor'"
                  class="size-8 text-neutral-600"
                />
              </div>
            </template>
          </div>
        </div>
      </div>
    </div>

    <div class="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-muted-foreground">
      <span>{{ attr?.displayOrientation === 'PORTRAIT' ? '세로' : '가로' }}</span>
      <span v-if="rotationLabel">· 회전 {{ rotationLabel }}</span>
      <span v-if="attr?.isMusicEnabled">· {{ attr?.isMuted ? '음소거' : '음악 켜짐' }}</span>
    </div>
  </div>
</template>
