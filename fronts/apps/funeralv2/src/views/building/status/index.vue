<script lang="ts" setup>
import { onMounted, onUnmounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Empty, Spin, message } from 'ant-design-vue';
import * as signalR from '@microsoft/signalr';
import { useStatusData } from './composables/use-status-data';
import StatusSearchForm from './modules/status-search-form.vue';
import BuildingSection from './modules/building-section.vue';
import DeviceDetailModal from './modules/device-detail-modal.vue';
import { upsertDeviceAttribute, getDeviceAttribute } from '#/api/building';

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
  filteredBuildings,
  getRoomsByBuilding,
  getBuildingSummary,
  devices,
  videos,
  musics,
  updateDeviceMediaState,
  updateDeviceStatusState,
} = useStatusData();

const deviceDetailModalRef = ref<InstanceType<typeof DeviceDetailModal> | null>(null);

let signalRConnection: signalR.HubConnection | null = null;

function initSignalR() {
  const hubUrl = '/api/funeral/hubs/device';
  signalRConnection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
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
    console.log(`[SignalR Status Event] Device: ${deviceCode} -> ${status}`);
    updateDeviceStatusState(deviceCode, status);
  });

  signalRConnection.start()
    .then(() => {
      console.log('[SignalR Connected] Device Status Monitor active');
    })
    .catch((err) => {
      console.error('[SignalR Connection Error]', err);
    });
}

onMounted(() => {
  onSearch();
  initSignalR();
});

onUnmounted(() => {
  if (signalRConnection) {
    signalRConnection.stop();
    signalRConnection = null;
  }
});

// 장비의 동영상/음악 즉시 변경 처리
async function handleUpdateDeviceMedia(payload: { deviceId: string; type: 'video' | 'music'; mediaId: string }) {
  const { deviceId, type, mediaId } = payload;
  const hide = message.loading('설정을 저장하는 중...', 0);
  try {
    // 1. 기존 속성 조회
    let attr: any;
    try {
      const res = await getDeviceAttribute(deviceId);

      console.log('getDeviceAttribute res:', res);

      // ApiResponse 언래핑: { result: [...] } 구조에서 데이터 추출
      const raw = (res as any)?.result ?? res;
      
      console.log('getDeviceAttribute raw:', raw);

      attr = Array.isArray(raw) ? raw[0] : raw;
      
      console.log('getDeviceAttribute attr:', attr);

    } catch (eee){
      // 속성이 아직 없을 수 있음
      
      console.log('getDeviceAttribute err:', eee);

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
    const res = await upsertDeviceAttribute(savePayload);
    message.success('장비 멀티미디어 설정이 즉시 변경되었습니다.');
    
    // 3. 로컬 상태 즉시 갱신 (전체 API 재조회 없이 변경된 미디어 명칭만 화면에 갱신)
    const raw = (res as any)?.result ?? res;
    const updatedAttr = Array.isArray(raw) ? raw[0] : raw;
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
    <div class="flex-1 overflow-auto bg-background/50 rounded-lg p-2">
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
        />
      </div>
    </div>
    <DeviceDetailModal ref="deviceDetailModalRef" />
  </Page>
</template>
