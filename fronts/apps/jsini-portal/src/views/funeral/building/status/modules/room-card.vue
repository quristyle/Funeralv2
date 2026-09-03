<script lang="ts" setup>
import { computed } from 'vue';
import { Tag, Badge, Dropdown, Menu, Tooltip, Button, Modal, message } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import { useRouter } from 'vue-router';
import dayjs from 'dayjs';
import { departDeceased, restartDeviceApp, setDeviceScreenPower } from '#/api/funeral/building';
import { useJsiniUser } from '#/composables/use-jsini-user';

const router = useRouter();

// 장비 제어(전원·재시작)는 관리자 역할만 (47번 문서 D-RS4). 서버도 같은 검사를 한다.
const { roles } = useJsiniUser();
const canControlDevices = computed(() =>
  roles.value.some((r) =>
    ['ADMINISTRATOR', 'SYSTEM_ADMINISTRATOR', 'PARTNER_ADMINISTRATOR'].includes(r),
  ),
);

/** 원격 화면 전원 — 즉시 실행 명령. 장비가 온라인일 때만 전달된다. */
async function handleScreenPower(device: any, state: 'OFF' | 'ON') {
  try {
    await setDeviceScreenPower(device.code, state);
    message.success(`[${device.name}] 화면 ${state === 'ON' ? '켜기' : '끄기'} 명령을 전송했습니다.`);
  } catch (err: any) {
    message.error(err?.response?.data?.message || err?.message || '명령 전송에 실패했습니다. 장비가 오프라인일 수 있습니다.');
  }
}

/** 플레이어 앱 재시작 — 확인 후 전송. 리눅스 장비는 systemd 가 3초 안에 되살린다. */
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
        message.error(err?.response?.data?.message || err?.message || '명령 전송에 실패했습니다. 장비가 오프라인일 수 있습니다.');
      }
    },
  });
}

const props = defineProps<{
  room: {
    id: string;
    name: string;
    shortName?: string;
    buildingId?: string;
    lastVacatedAt?: string;
    lastDepartedDeceasedId?: string;
    lastDepartedDeceasedName?: string;
    deceased?: any;
    devices: any[];
  };
  videos?: any[];
  musics?: any[];
}>();

const emit = defineEmits<{
  (e: 'update-media', payload: { deviceId: string; type: 'video' | 'music'; mediaId: string }): void;
  (e: 'show-detail', deviceId: string): void;
  (e: 'refresh'): void;
  // ── 카드 관리 메뉴 (옛 화면의 액션들 — 47번 문서 2단계) ──
  (e: 'edit-deceased', deceasedId: string): void;
  (e: 'create-deceased', roomId: string): void;
  (e: 'move-room', payload: { deceasedId: string; deceasedName: string; roomId: string; buildingId?: string }): void;
  (e: 'cancel-departure', payload: { deceasedId: string; deceasedName: string }): void;
  (e: 'bulk-media', payload: { roomId: string; type: 'video' | 'music'; mediaId: string }): void;
}>();

async function handleDepart() {
  if (!props.room.deceased) return;
  const deceased = props.room.deceased;

  Modal.confirm({
    title: '출상 처리 확인',
    content: `고인 [고 ${deceased.name}] 님의 출상(장례 종료 및 배정 해제) 처리를 진행하시겠습니까?`,
    okText: '진행',
    cancelText: '취소',
    okButtonProps: { danger: true },
    onOk: async () => {
      try {
        // 상태 전환과 배정 해제만 하는 전용 API 다 — 전체 PUT 재구성은
        // 목록에 없는 칸(비고 등)을 지우는 문제가 있었다 (47번 문서 0단계).
        await departDeceased(deceased.id);
        message.success('출상 처리가 정상적으로 완료되었습니다.');
        emit('refresh');
      } catch (err) {
        console.error('출상 처리 실패:', err);
        message.error('출상 처리 중 오류가 발생했습니다.');
      }
    }
  });
}

/** 고인 장례 상태 태그 — DeceasedStatus 정본 셋 (47번 문서 D-RS1) */
const deceasedStatusMap: Record<string, { label: string; color: string }> = {
  FUNERAL_IN_PROGRESS: { label: '장례 진행중', color: 'processing' },
  FUNERAL_DEPARTURE_COMPLETED: { label: '출상 완료', color: 'warning' },
  COMPLETED: { label: '장례 종료', color: 'default' },
};

function fmtDateTime(value?: string) {
  return value ? dayjs(value).format('MM-DD HH:mm') : '';
}

const deviceTypeMap: Record<string, { label: string; color: string }> = {
  FUNERAL_PORTRAIT: { label: '영정', color: 'purple' },
  MULTIMEDIA: { label: '미디어', color: 'orange' },
  ROOM_GUIDE: { label: '호실안내', color: 'blue' },
  ENTRANCE_GUIDE: { label: '입구안내', color: 'green' },
  KIOSK: { label: '키오스크', color: 'cyan' },
};
</script>

<template>
  <div 
    class="bg-card rounded-xl border border-border/80 p-5 shadow-sm hover:shadow-md hover:border-primary/50 transition-all duration-300 flex flex-col justify-between"
  >
    <div>
      <!-- 호실 카드 헤더 — ⋮ 메뉴가 모든 관리 액션의 단일 진입점이다 (47번 문서 4.3) -->
      <div class="flex items-center justify-between mb-4">
        <Tag color="blue" class="m-0 select-none text-xs font-semibold">
          {{ room.shortName??room.name }}
        </Tag>
        <Dropdown :trigger="['click']" placement="bottomRight">
          <Button type="text" size="small" class="h-6 w-6 p-0 flex items-center justify-center">
            <IconifyIcon icon="lucide:more-vertical" class="size-4 text-muted-foreground" />
          </Button>
          <template #overlay>
            <Menu>
              <template v-if="room.deceased">
                <Menu.Item key="edit" @click="emit('edit-deceased', room.deceased.id)">
                  고인 정보 관리 (사진 · 상주)
                </Menu.Item>
                <Menu.Item
                  key="move"
                  @click="emit('move-room', {
                    deceasedId: room.deceased.id,
                    deceasedName: room.deceased.name,
                    roomId: room.id,
                    buildingId: room.buildingId,
                  })"
                >
                  호실 변경
                </Menu.Item>
                <Menu.SubMenu v-if="room.devices.length > 0" key="bulkVideo" title="호실 영상 일괄 변경">
                  <Menu.Item key="bulk-video-clear" @click="emit('bulk-media', { roomId: room.id, type: 'video', mediaId: '' })">
                    🚫 미사용 (해제)
                  </Menu.Item>
                  <Menu.Divider />
                  <Menu.Item
                    v-for="v in videos"
                    :key="`bulk-video-${v.value}`"
                    @click="emit('bulk-media', { roomId: room.id, type: 'video', mediaId: v.value })"
                  >
                    {{ v.label }}
                  </Menu.Item>
                </Menu.SubMenu>
                <Menu.SubMenu v-if="room.devices.length > 0" key="bulkMusic" title="호실 음원 일괄 변경">
                  <Menu.Item key="bulk-music-clear" @click="emit('bulk-media', { roomId: room.id, type: 'music', mediaId: '' })">
                    🚫 미사용 (해제)
                  </Menu.Item>
                  <Menu.Divider />
                  <Menu.Item
                    v-for="m in musics"
                    :key="`bulk-music-${m.value}`"
                    @click="emit('bulk-media', { roomId: room.id, type: 'music', mediaId: m.value })"
                  >
                    {{ m.label }}
                  </Menu.Item>
                </Menu.SubMenu>
                <Menu.Divider />
                <Menu.Item key="depart" danger @click="handleDepart">출상 처리</Menu.Item>
              </template>
              <template v-else>
                <Menu.Item key="create" @click="emit('create-deceased', room.id)">
                  고인 등록 (이 호실로)
                </Menu.Item>
                <Menu.Item
                  v-if="room.lastDepartedDeceasedId"
                  key="cancelDepart"
                  @click="emit('cancel-departure', {
                    deceasedId: room.lastDepartedDeceasedId,
                    deceasedName: room.lastDepartedDeceasedName ?? '',
                  })"
                >
                  출상 취소 (故 {{ room.lastDepartedDeceasedName }})
                </Menu.Item>
              </template>
              <Menu.Divider />
              <Menu.Item key="goDeceased" @click="router.push('/building/deceased')">고인 관리 화면으로</Menu.Item>
              <Menu.Item key="goRoom" @click="router.push('/building/room')">호실 관리 화면으로</Menu.Item>
            </Menu>
          </template>
        </Dropdown>
      </div>

      <div class="flex flex-wrap gap-2">
        <!-- 고인 정보 표출부 (입실중) -->
        <div v-if="room.deceased" class="flex items-center justify-between p-3 bg-primary/5 rounded-lg border border-primary/10 gap-3">
          <div class="flex gap-3 min-w-0">
            <!-- 영정 썸네일 — 보정본 우선 선택은 서버(StatusService)가 한다 -->
            <div class="w-[60px] h-[75px] bg-muted rounded border border-border flex items-center justify-center overflow-hidden shrink-0">
              <img
                v-if="room.deceased.photoFileId || room.deceased.photoUrl"
                :src="room.deceased.photoFileId
                  ? `/api/file/thumbnail/${room.deceased.photoFileId}`
                  : room.deceased.photoUrl"
                class="w-full h-full object-cover"
                alt="영정"
              />
              <IconifyIcon v-else icon="mdi:account" class="size-8 text-muted-foreground/40" />
            </div>

            <!-- 고인 인적 사항 -->
            <div class="flex flex-col justify-between py-0.5 min-w-0">
              <div>
                <div class="font-bold text-base text-foreground truncate">{{ room.deceased.name }}</div>
                <div class="text-xs text-muted-foreground mt-0.5">
                  {{ room.deceased.gender === 'M' || room.deceased.gender === 'MALE' ? '남성' : '여성' }} / {{ room.deceased.age }}세
                </div>
              </div>
              <!-- 입관 · 발인 · 장지 — 옛 화면의 관리 항목 보강 (47번 문서 3.1) -->
              <div class="text-[10px] text-muted-foreground leading-4 mt-0.5">
                <div v-if="room.deceased.coffinTime">입관 {{ fmtDateTime(room.deceased.coffinTime) }}</div>
                <div v-if="room.deceased.dischargeTime">발인 {{ fmtDateTime(room.deceased.dischargeTime) }}</div>
                <div v-if="room.deceased.burialPlace" class="truncate max-w-[130px]">장지 {{ room.deceased.burialPlace }}</div>
              </div>

              <!-- 상태 태그와 출상 버튼을 분리했다 — 예전에는 상태 코드 어긋남 탓에
                   else 가지의 빨간 태그가 '우연히' 출상 진입점 노릇을 했다 (47번 문서 0단계). -->
              <div class="flex items-center gap-1">
                <Tag
                  :color="deceasedStatusMap[room.deceased.status]?.color ?? 'processing'"
                  class="m-0 text-[10px] py-0 px-1.5"
                >
                  {{ deceasedStatusMap[room.deceased.status]?.label ?? '장례 진행중' }}
                </Tag>
                <Button danger size="small" class="h-[18px] text-[10px] px-1.5 leading-none" @click="handleDepart">
                  출상
                </Button>
              </div>



            </div>
          </div>

        </div>

        <!-- 공실 상태 표출부 — 마지막 퇴실은 옛 화면의 '퇴실 {일시}' 표기다 -->
        <div v-else class="flex flex-col items-center justify-center py-4 mb-4 px-3 bg-muted/20 border border-dashed border-border rounded-lg text-muted-foreground text-xs gap-1.5">
          <IconifyIcon icon="mdi:door-closed" class="size-6 text-muted-foreground/35" />
          <span>배정 고인 없음 (공실)</span>
          <span v-if="room.lastVacatedAt" class="text-[10px] text-muted-foreground/70">
            퇴실 {{ fmtDateTime(room.lastVacatedAt) }}
          </span>
          <Button size="small" type="dashed" class="text-xs" @click="emit('create-deceased', room.id)">
            고인 등록
          </Button>
        </div>


        <div v-if="room.devices.length > 0" class="flex flex-wrap gap-2">
          <div 
            v-for="device in room.devices" 
            :key="device.id"
            class="flex flex-col gap-1.5 bg-muted/30 p-2 rounded-lg border border-border/60 text-xs"
          >
            <!-- 장비명 & 타입 뱃지 & 온라인 상태 -->
            <div class="flex items-center flex-col">
              <div class="flex items-center gap-1 min-w-0">
                <span class="font-bold text-foreground truncate max-w-[120px]">{{ device.name }}</span>
                <Tooltip :title="device.code">
                  <IconifyIcon 
                    icon="lucide:info" 
                    class="size-3.5 text-muted-foreground/60 hover:text-primary cursor-pointer shrink-0 transition-colors"
                    @click="emit('show-detail', device.id)"
                  />
                </Tooltip>
              </div>
              <div class="flex items-center gap-1">
                <Tag
                  :color="deviceTypeMap[device.deviceType]?.color || 'default'"
                  class="m-0 text-[9px] py-0 px-1 font-semibold select-none scale-90"
                >
                  {{ deviceTypeMap[device.deviceType]?.label || device.deviceType }}
                </Tag>
                <Badge :status="device.status === 'ONLINE' ? 'success' : 'error'" class="scale-75" />
                <!-- 장비 제어 — 관리자 역할만 노출 (D-RS4) -->
                <Dropdown v-if="canControlDevices" :trigger="['click']">
                  <IconifyIcon
                    icon="lucide:power"
                    class="size-3.5 text-muted-foreground/60 hover:text-primary cursor-pointer transition-colors"
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
            </div>
            
            <!-- 미디어 드롭다운 선택 영역 -->
            <div class="flex flex-col gap-1.5 text-[9px] text-muted-foreground select-none border-t border-border/40 pt-1.5">
              <Dropdown :trigger="['click']">
                <span class="hover:text-primary cursor-pointer border border-border/80 bg-card px-1.5 py-0.5 rounded flex items-center gap-1 max-w-[100px] truncate">
                  🎬 {{ device.videoName || '영상 없음' }}
                  <IconifyIcon icon="lucide:chevron-down" class="size-2.5" />
                </span>
                <template #overlay>
                  <Menu @click="({ key }) => emit('update-media', { deviceId: device.id, type: 'video', mediaId: key as string })">
                    <Menu.Item key="">🚫 미사용 (해제)</Menu.Item>
                    <Menu.Divider />
                    <Menu.Item v-for="v in videos" :key="v.value">
                      {{ v.label }}
                    </Menu.Item>
                  </Menu>
                </template>
              </Dropdown>
              
              <Dropdown :trigger="['click']">
                <span class="hover:text-primary cursor-pointer border border-border/80 bg-card px-1.5 py-0.5 rounded flex items-center gap-1 max-w-[100px] truncate">
                  🎵 {{ device.musicName || '음원 없음' }}
                  <IconifyIcon icon="lucide:chevron-down" class="size-2.5" />
                </span>
                <template #overlay>
                  <Menu @click="({ key }) => emit('update-media', { deviceId: device.id, type: 'music', mediaId: key as string })">
                    <Menu.Item key="">🚫 미사용 (해제)</Menu.Item>
                    <Menu.Divider />
                    <Menu.Item v-for="m in musics" :key="m.value">
                      {{ m.label }}
                    </Menu.Item>
                  </Menu>
                </template>
              </Dropdown>
            </div>
          </div>
        </div>

      </div>



    </div>






  </div>
</template>
