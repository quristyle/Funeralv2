<script lang="ts" setup>
import { computed, onMounted, onUnmounted, ref } from 'vue';
import * as signalR from '@microsoft/signalr';
import { ColPage } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import type { BuildingApi } from '#/api/funeral/building';
import { useDeviceGrid } from './composables/use-device-grid';
import { useDeviceConfig } from './composables/use-device-config';
import { useDeviceAttribute } from './composables/use-device-attribute';
import { getDevice } from '#/api/funeral/building';
import DeviceListPanel from './modules/device-list-panel.vue';
import DeviceDetailPanel from './modules/device-detail-panel.vue';
import DeviceFormModal from './modules/device-form-modal.vue';

// ─── 공통 상태 ──────────────────────────────────────────────────
const selectedDevice = ref<BuildingApi.Device | null>(null);
const activeTab = ref<string>('config');
const showConfigPanel = computed(() => selectedDevice.value !== null);

// ─── 모달 ref ───────────────────────────────────────────────────
const deviceFormModalRef = ref<InstanceType<typeof DeviceFormModal> | null>(null);

// ─── composables ──────────────────────────────────────────────
const configComposable = useDeviceConfig();
const attrComposable = useDeviceAttribute();

function closePanel() {
  selectedDevice.value = null;
  configComposable.resetConfig();
  attrComposable.resetAttr();
  gridComposable.clearCurrentRow();
}

function onRowClick(row: BuildingApi.Device) {
  if (selectedDevice.value?.id === row.id) return;
  selectedDevice.value = row;
  gridComposable.setCurrentRow(row);
  configComposable.loadDeviceConfig(row.id);
  attrComposable.loadDeviceAttribute(row.id);
}

/** 장비 관리 탭에서 정보 저장 후 호출되는 함수 */
async function onDeviceManaged() {
  // 그리드 목록 새로고침
  gridComposable.gridApi.query();
  if (selectedDevice.value) {
    // 현재 선택된 장비의 최신 정보를 다시 불러와 패널에 반영
    const updatedDevice = await getDevice(selectedDevice.value.id);
    selectedDevice.value = updatedDevice;
  }
}

const gridComposable = useDeviceGrid(selectedDevice, onRowClick, closePanel);

// ---------------------------------------------------------------------------
// 장비 온라인/오프라인 상태 실시간 반영
//
// 상태는 장비가 SignalR 로 접속/이탈할 때 서버가 DeviceStatusChanged 로 방송한다.
// 이 화면이 그 이벤트를 구독하지 않아, 목록을 불러온 시점의 상태가 그대로 굳어
// 실제로는 온라인인 장비가 오프라인으로 보이는 문제가 있었다.
// ---------------------------------------------------------------------------
let statusHub: signalR.HubConnection | null = null;
let gridRefreshTimer: ReturnType<typeof setTimeout> | null = null;

/** 여러 장비 상태가 연달아 바뀔 때 목록 재조회가 폭주하지 않도록 묶어서 처리한다. */
function scheduleGridRefresh() {
  if (gridRefreshTimer) clearTimeout(gridRefreshTimer);
  gridRefreshTimer = setTimeout(() => {
    gridComposable.gridApi.query();
    gridRefreshTimer = null;
  }, 500);
}

function initStatusHub() {
  statusHub = new signalR.HubConnectionBuilder()
    .withUrl('/api/funeral/hubs/device')
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (retryContext) => {
        if (retryContext.previousRetryCount === 0) return 0;
        if (retryContext.previousRetryCount === 1) return 2000;
        if (retryContext.previousRetryCount === 2) return 5000;
        return 10_000;
      },
    })
    .build();

  statusHub.on('DeviceStatusChanged', (deviceCode: string, status: string) => {
    // 서버는 문자열로 보내지만 Device.status 는 리터럴 유니온이라 좁혀서 넣는다.
    const nextStatus = status as BuildingApi.Device['status'];
    // 상세 패널에 열려 있는 장비라면 뱃지를 즉시 갱신한다.
    const current = selectedDevice.value;
    if (current && current.code === deviceCode) {
      selectedDevice.value = { ...current, status: nextStatus };
    }
    // 좌측 목록의 상태 표시도 맞춘다.
    scheduleGridRefresh();
  });

  statusHub.start().catch(() => {
    // 상태 표시는 부가 기능이므로 연결 실패가 화면 사용을 막지 않도록 조용히 넘어간다.
    // 자동 재연결이 계속 시도한다.
  });
}

onMounted(initStatusHub);

onUnmounted(() => {
  if (gridRefreshTimer) clearTimeout(gridRefreshTimer);
  statusHub?.stop();
  statusHub = null;
});

const {
  selectedCompanyId,
  selectedBuildingId,
  selectedFloorId,
  selectedRoomId,
  Grid,
  handleDelete,
  handleReboot,
} = gridComposable;

const {
  deviceConfig,
  configLoading,
  configSaving,
  powerOnTimeVal,
  powerOffTimeVal,
  rebootTimeVal,
  handleConfigSave,
  handleConfigReset,
} = configComposable;

const {
  deviceAttr,
  attrLoading,
  attrSaving,
  handleAttrSave,
  handleAttrReset,
} = attrComposable;
</script>

<template>
  <ColPage
    auto-content-height
    :left-width="50"
    :left-min-width="25"
    :left-max-width="80"
    :resizable="true"
    :split-line="true"
    :split-handle="true"
  >
    <!-- ── 좌측: 장비 목록 패널 ────────────────────────────────── -->
    <template #left>
      <DeviceListPanel
        :Grid="Grid"
        :selected-company-id="selectedCompanyId"
        :selected-building-id="selectedBuildingId"
        :selected-floor-id="selectedFloorId"
        :selected-room-id="selectedRoomId"
        :show-config-panel="showConfigPanel"
        @update:selected-company-id="(v) => selectedCompanyId = v"
        @update:selected-building-id="(v) => selectedBuildingId = v"
        @update:selected-floor-id="(v) => selectedFloorId = v"
        @update:selected-room-id="(v) => selectedRoomId = v"
        @create="deviceFormModalRef?.openCreate()"
        @edit="(row) => deviceFormModalRef?.openEdit(row)"
        @delete="handleDelete"
        @reboot="handleReboot"
      />
    </template>

    <!-- ── 우측: 장비 상세 패널 ────────────────────────────────── -->
    <transition name="slide-panel">
      <DeviceDetailPanel
        v-if="showConfigPanel && selectedDevice"
        :device="selectedDevice"
        :active-tab="activeTab"
        :device-config="deviceConfig"
        :config-loading="configLoading"
        :config-saving="configSaving"
        :power-on-time-val="powerOnTimeVal"
        :power-off-time-val="powerOffTimeVal"
        :reboot-time-val="rebootTimeVal"
        :device-attr="deviceAttr"
        :attr-loading="attrLoading"
        :attr-saving="attrSaving"
        @close="closePanel"
        @update:activeTab="(val) => activeTab = val"
        @config-save="handleConfigSave(selectedDevice!.id)"
        @config-reset="handleConfigReset(selectedDevice!.id)"
        @update:powerOnTimeVal="(val) => powerOnTimeVal = val"
        @update:powerOffTimeVal="(val) => powerOffTimeVal = val"
        @update:rebootTimeVal="(val) => rebootTimeVal = val"
        @device-managed="onDeviceManaged"
        @attr-save="handleAttrSave(selectedDevice!.id)"
        @attr-reset="handleAttrReset(selectedDevice!.id)"
      />

      <!-- 미선택 안내 -->
      <div
        v-else
        class="flex h-full items-center justify-center text-muted-foreground"
      >
        <div class="text-center">
          <IconifyIcon icon="lucide:monitor-dot" class="mx-auto mb-3 size-12 opacity-30" />
          <p class="text-sm">장비를 선택하면 설정 패널이 표시됩니다.</p>
        </div>
      </div>
    </transition>

    <!-- ── 장비 등록/수정 모달 ──────────────────────────────────── -->
    <DeviceFormModal
      :ref="(el) => { deviceFormModalRef = el as InstanceType<typeof DeviceFormModal> | null }"
      :selected-company-id="selectedCompanyId"
      :selected-building-id="selectedBuildingId"
      :selected-floor-id="selectedFloorId"
      :selected-room-id="selectedRoomId"
      @saved="gridComposable.gridApi.query()"
    />
  </ColPage>
</template>

<style scoped>
/* ── 설정 패널 슬라이드 애니메이션 ────────────────────── */
.slide-panel-enter-active,
.slide-panel-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}
.slide-panel-enter-from,
.slide-panel-leave-to {
  opacity: 0;
  transform: translateX(16px);
}

/* ── VXE 현재 행 하이라이트 ──────────────────────────── */
:deep(.vxe-table--current-row) {
  background-color: hsl(var(--primary) / 0.1) !important;
}
:deep(.vxe-table--current-row td:first-child) {
  box-shadow: inset 3px 0 0 hsl(var(--primary));
}
</style>
