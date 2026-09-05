<script lang="ts" setup>
/**
 * 장비 한 대의 줄 — 이름·유형·상태·전원·미디어.
 *
 * 호실 카드(`RoomColumn`)와 공용 장비 카드(`DeviceColumn`)가 함께 쓴다.
 * 예전에는 같은 마크업이 호실 카드와 건물 공용 장비 패널에 따로 있었고,
 * 그래서 공용 장비 쪽에는 전원 메뉴가 빠지는 식으로 조금씩 어긋났다.
 */
import { computed } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { Badge, Dropdown, Menu, Modal, Tag, Tooltip, message } from 'ant-design-vue';

import { restartDeviceApp, setDeviceScreenPower } from '#/api/funeral/building';
import { useJsiniUser } from '#/composables/use-jsini-user';

defineProps<{
  device: any;
  videos?: any[];
  musics?: any[];
  /** 여러 대가 늘어설 때는 칸으로 감싼다. */
  boxed?: boolean;
}>();

const emit = defineEmits<{
  (e: 'update-media', payload: { deviceId: string; type: 'music' | 'video'; mediaId: string }): void;
  (e: 'show-detail', deviceId: string): void;
}>();

// 장비 제어(전원·재시작)는 관리자 역할만 (47번 문서 D-RS4). 서버도 같은 검사를 한다.
const { roles } = useJsiniUser();
const canControlDevices = computed(() =>
  roles.value.some((r) =>
    ['ADMINISTRATOR', 'PARTNER_ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR'].includes(r),
  ),
);

const deviceTypeMap: Record<string, { color: string; label: string }> = {
  FUNERAL_PORTRAIT: { color: 'purple', label: '영정' },
  MULTIMEDIA: { color: 'orange', label: '미디어' },
  ROOM_GUIDE: { color: 'blue', label: '호실안내' },
  ENTRANCE_GUIDE: { color: 'green', label: '입구안내' },
  KIOSK: { color: 'cyan', label: '키오스크' },
};

async function handleScreenPower(device: any, state: 'OFF' | 'ON') {
  try {
    await setDeviceScreenPower(device.code, state);
    message.success(`[${device.name}] 화면 ${state === 'ON' ? '켜기' : '끄기'} 명령을 전송했습니다.`);
  } catch (err: any) {
    message.error(
      err?.response?.data?.message || err?.message || '명령 전송에 실패했습니다. 장비가 오프라인일 수 있습니다.',
    );
  }
}

function handleAppRestart(device: any) {
  Modal.confirm({
    title: '앱 재시작 확인',
    content: `[${device.name}] 장비의 플레이어 앱을 재시작하시겠습니까? 재기동까지 수 초간 화면이 꺼집니다.`,
    okText: '재시작',
    cancelText: '취소',
    okButtonProps: { danger: true },
    onOk: async () => {
      try {
        await restartDeviceApp(device.code);
        message.success(`[${device.name}] 앱 재시작 명령을 전송했습니다.`);
      } catch (err: any) {
        message.error(
          err?.response?.data?.message || err?.message || '명령 전송에 실패했습니다. 장비가 오프라인일 수 있습니다.',
        );
      }
    },
  });
}
</script>

<template>
  <div :class="boxed ? 'space-y-1 rounded border border-border/60 bg-muted/30 p-1' : 'space-y-1'">
    <div class="flex items-center gap-1">
      <Badge :status="device.status === 'ONLINE' ? 'success' : 'error'" class="scale-75" />
      <span class="min-w-0 flex-1 truncate text-sm font-medium text-foreground">
        {{ device.name }}
      </span>
      <Tag
        :color="deviceTypeMap[device.deviceType]?.color || 'default'"
        class="m-0 px-1 py-0 text-sm font-semibold"
      >
        {{ deviceTypeMap[device.deviceType]?.label || device.deviceType }}
      </Tag>
      <Tooltip :title="device.code">
        <IconifyIcon
          icon="lucide:info"
          class="size-4 shrink-0 cursor-pointer text-muted-foreground/60 transition-colors hover:text-primary"
          @click="emit('show-detail', device.id)"
        />
      </Tooltip>
      <Dropdown v-if="canControlDevices" :trigger="['click']">
        <IconifyIcon
          icon="lucide:power"
          class="size-4 shrink-0 cursor-pointer text-muted-foreground/60 transition-colors hover:text-primary"
        />
        <template #overlay>
          <Menu>
            <Menu.Item key="on" @click="handleScreenPower(device, 'ON')">화면 켜기</Menu.Item>
            <Menu.Item key="off" @click="handleScreenPower(device, 'OFF')">화면 끄기</Menu.Item>
            <Menu.Divider />
            <Menu.Item key="restart" danger @click="handleAppRestart(device)">앱 재시작</Menu.Item>
          </Menu>
        </template>
      </Dropdown>
    </div>

    <div class="flex gap-1 text-sm text-muted-foreground">
      <Dropdown :trigger="['click']">
        <span class="flex min-w-0 flex-1 cursor-pointer items-center gap-0.5 truncate rounded border border-border/80 bg-card px-1 py-0.5 hover:text-primary">
          <IconifyIcon icon="lucide:clapperboard" class="size-3 shrink-0" />
          <span class="truncate">{{ device.videoName || '영상 없음' }}</span>
        </span>
        <template #overlay>
          <Menu @click="({ key }) => emit('update-media', { deviceId: device.id, type: 'video', mediaId: key as string })">
            <Menu.Item key="">미사용 (해제)</Menu.Item>
            <Menu.Divider />
            <Menu.Item v-for="v in videos" :key="v.value">{{ v.label }}</Menu.Item>
          </Menu>
        </template>
      </Dropdown>

      <Dropdown :trigger="['click']">
        <span class="flex min-w-0 flex-1 cursor-pointer items-center gap-0.5 truncate rounded border border-border/80 bg-card px-1 py-0.5 hover:text-primary">
          <IconifyIcon icon="lucide:music" class="size-3 shrink-0" />
          <span class="truncate">{{ device.musicName || '음원 없음' }}</span>
        </span>
        <template #overlay>
          <Menu @click="({ key }) => emit('update-media', { deviceId: device.id, type: 'music', mediaId: key as string })">
            <Menu.Item key="">미사용 (해제)</Menu.Item>
            <Menu.Divider />
            <Menu.Item v-for="m in musics" :key="m.value">{{ m.label }}</Menu.Item>
          </Menu>
        </template>
      </Dropdown>
    </div>
  </div>
</template>
