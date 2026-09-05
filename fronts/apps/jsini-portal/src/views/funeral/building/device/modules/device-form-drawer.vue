<script lang="ts" setup>
import { computed, ref } from 'vue';
import { useVbenDrawer } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { Badge, Button, Form, Input, InputNumber, message, Modal, Tabs, Tooltip } from 'ant-design-vue';
import dayjs from 'dayjs';
import {
  createDevice, getDevice, restartDeviceApp, setDeviceScreenPower,
} from '#/api/funeral/building';
import { getFuneralStatusDetail } from '#/api/funeral/status';
import type { BuildingApi } from '#/api/funeral/building';
import BizSelect from '#/components/BizSelect.vue';
import DictSelect from '#/components/DictSelect.vue';
import { getDeviceTypeInfo } from '../constants/device-type';
import { useDeviceAttribute } from '../composables/use-device-attribute';
import { useDeviceConfig } from '../composables/use-device-config';
import DeviceConfigTab from './device-config-tab.vue';
import DeviceDisplayTab from './device-display-tab.vue';
import DeviceManagementTab from './device-management-tab.vue';

// ---------------------------------------------------------------------------
// 장비 서랍 — 등록과 수정을 함께 맡는다.
//
// 예전에는 목록 오른쪽에 상세 패널이 붙어 있었고 서랍은 등록만 했다. 화면이 좌우로
// 갈려 목록도 설정도 좁았기에, 패널이 하던 일을 전부 이 서랍으로 옮겼다.
//
// 탭은 **사람이 얼마나 자주 만지는지** 순서다 (49번 문서 D-DV2).
//   화면 표시 — 매일 · 하드웨어 — 가끔 · 장비 정보 — 설치할 때 한 번
// 예전에는 가장 안 건드리는 대장 정보가 첫 탭이었다.
//
// 머리말에는 신원 + 실시간 상태 + **즉시 명령**(화면 켜기/끄기 · 앱 재시작)을 둔다.
// 저장되지 않는 일회성 명령이 저장되는 예약값 사이에 끼어 있던 것을 떼어낸 것이다
// (D-DV1).
//
// 등록에는 탭이 없다. 화면 표시 · 하드웨어는 모두 장비 ID 를 열쇠로 삼는데
// 아직 저장 전이라 ID 가 없기 때문이다. 등록을 마친 뒤 [수정] 으로 들어오면 된다.
// ---------------------------------------------------------------------------

const props = defineProps<{
  selectedCompanyId: string;
  selectedBuildingId: string;
  selectedFloorId: string;
  selectedRoomId: string;
}>();

const emit = defineEmits<{
  /** 새 장비를 등록했다 — 목록을 다시 부른다. */
  (e: 'created'): void;
  /** 열려 있는 장비의 정보가 바뀌었다 — 목록의 그 행만 갈아 끼운다. */
  (e: 'updated', device: BuildingApi.Device): void;
}>();

const mode = ref<'create' | 'edit'>('create');
/** 수정 중인 장비. 등록 중에는 null 이다. */
const device = ref<BuildingApi.Device | null>(null);
const activeTab = ref<string>('display');
/** 즉시 명령 전송 중 여부 */
const isCommandSending = ref(false);
/**
 * 이 장비가 있는 호실에 배정된 고인 이름 (D-DV6).
 * 있으면 조문 중이라는 뜻이라 화면을 건드리기 전에 알린다.
 */
const roomDeceasedName = ref<string>('');

const configComposable = useDeviceConfig();
const attrComposable = useDeviceAttribute();

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

const { deviceAttr, attrLoading, attrSaving, handleAttrSave } = attrComposable;

/** 수정은 미리보기와 설정을 나란히 놓아야 해서 넓다. 등록은 세로 폼 하나뿐이라 좁게 쓴다. */
const drawerWidthClass = computed(() =>
  mode.value === 'edit' ? 'w-[1100px]' : 'w-[560px]',
);

/** 마지막 접속 — 오프라인 장비가 언제부터 안 보이는지가 제일 궁금하다. */
const lastSeenLabel = computed(() => {
  const at = device.value?.lastSeenAt;
  if (!at) return null;
  const t = dayjs(at);
  if (!t.isValid()) return null;
  const minutes = dayjs().diff(t, 'minute');
  if (minutes < 1) return '방금';
  if (minutes < 60) return `${minutes}분 전`;
  if (minutes < 60 * 24) return `${Math.floor(minutes / 60)}시간 전`;
  return t.format('MM-DD HH:mm');
});

const formModel = ref({
  name: '',
  shortName: '',
  code: '',
  deviceType: 'FUNERAL_PORTRAIT',
  ipAddress: '',
  macAddress: '',
  status: 'UNKNOWN' as 'ONLINE' | 'OFFLINE' | 'UNKNOWN',
  companyId: '',
  buildingId: '',
  floorId: '',
  roomId: '',
  sortOrder: 0,
});

const [DeviceDrawer, deviceDrawerApi] = useVbenDrawer({
  title: '장비 등록',
  destroyOnClose: true,
  onConfirm: async () => {
    await handleCreate();
  },
  onClosed: () => {
    device.value = null;
    roomDeceasedName.value = '';
    configComposable.resetConfig();
    attrComposable.resetAttr();
  },
});

/** 등록 서랍 열기 */
function openCreate() {
  mode.value = 'create';
  device.value = null;
  formModel.value = {
    name: '',
    shortName: '',
    code: '',
    deviceType: 'FUNERAL_PORTRAIT',
    ipAddress: '',
    macAddress: '',
    status: 'UNKNOWN',
    companyId: props.selectedCompanyId,
    buildingId: props.selectedBuildingId,
    floorId: props.selectedFloorId,
    roomId: props.selectedRoomId,
    sortOrder: 0,
  };
  deviceDrawerApi.setState({
    title: '장비 등록',
    showConfirmButton: true,
    confirmText: '등록',
    cancelText: '취소',
  });
  deviceDrawerApi.open();
}

/**
 * 수정 서랍 열기 — 예전 오른쪽 패널이 하던 일 전부.
 *
 * 저장 단추는 탭마다 따로 있으므로 서랍 아래의 [확인] 은 감춘다.
 * 확인 하나로 셋을 한꺼번에 저장하는 것처럼 보이면 거짓말이 된다.
 */
function openEdit(row: BuildingApi.Device) {
  mode.value = 'edit';
  device.value = row;
  activeTab.value = 'display';
  roomDeceasedName.value = '';
  configComposable.loadDeviceConfig(row.id);
  attrComposable.loadDeviceAttribute(row.id);
  loadRoomOccupancy(row);
  deviceDrawerApi.setState({
    title: '장비 수정',
    showConfirmButton: false,
    cancelText: '닫기',
  });
  deviceDrawerApi.open();
}

/**
 * 이 장비의 호실이 지금 쓰이고 있는지 확인한다 (D-DV6).
 *
 * 조문 중인 빈소의 화면을 바꾸면 조문객이 그 순간을 본다. 적용 전에 그 사실을
 * 알리기 위한 것이라, 실패해도 조용히 넘어간다 — 경고가 없다고 해서 편집을
 * 막을 이유는 없다.
 */
async function loadRoomOccupancy(row: BuildingApi.Device) {
  if (!row.roomId) return;
  try {
    const status = await getFuneralStatusDetail(row.roomId);
    if (status?.status === 'USING' && status.deceasedName) {
      roomDeceasedName.value = status.deceasedName;
    }
  } catch {
    // 조회 실패는 무시한다.
  }
}

/**
 * 즉시 명령 — DB 에 저장되지 않고 SignalR 로 곧장 간다.
 *
 * 서버는 장비가 실제로 붙어 있지 않으면 200 에 실패 메시지를 담아 돌려준다.
 * `requestClient` 가 그것을 예외로 올려 주므로 메시지를 그대로 보여 준다.
 */
async function sendScreenPower(state: 'ON' | 'OFF') {
  const code = device.value?.code;
  if (!code || isCommandSending.value) return;
  isCommandSending.value = true;
  try {
    await setDeviceScreenPower(code, state);
    message.success(`화면 ${state === 'ON' ? '켜기' : '끄기'} 명령을 보냈습니다.`);
  } catch (err: any) {
    message.error(
      err?.response?.data?.message || err?.message || '명령을 보내지 못했습니다. 장비가 오프라인일 수 있습니다.',
    );
  } finally {
    isCommandSending.value = false;
  }
}

function confirmAppRestart() {
  const current = device.value;
  if (!current) return;
  Modal.confirm({
    title: '앱 재시작 확인',
    content: roomDeceasedName.value
      ? `이 호실은 지금 조문 중입니다(고 ${roomDeceasedName.value}). 재기동까지 수 초간 화면이 꺼집니다. 계속하시겠습니까?`
      : `[${current.name}] 의 플레이어 앱을 재시작합니다. 재기동까지 수 초간 화면이 꺼집니다.`,
    okText: '재시작',
    cancelText: '취소',
    okButtonProps: { danger: true },
    onOk: async () => {
      try {
        await restartDeviceApp(current.code);
        message.success('앱 재시작 명령을 보냈습니다.');
      } catch (err: any) {
        message.error(
          err?.response?.data?.message || err?.message || '명령을 보내지 못했습니다. 장비가 오프라인일 수 있습니다.',
        );
      }
    },
  });
}

async function handleCreate() {
  try {
    await createDevice({
      ...formModel.value,
      code: formModel.value.code || '',
    });
    message.success('장비가 성공적으로 등록되었습니다.');
    deviceDrawerApi.close();
    emit('created');
  } catch {
    message.error('저장 실패');
  }
}

/** [장비 관리] 탭이 정보를 저장한 뒤 — 서랍 머리말과 목록의 그 행을 최신으로 맞춘다. */
async function onDeviceManaged() {
  const current = device.value;
  if (!current) return;
  const updated = await getDevice(current.id);
  // 지워진 장비를 다시 부르면 아무것도 안 온다. 그때는 열려 있던 것을 그대로 둔다.
  if (!updated) return;
  device.value = updated;
  emit('updated', updated);
}

/** 목록에서 지운 장비가 이 서랍에 열려 있으면 닫는다. */
function closeIfDevice(id: string) {
  if (device.value?.id === id) deviceDrawerApi.close();
}

/** SignalR 이 알려 온 온라인/오프라인을 머리말 뱃지에 반영한다. */
function applyStatus(code: string, status: BuildingApi.Device['status']) {
  const current = device.value;
  if (current && current.code === code) {
    device.value = { ...current, status };
  }
}

defineExpose({ openCreate, openEdit, closeIfDevice, applyStatus });
</script>

<template>
  <!--
    `@ok` 를 걸지 않는다. 등록은 위 `onConfirm` 하나가 맡는다 —
    둘 다 걸면 확인 한 번에 저장이 두 번 나갈 수 있다.

    `content-class="p-0"` — 수정 탭이 서랍 높이를 꽉 채워야 해서 기본 여백을 없앤다.
    여백은 각 탭이 스스로 준다.
  -->
  <DeviceDrawer :class="drawerWidthClass" content-class="p-0">
    <!-- 수정일 때는 제목 자리에 어떤 장비인지 적는다 -->
    <template v-if="mode === 'edit' && device" #title>
      <div class="flex min-w-0 items-center gap-2">
        <IconifyIcon
          :icon="getDeviceTypeInfo(device.deviceType).icon"
          class="size-5 shrink-0 text-primary"
        />
        <span class="truncate text-sm font-semibold">{{ device.name }}</span>
        <span class="rounded-full bg-primary/10 px-2 py-0.5 text-xs font-medium text-primary">
          {{ getDeviceTypeInfo(device.deviceType).label }}
        </span>
        <span class="truncate text-xs font-normal text-muted-foreground">
          {{ device.code }}
          <template v-if="device.roomShortName"> · {{ device.roomShortName }}</template>
        </span>
      </div>
    </template>

    <!--
      머리말 오른쪽 — 실시간 상태와 즉시 명령 (D-DV1).
      아래 탭의 값들과 달리 이 단추들은 아무것도 저장하지 않는다.
    -->
    <template v-if="mode === 'edit' && device" #extra>
      <div class="flex items-center gap-2">
        <span v-if="roomDeceasedName" class="flex items-center gap-1 text-xs text-amber-600 dark:text-amber-400">
          <IconifyIcon icon="lucide:alert-triangle" class="size-3.5" />
          조문 중
        </span>
        <span class="text-xs text-muted-foreground">
          <template v-if="device.ipAddress">{{ device.ipAddress }}</template>
          <template v-if="lastSeenLabel"> · {{ lastSeenLabel }}</template>
        </span>
        <Badge v-if="device.status === 'ONLINE'" status="success" text="온라인" />
        <Badge v-else-if="device.status === 'OFFLINE'" status="error" text="오프라인" />
        <Badge v-else status="default" text="미확인" />

        <span class="mx-1 h-4 w-px bg-border"></span>

        <Tooltip title="지금 바로 화면을 켠다 (저장되지 않음)">
          <Button size="small" :loading="isCommandSending" @click="sendScreenPower('ON')">
            <IconifyIcon icon="lucide:sun" class="size-3.5" />
          </Button>
        </Tooltip>
        <Tooltip title="지금 바로 화면을 끈다 (저장되지 않음)">
          <Button size="small" :loading="isCommandSending" @click="sendScreenPower('OFF')">
            <IconifyIcon icon="lucide:moon" class="size-3.5" />
          </Button>
        </Tooltip>
        <Tooltip title="플레이어 앱 재시작 (저장되지 않음)">
          <Button size="small" danger @click="confirmAppRestart">
            <IconifyIcon icon="lucide:rotate-ccw" class="size-3.5" />
          </Button>
        </Tooltip>
      </div>
    </template>

    <!-- ── 등록: 정보 폼 하나 ─────────────────────────────────── -->
    <div v-if="mode === 'create'" class="p-4">
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
        <Form.Item label="짧은 명칭">
          <Input v-model:value="formModel.shortName" placeholder="예: 로비 DID, 102호 등" />
        </Form.Item>
        <Form.Item label="장비코드">
          <Input
            v-model:value="formModel.code"
            placeholder="저장 시 자동으로 생성됩니다."
            :disabled="true"
          />
        </Form.Item>
        <Form.Item label="장비 유형">
          <DictSelect
            v-model:value="formModel.deviceType"
            dict-code="EQUIPMENT_TYPE"
            placeholder="장비 유형 선택"
            style="width: 100%"
          />
        </Form.Item>
        <Form.Item label="정렬 순서">
          <InputNumber
            v-model:value="formModel.sortOrder"
            :min="0"
            :precision="0"
            style="width: 100%"
          />
        </Form.Item>
        <!--
          IP/MAC 은 장비(플레이어)가 접속하면서 스스로 보고하는 값이므로 화면에서는 입력하지 않는다.
          수기로 넣어봐야 장비가 접속하는 순간 실제 값으로 덮어써지기 때문에,
          비활성 상태로 보여주기만 한다. (device-management-tab.vue 와 동일한 처리)
        -->
        <Form.Item label="IP 주소">
          <Input
            v-model:value="formModel.ipAddress"
            :disabled="true"
            placeholder="자동 감지 대기 중..."
          />
        </Form.Item>
        <Form.Item label="MAC 주소">
          <Input
            v-model:value="formModel.macAddress"
            :disabled="true"
            placeholder="자동 감지 대기 중..."
          />
        </Form.Item>
      </Form>
    </div>

    <!-- ── 수정: 자주 만지는 순서로 놓은 탭 셋 ─────────────────── -->
    <div v-else-if="device" class="flex h-full flex-col">
      <Tabs
        v-model:activeKey="activeTab"
        size="small"
        class="device-tabs flex min-h-0 flex-1 flex-col"
        :tab-bar-style="{ margin: 0, paddingLeft: '12px', paddingRight: '12px', flexShrink: 0 }"
      >
        <!-- 탭1: 화면 표시 — 매일 만진다 -->
        <Tabs.TabPane key="display">
          <template #tab>
            <span class="flex items-center gap-1.5">
              <IconifyIcon icon="lucide:monitor-play" class="size-3.5" />
              화면 표시
            </span>
          </template>
          <DeviceDisplayTab
            :attr="deviceAttr"
            :attr-loading="attrLoading"
            :attr-saving="attrSaving"
            :device-id="device.id"
            :device-type="device.deviceType"
            :deceased-name="roomDeceasedName"
            @apply="(draft) => handleAttrSave(device!.id, draft)"
          />
        </Tabs.TabPane>

        <!-- 탭2: 하드웨어 — 가끔 만진다 -->
        <Tabs.TabPane key="hardware">
          <template #tab>
            <span class="flex items-center gap-1.5">
              <IconifyIcon icon="lucide:sliders-horizontal" class="size-3.5" />
              하드웨어
            </span>
          </template>
          <DeviceConfigTab
            :device-config="deviceConfig"
            :config-loading="configLoading"
            :config-saving="configSaving"
            :power-on-time-val="powerOnTimeVal"
            :power-off-time-val="powerOffTimeVal"
            :reboot-time-val="rebootTimeVal"
            :device-id="device.id"
            @save="handleConfigSave(device!.id)"
            @reset="handleConfigReset(device!.id)"
            @update:powerOnTimeVal="(val) => (powerOnTimeVal = val)"
            @update:powerOffTimeVal="(val) => (powerOffTimeVal = val)"
            @update:rebootTimeVal="(val) => (rebootTimeVal = val)"
          />
        </Tabs.TabPane>

        <!-- 탭3: 장비 정보 — 설치할 때 한 번 -->
        <Tabs.TabPane key="identity">
          <template #tab>
            <span class="flex items-center gap-1.5">
              <IconifyIcon icon="lucide:tag" class="size-3.5" />
              장비 정보
            </span>
          </template>
          <DeviceManagementTab :device="device" @saved="onDeviceManaged" />
        </Tabs.TabPane>
      </Tabs>
    </div>
  </DeviceDrawer>
</template>

<style scoped>
:deep(.device-tabs .ant-tabs-content-holder) {
  display: flex;
  flex-direction: column;
  min-height: 0;
  flex: 1;
  overflow: hidden;
}
:deep(.device-tabs .ant-tabs-content) {
  flex: 1;
  min-height: 0;
  overflow: hidden;
}
:deep(.device-tabs .ant-tabs-tabpane) {
  height: 100%;
  display: flex;
  flex-direction: column;
}
</style>
