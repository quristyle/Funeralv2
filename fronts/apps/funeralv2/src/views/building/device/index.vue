<script lang="ts" setup>
import { ref, watch, computed } from 'vue';
import { useDebounceFn } from '@vueuse/core';
import { Page, useVbenModal } from '@vben/common-ui';
import { IconifyIcon, Plus } from '@vben/icons';
import {
  Button, message, Popconfirm, Form, Input, Select,
  Badge, Tooltip, Slider, Switch, TimePicker, Spin, Empty,
} from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import {
  getDevices, createDevice, updateDevice, deleteDevice,
  getDeviceConfigs, updateDeviceConfig,
} from '#/api/building';
import type { BuildingApi } from '#/api/building';
import BizSelect from '#/components/BizSelect.vue';
import dayjs from 'dayjs';

// ─── 상단 필터 상태 ─────────────────────────────────────────────
const selectedCompanyId = ref<string>('');
const selectedBuildingId = ref<string>('');
const selectedFloorId = ref<string>('');
const selectedRoomId = ref<string>('');

// ─── 우측 설정 패널 상태 ─────────────────────────────────────────
const selectedDevice = ref<BuildingApi.Device | null>(null);
const deviceConfig = ref<BuildingApi.DeviceConfig | null>(null);
const configLoading = ref(false);
const configSaving = ref(false);
const powerOnTimeVal = ref<any>(null);
const powerOffTimeVal = ref<any>(null);
const rebootTimeVal = ref<any>(null);

const showConfigPanel = computed(() => selectedDevice.value !== null);

// ─── SplitPane 상태 ───────────────────────────────────────────────
const SPLIT_MIN = 25;  // 최소 너비 %
const SPLIT_MAX = 80;  // 최대 너비 %
const leftPct = ref(50); // 좌측 패널 너비 %
const isDragging = ref(false);
const splitContainerRef = ref<HTMLElement | null>(null);

function onSplitMousedown(e: MouseEvent) {
  e.preventDefault();
  isDragging.value = true;
  const startX = e.clientX;
  const startPct = leftPct.value;
  const containerWidth = splitContainerRef.value?.offsetWidth ?? window.innerWidth;

  function onMousemove(ev: MouseEvent) {
    const delta = ((ev.clientX - startX) / containerWidth) * 100;
    leftPct.value = Math.min(SPLIT_MAX, Math.max(SPLIT_MIN, startPct + delta));
  }
  function onMouseup() {
    isDragging.value = false;
    window.removeEventListener('mousemove', onMousemove);
    window.removeEventListener('mouseup', onMouseup);
  }
  window.addEventListener('mousemove', onMousemove);
  window.addEventListener('mouseup', onMouseup);
}

// ─── 장비 목록 그리드 ────────────────────────────────────────────
const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '장비명', minWidth: 120 },
      { field: 'code', title: '코드', minWidth: 100 },
      {
        field: 'deviceType',
        title: '유형',
        minWidth: 80,
        formatter: ({ cellValue }: { cellValue: any }) => {
          const map: Record<string, string> = {
            DID: 'DID',
            KIOSK: '키오스크',
            SIGNBOARD: '현판',
          };
          return map[cellValue] ?? cellValue;
        },
      },
      {
        field: 'status',
        title: '상태',
        minWidth: 80,
        slots: { default: 'status-badge' },
      },
      {
        field: 'action',
        title: '작업',
        width: 110,
        fixed: 'right',
        slots: { default: 'action' },
      },
    ],
    height: 'auto',
    rowConfig: { isHover: true, isCurrent: true },
    proxyConfig: {
      autoLoad: false,
      ajax: {
        query: async () => {
          const params: {
            companyId?: string;
            buildingId?: string;
            floorId?: string;
            roomId?: string;
          } = {};
          if (selectedCompanyId.value) params.companyId = selectedCompanyId.value;
          if (selectedBuildingId.value) params.buildingId = selectedBuildingId.value;
          if (selectedFloorId.value) params.floorId = selectedFloorId.value;
          if (selectedRoomId.value) params.roomId = selectedRoomId.value;

          selectedDevice.value = null;
          deviceConfig.value = null;
          return await getDevices(params);
        },
      },
    },
  },
  // ★ gridEvents는 gridOptions 밖의 별도 최상위 prop (VxeGridProps 참조)
  gridEvents: {
    cellClick: ({ row }: { row: BuildingApi.Device }) => {
      onDeviceRowClick(row);
    },
  },
});


// ─── 필터 변경 → debounced 재조회 ────────────────────────────────
const debouncedQuery = useDebounceFn(() => {
  gridApi.query();
}, 300);

watch(
  [selectedCompanyId, selectedBuildingId, selectedFloorId, selectedRoomId],
  () => debouncedQuery(),
);

// ─── 행 클릭 처리 ────────────────────────────────────────────────
function onDeviceRowClick(row: BuildingApi.Device) {
  if (selectedDevice.value?.id === row.id) return;
  selectedDevice.value = row;
  // 그리드 내 현재 행 하이라이트
  gridApi.grid?.setCurrentRow?.(row);
  loadDeviceConfig(row.id);
}

// ─── 장비 설정 로드 ───────────────────────────────────────────────
async function loadDeviceConfig(deviceId: string) {
  configLoading.value = true;
  deviceConfig.value = null;
  powerOnTimeVal.value = null;
  powerOffTimeVal.value = null;
  rebootTimeVal.value = null;
  try {
    const res = await getDeviceConfigs({ deviceId });
    const raw = (res as any)?.result ?? res;
    const list: BuildingApi.DeviceConfig[] = Array.isArray(raw) ? raw : [];
    const found = list[0] ?? null;
    deviceConfig.value = found;
    if (found) {
      powerOnTimeVal.value = found.powerOnTime ? dayjs(found.powerOnTime, 'HH:mm') : null;
      powerOffTimeVal.value = found.powerOffTime ? dayjs(found.powerOffTime, 'HH:mm') : null;
      rebootTimeVal.value = found.rebootTime ? dayjs(found.rebootTime, 'HH:mm') : null;
    }
  } catch {
    message.error('장비 설정을 불러오는 데 실패했습니다.');
  } finally {
    configLoading.value = false;
  }
}

// ─── 설정 저장 ───────────────────────────────────────────────────
async function handleConfigSave() {
  if (!deviceConfig.value) return;
  configSaving.value = true;
  try {
    const payload: Partial<BuildingApi.DeviceConfig> = {
      ...deviceConfig.value,
      powerOnTime: powerOnTimeVal.value ? powerOnTimeVal.value.format('HH:mm') : '',
      powerOffTime: powerOffTimeVal.value ? powerOffTimeVal.value.format('HH:mm') : '',
      rebootTime: rebootTimeVal.value ? rebootTimeVal.value.format('HH:mm') : '',
    };
    await updateDeviceConfig(deviceConfig.value.id, payload);
    message.success('장비 설정이 저장되었습니다.');
  } catch {
    message.error('설정 저장에 실패했습니다.');
  } finally {
    configSaving.value = false;
  }
}

function closeConfigPanel() {
  selectedDevice.value = null;
  deviceConfig.value = null;
  gridApi.grid?.clearCurrentRow?.();
}

// ─── 장비 등록/수정 모달 ─────────────────────────────────────────
const [DeviceModal, deviceModalApi] = useVbenModal({
  title: '장비 정보 설정',
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  },
});

const formModel = ref({
  id: '',
  name: '',
  code: '',
  deviceType: 'DID',
  ipAddress: '',
  macAddress: '',
  status: 'UNKNOWN' as 'ONLINE' | 'OFFLINE' | 'UNKNOWN',
  companyId: '',
  buildingId: '',
  floorId: '',
  roomId: '',
});

function onCreate() {
  formModel.value = {
    id: '',
    name: '',
    code: '',
    deviceType: 'DID',
    ipAddress: '',
    macAddress: '',
    status: 'UNKNOWN',
    companyId: selectedCompanyId.value,
    buildingId: selectedBuildingId.value,
    floorId: selectedFloorId.value,
    roomId: selectedRoomId.value,
  };
  deviceModalApi.open();
}

function onEdit(row: any) {
  formModel.value = { ...row };
  deviceModalApi.open();
}

async function onDelete(row: any) {
  try {
    await deleteDevice(row.id);
    message.success('장비가 삭제되었습니다.');
    if (selectedDevice.value?.id === row.id) {
      closeConfigPanel();
    }
    gridApi.query();
  } catch {
    message.error('삭제 실패');
  }
}

function handleReboot(row: any) {
  message.loading({ content: `${row.name} 재부팅 명령 송신 중...`, key: 'reboot' });
  setTimeout(() => {
    message.success({ content: '명령 송신 성공. 장비가 곧 리부팅됩니다.', key: 'reboot', duration: 2 });
  }, 1000);
}

async function handleSave() {
  try {
    if (formModel.value.id) {
      await updateDevice(formModel.value.id, formModel.value);
      message.success('장비 정보가 수정되었습니다.');
    } else {
      await createDevice(formModel.value);
      message.success('장비가 성공적으로 등록되었습니다.');
    }
    deviceModalApi.close();
    gridApi.query();
  } catch {
    message.error('저장 실패');
  }
}
</script>

<template>
  <Page auto-content-height class="flex flex-col">
    <!-- ── 상단 필터 바 ──────────────────────────────────────── -->
    <div class="mb-3 flex flex-wrap items-center justify-between gap-3 bg-card p-3 rounded-lg shadow-sm border border-border shrink-0">
      <div class="flex flex-wrap items-center gap-3">
        <div class="flex items-center gap-2">
          <span class="text-xs font-semibold whitespace-nowrap text-muted-foreground">회사</span>
          <BizSelect
            v-model:value="selectedCompanyId"
            type="company"
            auto-select-first
            placeholder="회사 선택"
            class="w-40"
            show-search
            option-filter-prop="label"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="text-xs font-semibold whitespace-nowrap text-muted-foreground">건물</span>
          <BizSelect
            v-model:value="selectedBuildingId"
            type="building"
            :params="{ companyId: selectedCompanyId }"
            auto-select-first
            placeholder="건물 선택"
            class="w-40"
            show-search
            option-filter-prop="label"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="text-xs font-semibold whitespace-nowrap text-muted-foreground">층</span>
          <BizSelect
            v-model:value="selectedFloorId"
            type="floor"
            :params="{ buildingId: selectedBuildingId }"
            auto-select-first
            placeholder="층 선택"
            class="w-32"
            show-search
            option-filter-prop="label"
          />
        </div>
        <div class="flex items-center gap-2">
          <span class="text-xs font-semibold whitespace-nowrap text-muted-foreground">호실</span>
          <BizSelect
            v-model:value="selectedRoomId"
            type="room"
            :params="{ floorId: selectedFloorId }"
            allow-clear
            placeholder="전체 호실"
            class="w-32"
            show-search
            option-filter-prop="label"
          />
        </div>
      </div>
      <Button type="primary" size="small" @click="onCreate">
        <Plus class="size-4 mr-1" />
        장비 등록
      </Button>
    </div>

    <!-- ── SplitPane 영역 ─────────────────────────────────────── -->
    <div
      ref="splitContainerRef"
      class="relative flex flex-1 min-h-0 gap-0"
      :class="{ 'select-none': isDragging }"
    >
      <!-- ▌ 좌측: 장비 목록 -->
      <div
        class="flex flex-col min-w-0 min-h-0 overflow-hidden"
        :style="showConfigPanel ? { width: leftPct + '%' } : { width: '100%' }"
      >
        <Grid table-title="장비 목록">
          <template #status-badge="{ row }">
            <Badge v-if="row.status === 'ONLINE'" status="success" text="온라인" />
            <Badge v-else-if="row.status === 'OFFLINE'" status="error" text="오프라인" />
            <Badge v-else status="default" text="미확인" />
          </template>

          <template #action="{ row }">
            <div class="flex gap-1">
              <Tooltip title="수정">
                <Button type="link" size="small" @click.stop="onEdit(row)">
                  <IconifyIcon icon="lucide:edit" class="size-4" />
                </Button>
              </Tooltip>
              <Tooltip title="원격 재부팅">
                <Button type="link" size="small" @click.stop="handleReboot(row)">
                  <IconifyIcon icon="lucide:power-off" class="size-4" />
                </Button>
              </Tooltip>
              <Popconfirm title="삭제하시겠습니까?" @confirm="onDelete(row)">
                <Tooltip title="삭제">
                  <Button type="link" size="small" danger @click.stop>
                    <IconifyIcon icon="lucide:trash-2" class="size-4" />
                  </Button>
                </Tooltip>
              </Popconfirm>
            </div>
          </template>
        </Grid>
        <!-- 우측 패널 없을 때 안내 문구 -->
        <div
          v-if="!showConfigPanel"
          class="mt-2 text-center text-xs text-muted-foreground"
        >
          장비를 클릭하면 오른쪽에 설정 패널이 표시됩니다.
        </div>
      </div>

      <!-- ▌ 드래그 구분선 (우측 패널 표시 시에만) -->
      <div
        v-if="showConfigPanel"
        class="split-divider"
        @mousedown="onSplitMousedown"
      >
        <div class="split-divider-handle" />
      </div>

      <!-- ▌ 우측: 장비 설정 패널 -->
      <transition name="slide-panel">
        <div
          v-if="showConfigPanel"
          class="flex flex-col min-h-0 min-w-0 bg-card border border-border rounded-lg shadow-sm"
          :style="{ width: (100 - leftPct) + '%' }"
        >
          <!-- 패널 헤더 -->
          <div class="flex items-center justify-between px-4 py-2 border-b border-border bg-muted/40 shrink-0 rounded-t-lg">
            <div class="flex items-center gap-2 min-w-0">
              <IconifyIcon icon="lucide:settings-2" class="size-4 text-primary shrink-0" />
              <div class="min-w-0">
                <div class="font-semibold text-sm truncate">{{ selectedDevice?.name }}</div>
                <div class="text-xs text-muted-foreground truncate">
                  {{ selectedDevice?.code }}
                  <span v-if="selectedDevice?.ipAddress"> · {{ selectedDevice.ipAddress }}</span>
                </div>
              </div>
            </div>
            <div class="flex items-center gap-2 shrink-0">
              <Badge v-if="selectedDevice?.status === 'ONLINE'" status="success" text="온라인" />
              <Badge v-else-if="selectedDevice?.status === 'OFFLINE'" status="error" text="오프라인" />
              <Badge v-else status="default" text="미확인" />
              <Tooltip title="패널 닫기">
                <Button type="text" size="small" @click="closeConfigPanel">
                  <IconifyIcon icon="lucide:x" class="size-4" />
                </Button>
              </Tooltip>
            </div>
          </div>

          <!-- 로딩 -->
          <div v-if="configLoading" class="flex flex-1 items-center justify-center py-16">
            <Spin tip="설정 불러오는 중..." />
          </div>

          <!-- 설정 없음 -->
          <div v-else-if="!deviceConfig" class="flex flex-1 items-center justify-center py-10">
            <Empty description="등록된 설정이 없습니다." />
          </div>

          <!-- 설정 폼 -->
          <div v-else class="flex-1 overflow-auto px-4 py-3">
            <Form layout="vertical" size="small">
              <Form.Item label="기기 음량 (Volume)">
                <Slider v-model:value="deviceConfig.volume" :min="0" :max="100" />
                <div class="text-right text-xs text-muted-foreground">{{ deviceConfig.volume }}%</div>
              </Form.Item>

              <Form.Item label="화면 밝기 (Brightness)">
                <Slider v-model:value="deviceConfig.brightness" :min="0" :max="100" />
                <div class="text-right text-xs text-muted-foreground">{{ deviceConfig.brightness }}%</div>
              </Form.Item>

              <div class="my-3 border-t border-border" />

              <Form.Item label="자동 전원 제어">
                <Switch
                  v-model:checked="deviceConfig.isAutoPower"
                  checked-children="사용"
                  un-checked-children="사용안함"
                />
              </Form.Item>

              <template v-if="deviceConfig.isAutoPower">
                <div class="grid grid-cols-2 gap-3">
                  <Form.Item label="자동 켜짐 시각">
                    <TimePicker v-model:value="powerOnTimeVal" format="HH:mm" style="width: 100%" />
                  </Form.Item>
                  <Form.Item label="자동 꺼짐 시각">
                    <TimePicker v-model:value="powerOffTimeVal" format="HH:mm" style="width: 100%" />
                  </Form.Item>
                </div>
              </template>

              <div class="my-3 border-t border-border" />

              <Form.Item label="일일 자동 재시작 시각">
                <TimePicker v-model:value="rebootTimeVal" format="HH:mm" style="width: 100%" />
              </Form.Item>
            </Form>
          </div>

          <!-- 저장 버튼 -->
          <div
            v-if="deviceConfig && !configLoading"
            class="px-4 py-2 border-t border-border bg-muted/40 shrink-0 flex justify-end gap-2 rounded-b-lg"
          >
            <Button @click="loadDeviceConfig(selectedDevice!.id)">초기화</Button>
            <Button type="primary" :loading="configSaving" @click="handleConfigSave">
              설정 저장
            </Button>
          </div>
        </div>
      </transition>
    </div>

    <!-- ── 장비 등록/수정 모달 ────────────────────────────────── -->
    <DeviceModal @ok="handleSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="장비 소속 위치" required>
            <div class="grid grid-cols-2 gap-4">
              <BizSelect
                v-model:value="formModel.buildingId"
                type="building"
                :params="{ companyId: formModel.companyId || selectedCompanyId }"
                placeholder="건물 선택"
              />
              <BizSelect
                v-model:value="formModel.floorId"
                type="floor"
                :params="{ buildingId: formModel.buildingId }"
                placeholder="층 선택"
              />
              <BizSelect
                v-model:value="formModel.roomId"
                type="room"
                :params="{ floorId: formModel.floorId }"
                placeholder="호실 선택"
              />
            </div>
          </Form.Item>
          <Form.Item label="장비명" required>
            <Input v-model:value="formModel.name" placeholder="예: 로비 대형 DID, 102호 현판" />
          </Form.Item>
          <Form.Item label="장비코드" required>
            <Input
              v-model:value="formModel.code"
              placeholder="예: DID_LOBBY_01"
              :disabled="!!formModel.id"
            />
          </Form.Item>
          <Form.Item label="장비 유형">
            <Select v-model:value="formModel.deviceType">
              <Select.Option value="DID">안내 모니터(DID)</Select.Option>
              <Select.Option value="KIOSK">무인 키오스크</Select.Option>
              <Select.Option value="SIGNBOARD">호실 현판</Select.Option>
            </Select>
          </Form.Item>
          <Form.Item label="IP 주소">
            <Input v-model:value="formModel.ipAddress" placeholder="예: 192.168.1.100" />
          </Form.Item>
          <Form.Item label="MAC 주소">
            <Input v-model:value="formModel.macAddress" placeholder="예: 00:0a:95:9d:68:16" />
          </Form.Item>
        </Form>
      </div>
    </DeviceModal>
  </Page>
</template>

<style scoped>
/* ── SplitPane 드래그 구분선 ──────────────────────────── */
.split-divider {
  position: relative;
  width: 6px;
  cursor: col-resize;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 10;
}

.split-divider::before {
  content: '';
  position: absolute;
  inset: 0;
  background: transparent;
  transition: background 0.15s;
}

.split-divider:hover::before,
.split-divider:active::before {
  background: hsl(var(--primary) / 0.15);
}

.split-divider-handle {
  width: 3px;
  height: 40px;
  border-radius: 9999px;
  background: hsl(var(--border));
  transition: background 0.15s, transform 0.15s;
}

.split-divider:hover .split-divider-handle {
  background: hsl(var(--primary) / 0.6);
  transform: scaleY(1.3);
}

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
