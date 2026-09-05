<script lang="ts" setup>
/**
 * 빈소현황 (`/room_status`).
 *
 * **한 화면이 밀도 셋으로 갈린다** (47번 문서 5단계). 시설 한 곳만 볼 때와
 * 스무 곳을 관제할 때가 서로 반대되는 것을 원하기 때문이다 — 앞은 깊이(조작),
 * 뒤는 넓이(이상 감지). 라우트·데이터·액션은 하나로 두고 그리는 크기만 바꾼다.
 *
 *   운영(manage)   시설 1곳      RoomColumn 보드 — 화면에서 직접 조작
 *   감시(watch)    시설 2~4곳    시설 레인 + 압축 타일 + 우측 상세 패널
 *   상황판(board)  시설 5곳~     시설 스트립 — 색으로만 훑고 시설로 내려간다
 *
 * 밀도는 조회 결과가 자동으로 고르고 사용자가 덮어쓴다. 덮어쓴 값과 시설 선택은
 * 브라우저에 남는다 — 시설 다섯을 매번 다시 펼치지 않게.
 */
import { computed, onMounted, onUnmounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { useAccessStore } from '@vben/stores';

import * as signalR from '@microsoft/signalr';
import { Button, Empty, Modal, Spin, Switch, Tooltip, message } from 'ant-design-vue';

import {
  cancelDeceasedDeparture,
  getDeviceAttribute,
  upsertDeviceAttribute,
} from '#/api/funeral/building';

import DeceasedFormDrawer from '../deceased/modules/deceased-form-drawer.vue';
import { useStatusData } from './composables/use-status-data';
import DeviceDetailModal from './modules/device-detail-modal.vue';
import FacilityBoard from './modules/facility-board.vue';
import FacilityLane from './modules/facility-lane.vue';
import FacilityStrip from './modules/facility-strip.vue';
import RoomColumn from './modules/room-column.vue';
import RoomDetailDrawer from './modules/room-detail-drawer.vue';
import RoomMoveModal from './modules/room-move-modal.vue';
import StatusSearchForm from './modules/status-search-form.vue';

const {
  searchForm,
  selectedFacilityIds,
  roomEnterDates,
  funeralDates,
  loading,
  hasLoaded,
  onSearch,
  onReset,
  reloadSilently,
  roomStatuses,
  videos,
  musics,
  now,
  touchNow,
  allFacilities,
  facilities,
  multiCompany,
  globalSummary,
  roomStateOf,
  isMobile,
  density,
  densityOverride,
  setDensity,
  collapsedFacilities,
  toggleFacility,
  sortByAlert,
  drillIntoFacility,
  clearFacilityFilter,
  updateDeviceMediaState,
  updateDeviceStatusState,
} = useStatusData();

const deviceDetailModalRef = ref<InstanceType<typeof DeviceDetailModal> | null>(null);
const deceasedFormDrawerRef = ref<InstanceType<typeof DeceasedFormDrawer> | null>(null);
const roomMoveModalRef = ref<InstanceType<typeof RoomMoveModal> | null>(null);
const roomDetailDrawerRef = ref<InstanceType<typeof RoomDetailDrawer> | null>(null);

// ── 감시 밀도의 상세 ─────────────────────────────────────────────
// 데스크탑은 오른쪽 260px 패널, 모바일은 아래에서 올라오는 서랍이다. 그리는 내용은
// 같은 `RoomColumn` 이라 액션 진입점은 한 벌로 남는다.
const selectedRoomId = ref<string>('');
const selectedRoom = computed(() =>
  roomStatuses.value.find((r) => r.id === selectedRoomId.value),
);

function selectRoom(roomId: string) {
  if (isMobile.value) {
    selectedRoomId.value = roomId;
    roomDetailDrawerRef.value?.open(roomId);
    return;
  }
  // 넓은 화면에서는 같은 칸을 다시 누르면 패널을 접는다.
  selectedRoomId.value = selectedRoomId.value === roomId ? '' : roomId;
}

const DENSITY_OPTIONS = [
  { value: 'auto', label: '자동', hint: '조회 범위에 맞춰 고른다' },
  { value: 'manage', label: '운영', hint: '시설 한 곳을 크게 — 화면에서 바로 조작' },
  { value: 'watch', label: '감시', hint: '시설 두셋을 레인으로 — 눌러서 상세' },
  { value: 'board', label: '상황판', hint: '시설 여럿을 한 줄씩 — 이상만 본다' },
] as const;

// ── 카드 관리 메뉴 핸들러 (47번 문서 2단계) ─────────────────────────

/** 고인 정보 관리 — 고인관리의 종합 드로어를 그대로 재사용한다. */
function handleEditDeceased(deceasedId: string) {
  deceasedFormDrawerRef.value?.open({ id: deceasedId });
}

/** 공실에서 고인 등록 — 그 호실을 미리 채운 신규 폼. */
function handleCreateDeceased(roomId: string) {
  deceasedFormDrawerRef.value?.open({ roomId });
}

/** 호실 변경 팝업 열기. */
function handleMoveRoom(payload: {
  deceasedId: string;
  deceasedName: string;
  roomId: string;
  buildingId?: string;
}) {
  roomMoveModalRef.value?.open(payload);
}

/** 공실 카드의 출상 취소 — 되돌아갈 호실에 다른 고인이 있으면 서버가 거부한다. */
function handleCancelDeparture(payload: { deceasedId: string; deceasedName: string }) {
  Modal.confirm({
    title: '출상 취소 확인',
    content: `고인 [故 ${payload.deceasedName}] 님의 출상을 취소하고 이 호실 배정을 복구하시겠습니까?`,
    okText: '진행',
    cancelText: '취소',
    onOk: async () => {
      try {
        await cancelDeceasedDeparture(payload.deceasedId);
        message.success('출상 취소 처리가 완료되었습니다.');
        onSearch();
      } catch (err: any) {
        const reason = err?.response?.data?.message || err?.message;
        message.error(reason || '출상 취소 중 오류가 발생했습니다.');
      }
    },
  });
}

/** 호실 미디어 일괄 변경 — 호실의 모든 장비에 같은 영상/음원을 건다.
 *  옛 화면의 고인 단위(=호실 일괄) 변경에 해당한다 (ASIS A4·A5). */
async function handleBulkMedia(payload: {
  roomId: string;
  type: 'music' | 'video';
  mediaId: string;
}) {
  const room = roomStatuses.value.find((r) => r.id === payload.roomId);
  if (!room || room.devices.length === 0) return;
  for (const device of room.devices) {
    await handleUpdateDeviceMedia({
      deviceId: device.id,
      type: payload.type,
      mediaId: payload.mediaId,
    });
  }
}

let signalRConnection: null | signalR.HubConnection = null;

// ── 갱신 체계 (47번 문서 4단계) ──────────────────────────────────
// 옛 화면은 180초 폴링이었다. 여기서는 60초 폴링 + SignalR 푸시 + 탭 복귀 재조회를
// 함께 쓴다 — 서버 조인 한 번이라 부담이 작다. 폴링은 푸시가 유실됐을 때의 보험이다.
const POLL_MS = 60_000;
/** 남은 시간 표시는 조회 없이도 흘러가야 한다. */
const TICK_MS = 30_000;
let pollTimer: ReturnType<typeof setInterval> | undefined;
let tickTimer: ReturnType<typeof setInterval> | undefined;
let assignmentDebounce: ReturnType<typeof setTimeout> | undefined;

function onVisibilityChange() {
  if (document.visibilityState === 'visible') {
    reloadSilently();
  }
}

function initSignalR() {
  const hubUrl = '/api/funeral/hubs/device';
  signalRConnection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl, {
      // 허브 라우트는 게이트웨이에서 익명이지만, 토큰이 있으면 실어 보낸다 —
      // 나중에 익명을 걷어낼 때(D-M1) 화면 쪽을 다시 손대지 않기 위해서다.
      accessTokenFactory: () => useAccessStore().accessToken ?? '',
    })
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext) => {
        if (retryContext.previousRetryCount === 0) return 0;
        if (retryContext.previousRetryCount === 1) return 2000;
        if (retryContext.previousRetryCount === 2) return 5000;
        return 10_000;
      },
    })
    .build();

  signalRConnection.on('DeviceStatusChanged', (deviceCode: string, status: string) => {
    updateDeviceStatusState(deviceCode, status);
  });

  // 배정 변경(등록·이동·출상·취소) 푸시 — 잠깐 모아서 한 번만 재조회한다.
  signalRConnection.on('RoomAssignmentChanged', () => {
    if (assignmentDebounce) clearTimeout(assignmentDebounce);
    assignmentDebounce = setTimeout(() => reloadSilently(), 800);
  });

  // 재접속하면 끊겨 있던 동안의 변경을 놓쳤을 수 있으므로 전체를 다시 맞춘다.
  signalRConnection.onreconnected(() => {
    reloadSilently();
  });

  signalRConnection.start().catch((err) => {
    console.error('[SignalR Connection Error]', err);
  });
}

onMounted(() => {
  onSearch();
  initSignalR();
  pollTimer = setInterval(() => {
    if (document.visibilityState === 'visible') reloadSilently();
  }, POLL_MS);
  tickTimer = setInterval(touchNow, TICK_MS);
  document.addEventListener('visibilitychange', onVisibilityChange);
});

onUnmounted(() => {
  if (signalRConnection) {
    signalRConnection.stop();
    signalRConnection = null;
  }
  if (pollTimer) clearInterval(pollTimer);
  if (tickTimer) clearInterval(tickTimer);
  if (assignmentDebounce) clearTimeout(assignmentDebounce);
  document.removeEventListener('visibilitychange', onVisibilityChange);
});

// 장비의 동영상/음악 즉시 변경 처리
async function handleUpdateDeviceMedia(payload: {
  deviceId: string;
  type: 'music' | 'video';
  mediaId: string;
}) {
  const { deviceId, mediaId, type } = payload;
  const hide = message.loading('설정을 저장하는 중...', 0);
  try {
    // 1. 기존 속성 조회 (봉투는 API 모듈이 벗겨서 온다 — 준수사항 7)
    let attr: any;
    try {
      attr = await getDeviceAttribute(deviceId);
    } catch {
      // 속성이 아직 없을 수 있음
    }

    if (!attr) {
      attr = {
        deviceId,
        displayOrientation: 'LANDSCAPE',
        contentIntervalSec: 10,
        isScreensaverEnabled: false,
        screensaverTimeoutSec: 300,
        isMemorialPhotoEnabled: false,
        memorialPhotoEffect: 'FADE',
        isDeceasedNameVisible: true,
        isFamilyContactVisible: false,
        isVideoEnabled: false,
        isMusicEnabled: false,
        isMediaLoop: true,
        isMuted: false,
      };
    }

    // 2. 값 설정 및 즉시 사용 활성화 상태 자동 설정
    if (type === 'video') {
      attr.videoId = mediaId || null;
      attr.isVideoEnabled = !!mediaId; // 미사용 시 false, 선택 시 true
    } else {
      attr.musicId = mediaId || null;
      attr.isMusicEnabled = !!mediaId; // 미사용 시 false, 선택 시 true
    }

    // id가 있으면 Omit<DeviceAttribute, 'id'> 형태에서 id 속성을 명시적으로 제거
    const savePayload = { ...attr };
    delete savePayload.id;

    // 업데이트 처리를 위해 upsertDeviceAttribute 호출
    const updatedAttr = await upsertDeviceAttribute(savePayload);
    message.success('장비 멀티미디어 설정이 즉시 변경되었습니다.');

    // 3. 로컬 상태 즉시 갱신 (전체 API 재조회 없이 변경된 미디어 명칭만 화면에 갱신)
    if (updatedAttr) {
      if (type === 'video') {
        const mediaName = updatedAttr.videoId
          ? (videos.value.find((v) => v.value === updatedAttr.videoId)?.label ?? '')
          : null;
        updateDeviceMediaState(deviceId, 'video', updatedAttr.videoId, mediaName);
      } else {
        const mediaName = updatedAttr.musicId
          ? (musics.value.find((m) => m.value === updatedAttr.musicId)?.label ?? '')
          : null;
        updateDeviceMediaState(deviceId, 'music', updatedAttr.musicId, mediaName);
      }
    }
  } catch (err) {
    console.error('장비 미디어 변경 실패:', err);
    message.error('장비 미디어 설정 변경 실패');
  } finally {
    hide();
  }
}

/** 레인·스트립 머리글의 진입점 — 그 시설만 남기고 운영 밀도로 내려간다. */
function handleDrill(facilityId: string) {
  drillIntoFacility(facilityId);
  selectedRoomId.value = '';
}
</script>

<template>
  <Page auto-content-height>
    <StatusSearchForm
      v-model="searchForm"
      v-model:selected-facility-ids="selectedFacilityIds"
      v-model:room-enter-dates="roomEnterDates"
      v-model:funeral-dates="funeralDates"
      :facilities="allFacilities"
      @search="onSearch"
      @reset="onReset"
    />

    <!-- 전역 배너 + 밀도 세그먼트. 숫자는 서버가 센 것을 그대로 쓴다
         (`RoomBoard.summary` — 예전에는 받아 놓고 버린 뒤 화면에서 다시 셌다). -->
    <div
      v-if="hasLoaded && !loading"
      class="mb-2 flex flex-wrap items-center gap-x-4 gap-y-1 rounded-lg border border-border bg-card px-3 py-1.5 text-sm"
    >
      <span class="flex items-center gap-1 font-bold text-foreground">
        <IconifyIcon icon="mdi:office-building" class="size-4 text-primary" />
        시설 {{ globalSummary.facilityCount }}
        <span v-if="globalSummary.companyCount > 1" class="font-normal text-muted-foreground">
          / 회사 {{ globalSummary.companyCount }}
        </span>
      </span>
      <span>
        <span class="font-bold text-emerald-600">{{ globalSummary.using }}</span>
        <span class="text-muted-foreground">/{{ globalSummary.total }} 사용</span>
      </span>
      <span class="text-muted-foreground">공실 {{ globalSummary.empty }}</span>
      <span v-if="globalSummary.dischargeToday > 0" class="text-amber-600">
        오늘 발인 {{ globalSummary.dischargeToday }}
      </span>
      <span v-if="globalSummary.coffinToday > 0" class="text-muted-foreground">
        오늘 입관 {{ globalSummary.coffinToday }}
      </span>
      <span
        v-if="globalSummary.deviceOffline > 0"
        class="flex items-center gap-1 font-bold text-red-500"
      >
        <IconifyIcon icon="lucide:wifi-off" class="size-3.5" />
        장비 {{ globalSummary.deviceOffline }}/{{ globalSummary.deviceTotal }}대 오프라인
      </span>

      <Button
        v-if="selectedFacilityIds.length > 0"
        size="small"
        type="text"
        class="text-sm"
        @click="clearFacilityFilter"
      >
        <span class="flex items-center gap-1">
          <IconifyIcon icon="lucide:x" class="size-3" />
          시설 선택 해제
        </span>
      </Button>

      <div class="ml-auto flex items-center gap-2">
        <label
          v-if="density !== 'manage'"
          class="flex cursor-pointer items-center gap-1 text-muted-foreground"
        >
          <Switch v-model:checked="sortByAlert" size="small" />
          이상순
        </label>

        <div class="flex items-center overflow-hidden rounded border border-border">
          <Tooltip v-for="opt in DENSITY_OPTIONS" :key="opt.value" :title="opt.hint">
            <button
              type="button"
              class="px-2 py-0.5 text-sm transition-colors"
              :class="
                densityOverride === opt.value
                  ? 'bg-primary text-primary-foreground'
                  : 'text-muted-foreground hover:bg-muted'
              "
              @click="setDensity(opt.value)"
            >
              {{ opt.label }}
            </button>
          </Tooltip>
        </div>
      </div>
    </div>

    <div class="flex-1 overflow-auto rounded-lg bg-background/50">
      <div v-if="loading" class="flex h-96 items-center justify-center">
        <Spin size="large" tip="빈소 현황 데이터를 조회 중입니다..." />
      </div>

      <div v-else-if="!hasLoaded" class="flex h-96 items-center justify-center">
        <Empty description="회사 필터를 설정하고 검색 버튼을 클릭하여 현황 조회를 시작해주세요." />
      </div>

      <div v-else-if="facilities.length === 0" class="flex h-96 items-center justify-center">
        <Empty description="조회된 시설이 없습니다. 회사 · 시설 선택을 확인해 주세요." />
      </div>

      <!-- ── 밀도 1 「운영」 ─────────────────────────────────────── -->
      <div v-else-if="density === 'manage'" class="space-y-2">
        <FacilityBoard
          v-for="facility in facilities"
          :key="facility.id"
          :facility="facility"
          :now="now"
          :videos="videos"
          :musics="musics"
          :show-company="multiCompany"
          :collapsible="facilities.length > 1"
          :collapsed="!!collapsedFacilities[facility.id]"
          :room-state="roomStateOf"
          @toggle="toggleFacility(facility.id)"
          @update-media="handleUpdateDeviceMedia"
          @show-detail="(id) => deviceDetailModalRef?.open(id)"
          @refresh="onSearch"
          @edit-deceased="handleEditDeceased"
          @create-deceased="handleCreateDeceased"
          @move-room="handleMoveRoom"
          @cancel-departure="handleCancelDeparture"
          @bulk-media="handleBulkMedia"
        />
      </div>

      <!-- ── 밀도 2 「감시」 ─────────────────────────────────────── -->
      <div v-else-if="density === 'watch'" class="flex gap-2">
        <div class="min-w-0 flex-1 space-y-2">
          <FacilityLane
            v-for="facility in facilities"
            :key="facility.id"
            :facility="facility"
            :now="now"
            :show-company="multiCompany"
            :collapsed="!!collapsedFacilities[facility.id]"
            :selected-room-id="selectedRoomId"
            :room-state="roomStateOf"
            @toggle="toggleFacility(facility.id)"
            @drill="handleDrill(facility.id)"
            @select-room="selectRoom"
            @show-device="(id) => deviceDetailModalRef?.open(id)"
          />
        </div>

        <!-- 상세 패널 — 운영 밀도의 칸을 그대로 쓴다.
             모바일에서는 이 자리를 비우고 서랍으로 올린다(아래 RoomDetailDrawer). -->
        <aside v-if="!isMobile" class="w-[260px] shrink-0">
          <RoomColumn
            v-if="selectedRoom"
            :room="selectedRoom"
            :state="roomStateOf(selectedRoom)"
            :now="now"
            :videos="videos"
            :musics="musics"
            variant="panel"
            @update-media="handleUpdateDeviceMedia"
            @show-detail="(id) => deviceDetailModalRef?.open(id)"
            @refresh="onSearch"
            @edit-deceased="handleEditDeceased"
            @create-deceased="handleCreateDeceased"
            @move-room="handleMoveRoom"
            @cancel-departure="handleCancelDeparture"
            @bulk-media="handleBulkMedia"
          />
          <div
            v-else
            class="flex h-40 flex-col items-center justify-center gap-2 rounded-lg border border-dashed border-border text-sm text-muted-foreground"
          >
            <IconifyIcon icon="lucide:mouse-pointer-click" class="size-6 opacity-40" />
            호실을 누르면 여기에 열립니다
          </div>
        </aside>
      </div>

      <!-- ── 밀도 3 「상황판」 ───────────────────────────────────── -->
      <div v-else class="space-y-2">
        <div class="rounded-lg border border-border bg-card px-2 py-1">
          <FacilityStrip
            v-for="facility in facilities"
            :key="facility.id"
            :facility="facility"
            :now="now"
            :show-company="multiCompany"
            :room-state="roomStateOf"
            @drill="handleDrill(facility.id)"
          />
        </div>
      </div>
    </div>

    <DeviceDetailModal ref="deviceDetailModalRef" />
    <!-- 고인 종합 드로어(고인관리 것 재사용) · 호실 변경 팝업 -->
    <DeceasedFormDrawer ref="deceasedFormDrawerRef" @saved="onSearch" />
    <RoomMoveModal ref="roomMoveModalRef" @moved="onSearch" />
    <!-- 모바일 상세 — 뒤로가기로 닫힌다 -->
    <RoomDetailDrawer
      ref="roomDetailDrawerRef"
      :rooms="roomStatuses"
      :now="now"
      :videos="videos"
      :musics="musics"
      :room-state="roomStateOf"
      @update-media="handleUpdateDeviceMedia"
      @show-detail="(id) => deviceDetailModalRef?.open(id)"
      @refresh="onSearch"
      @edit-deceased="handleEditDeceased"
      @create-deceased="handleCreateDeceased"
      @move-room="handleMoveRoom"
      @cancel-departure="handleCancelDeparture"
      @bulk-media="handleBulkMedia"
    />
  </Page>
</template>
