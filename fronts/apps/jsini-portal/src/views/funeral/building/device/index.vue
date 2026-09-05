<script lang="ts" setup>
import { onMounted, onUnmounted, ref } from 'vue';
import * as signalR from '@microsoft/signalr';
import { Page } from '@vben/common-ui';
import type { BuildingApi } from '#/api/funeral/building';
import { useDeviceGrid } from './composables/use-device-grid';
import DeviceListPanel from './modules/device-list-panel.vue';
import DeviceFormDrawer from './modules/device-form-drawer.vue';

// ---------------------------------------------------------------------------
// 장비 관리
//
// 예전에는 화면이 좌우로 갈려 왼쪽이 목록, 오른쪽이 상세 패널이었다. 둘 다 좁아서
// 목록은 열이 잘리고 설정은 세로로 길어졌다. 상세 패널을 걷어내고 그 일을 전부
// 서랍(`device-form-drawer.vue`)으로 옮겼다 — 목록은 화면 전체를 쓴다.
// ---------------------------------------------------------------------------

const deviceFormDrawerRef = ref<InstanceType<typeof DeviceFormDrawer> | null>(null);

/** 목록의 [수정] 아이콘 · 행 두 번 누르기 — 정보 · 설정 · 속성 · 화면 구성이 모두 서랍에 있다. */
function openEditDrawer(row: BuildingApi.Device) {
  deviceFormDrawerRef.value?.openEdit(row);
}

const gridComposable = useDeviceGrid(
  openEditDrawer,
  (row) => deviceFormDrawerRef.value?.closeIfDevice(row.id),
  () => deviceFormDrawerRef.value?.openCreate(),
);

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
    // 서랍이 그 장비를 열고 있다면 머리말 뱃지를 즉시 갱신한다.
    deviceFormDrawerRef.value?.applyStatus(deviceCode, nextStatus);
    // 목록의 상태 표시도 맞춘다.
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
</script>

<template>
  <Page auto-content-height>
    <DeviceListPanel
      :Grid="Grid"
      :selected-company-id="selectedCompanyId"
      :selected-building-id="selectedBuildingId"
      :selected-floor-id="selectedFloorId"
      :selected-room-id="selectedRoomId"
      @update:selected-company-id="(v) => selectedCompanyId = v"
      @update:selected-building-id="(v) => selectedBuildingId = v"
      @update:selected-floor-id="(v) => selectedFloorId = v"
      @update:selected-room-id="(v) => selectedRoomId = v"
      @create="deviceFormDrawerRef?.openCreate()"
      @edit="openEditDrawer"
      @delete="handleDelete"
      @reboot="handleReboot"
    />

    <!-- ── 장비 서랍 (등록 · 수정 · 설정 · 속성 · 화면 구성) ────── -->
    <DeviceFormDrawer
      :ref="(el) => { deviceFormDrawerRef = el as InstanceType<typeof DeviceFormDrawer> | null }"
      :selected-company-id="selectedCompanyId"
      :selected-building-id="selectedBuildingId"
      :selected-floor-id="selectedFloorId"
      :selected-room-id="selectedRoomId"
      @created="gridComposable.gridApi.query()"
      @updated="(device) => gridComposable.replaceRow(device)"
    />
  </Page>
</template>

<style scoped>
/* ── VXE 현재 행 하이라이트 ──────────────────────────── */
:deep(.vxe-table--current-row) {
  background-color: hsl(var(--primary) / 0.1) !important;
}
:deep(.vxe-table--current-row td:first-child) {
  box-shadow: inset 3px 0 0 hsl(var(--primary));
}
</style>
