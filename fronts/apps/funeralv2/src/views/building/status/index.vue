<script lang="ts" setup>
import { Page } from '@vben/common-ui';
import { Empty, Spin, message } from 'ant-design-vue';
import { useStatusData } from './composables/use-status-data';
import StatusSearchForm from './modules/status-search-form.vue';
import BuildingSection from './modules/building-section.vue';
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
} = useStatusData();

// 장비의 동영상/음악 즉시 변경 처리
async function handleUpdateDeviceMedia(payload: { deviceId: string; type: 'video' | 'music'; mediaId: string }) {
  const { deviceId, type, mediaId } = payload;
  const hide = message.loading('설정을 저장하는 중...', 0);
  try {
    // 1. 기존 속성 조회
    let attr: any;
    try {
      const res = await getDeviceAttribute(deviceId);
      // ApiResponse 언래핑: { result: [...] } 구조에서 데이터 추출
      const raw = (res as any)?.result ?? res;
      attr = Array.isArray(raw) ? raw[0] : raw;
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
    
    // 2. 값 설정 및 즉시 사용(Enabled = true) 변경
    if (type === 'video') {
      attr.videoId = mediaId || null;
      attr.isVideoEnabled = true; // 선택 시 즉시 사용 활성화
    } else {
      attr.musicId = mediaId || null;
      attr.isMusicEnabled = true; // 선택 시 즉시 사용 활성화
    }
    
    // id가 있으면 Omit<DeviceAttribute, 'id'> 형태에서 id 속성을 명시적으로 제거
    const savePayload = { ...attr };
    delete savePayload.id;
    
    await upsertDeviceAttribute(savePayload);
    message.success('장비 멀티미디어 설정이 즉시 변경되었습니다.');
    
    // 3. 목록 재조회로 뷰 갱신
    onSearch();
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
        />
      </div>
    </div>
  </Page>
</template>
