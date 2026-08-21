<script lang="ts" setup>
import { ref } from 'vue';
import { Modal, Tabs, Descriptions, Tag, Badge, Spin, Table, Empty } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import { getDevice, getDeviceAttribute, getDeviceRibbons, getDeviceTextOverlays } from '#/api/funeral/building';
import type { BuildingApi } from '#/api/funeral/building';

const visible = ref(false);
const loading = ref(false);
const activeKey = ref('basic');

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

// 리본 테이블 컬럼
const ribbonColumns = [
  { title: '텍스트', dataIndex: 'text', key: 'text' },
  { title: '유형', dataIndex: 'ribbonType', key: 'ribbonType' },
  { title: '배경색', dataIndex: 'backgroundColor', key: 'backgroundColor' },
  { title: '폰트색', dataIndex: 'textColor', key: 'textColor' },
  { title: '정렬', dataIndex: 'alignment', key: 'alignment' },
];

// 오버레이 테이블 컬럼
const overlayColumns = [
  { title: '식별자', dataIndex: 'overlayKey', key: 'overlayKey' },
  { title: '내용', dataIndex: 'textValue', key: 'textValue' },
  { title: '위치 (X, Y)', dataIndex: 'position', key: 'position' },
  { title: '폰트 크기', dataIndex: 'fontSize', key: 'fontSize' },
  { title: '사용 여부', dataIndex: 'isEnabled', key: 'isEnabled' },
];

async function open(deviceId: string) {
  visible.value = true;
  loading.value = true;
  activeKey.value = 'basic';
  
  // 데이터 초기화
  deviceData.value = null;
  attributeData.value = null;
  ribbonData.value = [];
  overlayData.value = [];

  try {
    const [deviceRes, attrRes, ribbonsRes, overlaysRes] = await Promise.all([
      getDevice(deviceId).catch(() => null),
      getDeviceAttribute(deviceId).catch(() => null),
      getDeviceRibbons(deviceId).catch(() => null),
      getDeviceTextOverlays(deviceId).catch(() => null),
    ]);

    // 1. 장비 기본 정보 파싱
    if (deviceRes) {
      const rawDev = (deviceRes as any)?.result ?? deviceRes;
      deviceData.value = Array.isArray(rawDev) ? rawDev[0] : rawDev;
    }

    // 2. 장비 속성 파싱
    if (attrRes) {
      const rawAttr = (attrRes as any)?.result ?? attrRes;
      attributeData.value = Array.isArray(rawAttr) ? rawAttr[0] : rawAttr;
    }

    // 3. 리본 목록 파싱
    if (ribbonsRes) {
      const rawRibbons = (ribbonsRes as any)?.result ?? ribbonsRes;
      ribbonData.value = Array.isArray(rawRibbons) ? rawRibbons : [];
    }

    // 4. 오버레이 목록 파싱
    if (overlaysRes) {
      const rawOverlays = (overlaysRes as any)?.result ?? overlaysRes;
      overlayData.value = Array.isArray(rawOverlays) ? rawOverlays : [];
    }
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
              <Table
                :columns="ribbonColumns"
                :data-source="ribbonData"
                :pagination="false"
                size="small"
                row-key="id"
                bordered
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.key === 'backgroundColor'">
                    <span class="inline-flex items-center gap-1.5 font-mono">
                      <span class="size-3 rounded-full border border-border inline-block" :style="{ backgroundColor: record.backgroundColor }"></span>
                      {{ record.backgroundColor }}
                    </span>
                  </template>
                  <template v-else-if="column.key === 'textColor'">
                    <span class="inline-flex items-center gap-1.5 font-mono">
                      <span class="size-3 rounded-full border border-border inline-block" :style="{ backgroundColor: record.textColor }"></span>
                      {{ record.textColor }}
                    </span>
                  </template>
                </template>
              </Table>
              <div v-if="ribbonData.length === 0" class="py-12">
                <Empty description="등록된 리본 설정 정보가 없습니다." />
              </div>
            </div>
          </Tabs.TabPane>

          <!-- 4. 텍스트 오버레이 탭 -->
          <Tabs.TabPane key="overlay" tab="텍스트 오버레이">
            <div class="mt-2">
              <Table
                :columns="overlayColumns"
                :data-source="overlayData"
                :pagination="false"
                size="small"
                row-key="id"
                bordered
              >
                <template #bodyCell="{ column, record }">
                  <template v-if="column.key === 'position'">
                    X: {{ record.positionX }}%, Y: {{ record.positionY }}%
                  </template>
                  <template v-else-if="column.key === 'fontSize'">
                    {{ record.fontSize }}px
                  </template>
                  <template v-else-if="column.key === 'isEnabled'">
                    <Badge :status="record.isEnabled ? 'success' : 'default'" :text="record.isEnabled ? '사용' : '미사용'" />
                  </template>
                </template>
              </Table>
              <div v-if="overlayData.length === 0" class="py-12">
                <Empty description="등록된 텍스트 오버레이 정보가 없습니다." />
              </div>
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
