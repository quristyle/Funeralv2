<script lang="ts" setup>
import { onMounted, onUnmounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { useAccessStore } from '@vben/stores';
import { Empty, Modal, Spin, message } from 'ant-design-vue';
import * as signalR from '@microsoft/signalr';
import { useStatusData } from './composables/use-status-data';
import StatusSearchForm from './modules/status-search-form.vue';
import BuildingSection from './modules/building-section.vue';
import DeviceDetailModal from './modules/device-detail-modal.vue';
import RoomMoveModal from './modules/room-move-modal.vue';
import DeceasedFormDrawer from '../deceased/modules/deceased-form-drawer.vue';
import { upsertDeviceAttribute, getDeviceAttribute, cancelDeceasedDeparture } from '#/api/funeral/building';

const {
  searchForm,
  roomEnterDates,
  funeralDates,
  loading,
  hasLoaded,
  collapsedBuildings,
  toggleBuilding,
  onSearch,
  onReset,
  reloadSilently,
  filteredBuildings,
  getRoomsByBuilding,
  getBuildingSummary,
  roomStatuses,
  devices,
  videos,
  musics,
  updateDeviceMediaState,
  updateDeviceStatusState,
} = useStatusData();

const deviceDetailModalRef = ref<InstanceType<typeof DeviceDetailModal> | null>(null);
const deceasedFormDrawerRef = ref<InstanceType<typeof DeceasedFormDrawer> | null>(null);
const roomMoveModalRef = ref<InstanceType<typeof RoomMoveModal> | null>(null);

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
function handleMoveRoom(payload: { deceasedId: string; deceasedName: string; roomId: string; buildingId?: string }) {
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
async function handleBulkMedia(payload: { roomId: string; type: 'video' | 'music'; mediaId: string }) {
  const room = roomStatuses.value.find((r) => r.id === payload.roomId);
  if (!room || room.devices.length === 0) return;
  for (const device of room.devices) {
    await handleUpdateDeviceMedia({ deviceId: device.id, type: payload.type, mediaId: payload.mediaId });
  }
}

let signalRConnection: signalR.HubConnection | null = null;

// ── 갱신 체계 (47번 문서 4단계) ──────────────────────────────────
// 옛 화면은 180초 폴링이었다. 여기서는 60초 폴링 + SignalR 푸시 + 탭 복귀 재조회를
// 함께 쓴다 — 서버 조인 한 번이라 부담이 작다. 폴링은 푸시가 유실됐을 때의 보험이다.
const POLL_MS = 60_000;
let pollTimer: ReturnType<typeof setInterval> | undefined;
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
        return 10000;
      }
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
  document.addEventListener('visibilitychange', onVisibilityChange);
});

onUnmounted(() => {
  if (signalRConnection) {
    signalRConnection.stop();
    signalRConnection = null;
  }
  if (pollTimer) clearInterval(pollTimer);
  if (assignmentDebounce) clearTimeout(assignmentDebounce);
  document.removeEventListener('visibilitychange', onVisibilityChange);
});

// 장비의 동영상/음악 즉시 변경 처리
async function handleUpdateDeviceMedia(payload: { deviceId: string; type: 'video' | 'music'; mediaId: string }) {
  const { deviceId, type, mediaId } = payload;
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
        const mediaName = updatedAttr.videoId ? (videos.value.find((v) => v.value === updatedAttr.videoId)?.label ?? '') : null;
        updateDeviceMediaState(deviceId, 'video', updatedAttr.videoId, mediaName);
      } else {
        const mediaName = updatedAttr.musicId ? (musics.value.find((m) => m.value === updatedAttr.musicId)?.label ?? '') : null;
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
</script>

<template>
  <Page auto-content-height>
    <!-- ── 상단 검색 필터 바 (Horizontal) ───────────────────────────── -->
    <StatusSearchForm
      v-model="searchForm"
      v-model:room-enter-dates="roomEnterDates"
      v-model:funeral-dates="funeralDates"
      @search="onSearch"
      @reset="onReset"
    />

    <!-- ── 빈소 현황 대시보드 콘텐츠 영역 ────────────────────────────── -->
    <div class="flex-1 overflow-auto bg-background/50 rounded-lg p-0">
      <div v-if="loading" class="flex h-96 items-center justify-center">
        <Spin size="large" tip="빈소 현황 데이터를 조회 중입니다..." />
      </div>

      <div v-else-if="!hasLoaded" class="flex h-96 items-center justify-center">
        <Empty description="회사 필터를 설정하고 검색 버튼을 클릭하여 현황 조회를 시작해주세요." />
      </div>

      <div v-else-if="filteredBuildings.length === 0" class="flex h-96 items-center justify-center">
        <Empty description="조회 가능한 건물 정보가 존재하지 않습니다." />
      </div>

      <div v-else class="space-y-8">
        <!-- 건물별 루프 섹션 -->
        <BuildingSection
          v-for="building in filteredBuildings"
          :key="building.id"
          :building="building"
          :rooms="getRoomsByBuilding(building.id)"
          :devices="devices"
          :videos="videos"
          :musics="musics"
          :collapsed="!!collapsedBuildings[building.id]"
          :summary="getBuildingSummary(building.id)"
          @toggle="toggleBuilding(building.id)"
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
    </div>
    <DeviceDetailModal ref="deviceDetailModalRef" />
    <!-- 고인 종합 드로어(고인관리 것 재사용) · 호실 변경 팝업 -->
    <DeceasedFormDrawer ref="deceasedFormDrawerRef" @saved="onSearch" />
    <RoomMoveModal ref="roomMoveModalRef" @moved="onSearch" />
  </Page>
</template>
