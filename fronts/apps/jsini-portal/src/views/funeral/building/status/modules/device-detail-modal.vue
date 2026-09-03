<script lang="ts" setup>
import { ref } from 'vue';
import { Modal, Tabs, Descriptions, Tag, Badge, Spin, Empty } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getDevice, getDeviceAttribute, getDeviceRibbons, getDeviceTextOverlays } from '#/api/funeral/building';
import type { BuildingApi } from '#/api/funeral/building';

/**
 * [장비 상세 정보 팝업]
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **가져오기 방식은 그대로다** — `open()` 이 네 곳을 한 번에 부르고 그 결과를
 * `:table-data` 로 그리드에 넘긴다. 팝업 안이라 `page-fill-last` 가 없으므로
 * 높이는 숫자로 준다.
 * ------------------------------------------------------------
 */

const visible = ref(false);
const loading = ref(false);
const activeKey = ref('basic');

/** 지금 보고 있는 장비. 도구줄의 재조회가 `open()` 을 다시 부를 때 쓴다. */
const currentDeviceId = ref('');

// 데이터 상태
const deviceData = ref<BuildingApi.Device | null>(null);
const attributeData = ref<BuildingApi.DeviceAttribute | null>(null);
const ribbonData = ref<BuildingApi.DeviceRibbon[]>([]);
const overlayData = ref<BuildingApi.DeviceTextOverlay[]>([]);

const deviceTypeMap: Record<string, { label: string; color: string }> = {
  FUNERAL_PORTRAIT: { label: '영정사진', color: 'purple' },
  MULTIMEDIA: { label: '멀티미디어', color: 'orange' },
  ROOM_GUIDE: { label: '호실 안내', color: 'blue' },
  ENTRANCE_GUIDE: { label: '입구 안내', color: 'green' },
  KIOSK: { label: '키오스크', color: 'cyan' },
};

/**
 * 도구줄의 재조회. 이 팝업에서는 `open()` 이 곧 조회다 —
 * 리본·오버레이가 그 한 번으로 함께 오므로 두 그리드가 같은 것을 부른다.
 *
 * `open()` 은 탭을 '기본' 으로 되돌리므로 보던 탭을 다시 세운다.
 * (`open()` 은 첫 `await` 앞에서 `activeKey` 를 바꾸니 부른 직후가 그 자리다.)
 */
function reload() {
  if (!currentDeviceId.value) return;
  const tab = activeKey.value;
  open(currentDeviceId.value);
  activeKey.value = tab;
}

// 리본 그리드 — 컬럼은 DeviceRibbon DTO 의 실제 칸이다.
// (예전에는 text·ribbonType 같은 DTO 에 없는 칸을 그려서 전부 빈 칸으로 나왔다.)
const [RibbonGrid] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      { field: 'mediaSourceName', minWidth: 180, title: '장식 이미지' },
      {
        field: 'position',
        // 두 값(positionLeft · positionTop)을 합쳐 그리는 칸이라 걸러 낼 값이 없다.
        params: { filter: false, sort: false },
        slots: { default: 'position' },
        title: '위치 (L, T)',
        width: 150,
      },
      {
        field: 'size',
        params: { filter: false, sort: false },
        slots: { default: 'size' },
        title: '크기 (W×H)',
        width: 150,
      },
      { field: 'sortOrder', title: '순서', width: 80 },
      { field: 'remark', minWidth: 120, title: '비고' },
    ],
    emptyText: '등록된 리본 설정 정보가 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    gridFeatures: { onRefresh: () => reload() },
    // 팝업 안이라 부모가 높이를 주지 않는다. 숫자로 준다.
    height: 340,
    // 전량을 한 번에 넘긴다. 페이저를 두지 않는다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'id' },
  } as any,
});

// 오버레이 그리드 — 컬럼은 DeviceTextOverlay DTO 의 실제 칸이다.
// (예전에는 overlayKey·textValue·isEnabled 같은 DTO 에 없는 칸을 그려서 빈 칸으로 나왔다.)
const [OverlayGrid] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      { field: 'textContent', minWidth: 180, title: '내용' },
      {
        field: 'fontSize',
        formatter: ({ cellValue }: any) => `${cellValue}px`,
        title: '폰트 크기',
        width: 90,
      },
      {
        field: 'fontColor',
        minWidth: 120,
        slots: { default: 'fontColor' },
        title: '폰트색',
      },
      {
        field: 'backgroundColor',
        minWidth: 120,
        slots: { default: 'backgroundColor' },
        title: '배경색',
      },
      { field: 'textAlign', title: '정렬', width: 80 },
      {
        field: 'position',
        // 두 값(positionLeft · positionTop)을 합쳐 그리는 칸이라 걸러 낼 값이 없다.
        params: { filter: false, sort: false },
        slots: { default: 'position' },
        title: '위치 (L, T)',
        width: 150,
      },
      { field: 'sortOrder', title: '순서', width: 80 },
    ],
    emptyText: '등록된 텍스트 오버레이 정보가 없습니다.',
    gridFeatures: { onRefresh: () => reload() },
    height: 340,
    // 전량을 한 번에 넘긴다. 페이저를 두지 않는다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'id' },
  } as any,
});

async function open(deviceId: string) {
  visible.value = true;
  loading.value = true;
  activeKey.value = 'basic';
  currentDeviceId.value = deviceId;
  
  // 데이터 초기화
  deviceData.value = null;
  attributeData.value = null;
  ribbonData.value = [];
  overlayData.value = [];

  try {
    // 봉투는 API 모듈이 벗겨서 온다 — 준수사항 7.
    const [deviceRes, attrRes, ribbonsRes, overlaysRes] = await Promise.all([
      getDevice(deviceId).catch(() => null),
      getDeviceAttribute(deviceId).catch(() => null),
      getDeviceRibbons(deviceId).catch(() => null),
      getDeviceTextOverlays(deviceId).catch(() => null),
    ]);

    deviceData.value = deviceRes ?? null;
    attributeData.value = attrRes ?? null;
    ribbonData.value = ribbonsRes ?? [];
    overlayData.value = overlaysRes ?? [];
  } catch (err) {
    console.error('장비 상세 정보 로드 실패:', err);
  } finally {
    loading.value = false;
  }
}

defineExpose({
  open,
});
</script>

<template>
  <Modal
    v-model:open="visible"
    title="장비 상세 정보"
    :footer="null"
    width="800px"
    destroy-on-close
  >
    <div class="min-h-[400px] py-2 relative">
      <div v-if="loading" class="absolute inset-0 flex items-center justify-center bg-background/50 z-10">
        <Spin size="large" tip="장비 상세 정보를 로딩 중입니다..." />
      </div>

      <div v-else-if="!deviceData" class="flex flex-col items-center justify-center py-20 text-muted-foreground">
        <IconifyIcon icon="lucide:monitor-off" class="size-16 opacity-30 mb-4" />
        <p>장비 정보를 불러올 수 없습니다.</p>
      </div>

      <div v-else class="space-y-4">
        <!-- 장비 기본 헤더 요약 -->
        <div class="flex items-center justify-between border-b border-border pb-4">
          <div class="flex items-center gap-3">
            <IconifyIcon icon="mdi:monitor-dashboard" class="size-8 text-primary" />
            <div>
              <h3 class="text-lg font-bold text-foreground leading-tight">{{ deviceData.name }}</h3>
              <p class="text-xs text-muted-foreground mt-0.5">{{ deviceData.code }}</p>
            </div>
          </div>
          <div class="flex items-center gap-2">
            <Tag :color="deviceTypeMap[deviceData.deviceType]?.color || 'default'">
              {{ deviceTypeMap[deviceData.deviceType]?.label || deviceData.deviceType }}
            </Tag>
            <Badge :status="deviceData.status === 'ONLINE' ? 'success' : 'error'" :text="deviceData.status === 'ONLINE' ? '온라인' : '오프라인'" />
          </div>
        </div>

        <!-- 탭 영역 -->
        <Tabs v-model:activeKey="activeKey" class="w-full">
          <!-- 1. 장비관리(기본) 탭 -->
          <Tabs.TabPane key="basic" tab="장비 기본 정보">
            <Descriptions bordered :column="2" size="small" class="mt-2">
              <Descriptions.Item label="장비명">{{ deviceData.name }}</Descriptions.Item>
              <Descriptions.Item label="짧은 명칭">{{ deviceData.shortName || '-' }}</Descriptions.Item>
              <Descriptions.Item label="장비 코드">{{ deviceData.code }}</Descriptions.Item>
              <Descriptions.Item label="장비 유형">{{ deviceTypeMap[deviceData.deviceType]?.label || deviceData.deviceType }}</Descriptions.Item>
              <Descriptions.Item label="IP 주소">{{ deviceData.ipAddress || '-' }}</Descriptions.Item>
              <Descriptions.Item label="MAC 주소">{{ deviceData.macAddress || '-' }}</Descriptions.Item>
              <Descriptions.Item label="소속 건물">{{ deviceData.buildingShortName || '-' }}</Descriptions.Item>
              <Descriptions.Item label="소속 층/호실">
                {{ deviceData.floorShortName || '' }} {{ deviceData.roomShortName || '' }}
                <span v-if="!deviceData.floorShortName && !deviceData.roomShortName">-</span>
              </Descriptions.Item>
              <Descriptions.Item label="정렬 순서" :span="2">{{ deviceData.sortOrder }}</Descriptions.Item>
            </Descriptions>
          </Tabs.TabPane>

          <!-- 2. 장비속성 탭 -->
          <Tabs.TabPane key="attribute" tab="장비 속성 정보">
            <div v-if="!attributeData" class="py-12">
              <Empty description="등록된 장비 속성 정보가 존재하지 않습니다." />
            </div>
            <div v-else class="space-y-6 mt-2 max-h-[450px] overflow-y-auto pr-1">
              <!-- 공통 표시 설정 -->
              <div>
                <h4 class="text-sm font-semibold border-l-4 border-primary pl-2 mb-3">화면 표시 설정</h4>
                <Descriptions bordered :column="2" size="small">
                  <Descriptions.Item label="화면 방향">
                    <Tag color="blue">{{ attributeData.displayOrientation }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="화면 표현">
                    <Tag color="cyan">{{ attributeData.portraitOrientation || '-' }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="영상 표현">
                    <Tag color="purple">{{ attributeData.videoOrientation || '-' }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="콘텐츠 간격">{{ attributeData.contentIntervalSec }}초</Descriptions.Item>
                  <Descriptions.Item label="대기화면 사용">
                    <Badge :status="attributeData.isScreensaverEnabled ? 'processing' : 'default'" :text="attributeData.isScreensaverEnabled ? '사용' : '미사용'" />
                  </Descriptions.Item>
                  <Descriptions.Item label="대기화면 시간">{{ attributeData.screensaverTimeoutSec }}초</Descriptions.Item>
                  <Descriptions.Item label="화면 여백 (위/아래/좌/우)" :span="2">
                    T: {{ attributeData.displayPaddingTop }}%, B: {{ attributeData.displayPaddingBottom }}%, 
                    L: {{ attributeData.displayPaddingLeft }}%, R: {{ attributeData.displayPaddingRight }}%
                  </Descriptions.Item>
                </Descriptions>
              </div>

              <!-- 영정사진/추모 콘텐츠 설정 -->
              <div>
                <h4 class="text-sm font-semibold border-l-4 border-primary pl-2 mb-3">추모 콘텐츠 설정</h4>
                <Descriptions bordered :column="2" size="small">
                  <Descriptions.Item label="영정사진 사용">
                    <Badge :status="attributeData.isMemorialPhotoEnabled ? 'processing' : 'default'" :text="attributeData.isMemorialPhotoEnabled ? '사용' : '미사용'" />
                  </Descriptions.Item>
                  <Descriptions.Item label="전환 효과">{{ attributeData.memorialPhotoEffect }}</Descriptions.Item>
                  <Descriptions.Item label="사진 세로 정렬">{{ attributeData.photoVerticalAlignment }}</Descriptions.Item>
                  <Descriptions.Item label="사진 가로 정렬">{{ attributeData.photoHorizontalAlignment }}</Descriptions.Item>
                  <Descriptions.Item label="고인명 노출">
                    <Badge :status="attributeData.isDeceasedNameVisible ? 'success' : 'default'" :text="attributeData.isDeceasedNameVisible ? '노출' : '숨김'" />
                  </Descriptions.Item>
                  <Descriptions.Item label="연락처 노출">
                    <Badge :status="attributeData.isFamilyContactVisible ? 'success' : 'default'" :text="attributeData.isFamilyContactVisible ? '노출' : '숨김'" />
                  </Descriptions.Item>
                  <Descriptions.Item label="영정 여백 (위/아래/좌/우)" :span="2">
                    T: {{ attributeData.memorialPaddingTop }}%, B: {{ attributeData.memorialPaddingBottom }}%, 
                    L: {{ attributeData.memorialPaddingLeft }}%, R: {{ attributeData.memorialPaddingRight }}%
                  </Descriptions.Item>
                </Descriptions>
              </div>

              <!-- 멀티미디어 콘텐츠 설정 -->
              <div>
                <h4 class="text-sm font-semibold border-l-4 border-primary pl-2 mb-3">멀티미디어 설정</h4>
                <Descriptions bordered :column="2" size="small">
                  <Descriptions.Item label="동영상 활성화">
                    <Badge :status="attributeData.isVideoEnabled ? 'processing' : 'default'" :text="attributeData.isVideoEnabled ? '사용' : '미사용'" />
                  </Descriptions.Item>
                  <Descriptions.Item label="음악 활성화">
                    <Badge :status="attributeData.isMusicEnabled ? 'processing' : 'default'" :text="attributeData.isMusicEnabled ? '사용' : '미사용'" />
                  </Descriptions.Item>
                  <Descriptions.Item label="재생 동영상 ID">{{ attributeData.videoId || '-' }}</Descriptions.Item>
                  <Descriptions.Item label="재생 음악 ID">{{ attributeData.musicId || '-' }}</Descriptions.Item>
                  <Descriptions.Item label="볼륨 / 음소거">
                    Vol: {{ attributeData.musicVolume ?? 50 }}% / 
                    <Tag :color="attributeData.isMuted ? 'red' : 'green'">{{ attributeData.isMuted ? '음소거' : '소리켬' }}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="반복 재생">
                    <Tag :color="attributeData.isMediaLoop ? 'blue' : 'default'">{{ attributeData.isMediaLoop ? '반복' : '1회' }}</Tag>
                  </Descriptions.Item>
                </Descriptions>
              </div>
            </div>
          </Tabs.TabPane>

          <!-- 3. 리본설정 탭 -->
          <Tabs.TabPane key="ribbon" tab="리본 설정">
            <div class="mt-2">
              <RibbonGrid :table-data="ribbonData">
                <template #position="{ row }">
                  L: {{ row.positionLeft }}%, T: {{ row.positionTop }}%
                </template>
                <template #size="{ row }">
                  {{ row.width }}% × {{ row.height }}%
                </template>
              </RibbonGrid>
            </div>
          </Tabs.TabPane>

          <!-- 4. 텍스트 오버레이 탭 -->
          <Tabs.TabPane key="overlay" tab="텍스트 오버레이">
            <div class="mt-2">
              <OverlayGrid :table-data="overlayData">
                <template #fontColor="{ row }">
                  <span class="inline-flex items-center gap-1.5 font-mono">
                    <span class="size-3 rounded-full border border-border inline-block" :style="{ backgroundColor: row.fontColor }"></span>
                    {{ row.fontColor }}
                  </span>
                </template>
                <template #backgroundColor="{ row }">
                  <span class="inline-flex items-center gap-1.5 font-mono">
                    <span class="size-3 rounded-full border border-border inline-block" :style="{ backgroundColor: row.backgroundColor }"></span>
                    {{ row.backgroundColor }}
                  </span>
                </template>
                <template #position="{ row }">
                  L: {{ row.positionLeft }}%, T: {{ row.positionTop }}%
                </template>
              </OverlayGrid>
            </div>
          </Tabs.TabPane>
        </Tabs>
      </div>
    </div>
  </Modal>
</template>

<style scoped>
:deep(.ant-descriptions-item-label) {
  font-weight: 600;
  width: 140px;
}
</style>
