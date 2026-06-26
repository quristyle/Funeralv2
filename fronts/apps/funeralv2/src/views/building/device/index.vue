<script lang="ts" setup>
import { ref, computed } from 'vue';
import { ColPage } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import type { BuildingApi } from '#/api/building';
import { useDeviceGrid } from './composables/use-device-grid';
import { useDeviceConfig } from './composables/use-device-config';
import { useDeviceAttribute } from './composables/use-device-attribute';
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
  activeTab.value = 'config';
  gridComposable.setCurrentRow(row);
  configComposable.loadDeviceConfig(row.id);
  attrComposable.loadDeviceAttribute(row.id);
}

const gridComposable = useDeviceGrid(selectedDevice, onRowClick, closePanel);

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
  loadDeviceConfig,
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
