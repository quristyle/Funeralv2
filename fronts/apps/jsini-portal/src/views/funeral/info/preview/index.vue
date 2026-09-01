<script lang="ts" setup>
/**
 * 미리보기 — 옛 `client_machine/{번호}/index.jsp` 를 열던 메뉴(JQLME · JQLSME ·
 * JQLBME · JQLXME) 자리다.
 *
 * 옛 시스템은 장비 종류마다 메뉴를 따로 두고 새 창으로 띄웠다. 지금은 장비가 코드 하나로
 * 열리므로 메뉴를 넷으로 나눌 이유가 없다 — 장비 목록에서 골라 새 창으로 연다.
 */
import { onMounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { Button, Empty, Input, Select, Spin, Tag, message } from 'ant-design-vue';
import dayjs from 'dayjs';
import type { InfoApi } from '#/api/funeral/info';
import { getDevicePreviews } from '#/api/funeral/info';
import { getBuildings, getRooms } from '#/api/funeral/building';

const buildings = ref<any[]>([]);
const rooms = ref<any[]>([]);
const buildingId = ref<string | undefined>();
const roomId = ref<string | undefined>();
const keyword = ref('');

const loading = ref(false);
const devices = ref<InfoApi.DevicePreview[]>([]);

async function load() {
  loading.value = true;
  try {
    const list = await getDevicePreviews({
      buildingId: buildingId.value || undefined,
      roomId: roomId.value || undefined,
    });
    const k = keyword.value.trim().toLowerCase();
    devices.value = k
      ? (list ?? []).filter(
          (d) =>
            d.name.toLowerCase().includes(k) ||
            (d.deviceCode ?? '').toLowerCase().includes(k) ||
            (d.roomName ?? '').toLowerCase().includes(k),
        )
      : (list ?? []);
  } catch {
    message.error('장비 목록을 불러오지 못했습니다.');
  } finally {
    loading.value = false;
  }
}

async function fetchRooms() {
  try {
    rooms.value = (await getRooms({ buildingId: buildingId.value })) || [];
  } catch {
    rooms.value = [];
  }
}

async function handleBuildingChange() {
  roomId.value = undefined;
  await fetchRooms();
  await load();
}

/**
 * 새 창으로 연다. 옛 메뉴의 `view_type:new` 와 같은 동작이다.
 * 미리보기는 장비가 실제로 그리는 화면이라 앱 안에 끼워 넣으면 크기가 어긋난다.
 */
function openPreview(device: InfoApi.DevicePreview) {
  if (!device.previewUrl) {
    message.warning('이 장비에는 인증 코드가 없어 미리볼 수 없습니다.');
    return;
  }
  window.open(device.previewUrl, '_blank', 'noopener');
}

onMounted(async () => {
  try {
    buildings.value = (await getBuildings()) || [];
  } catch {
    buildings.value = [];
  }
  await fetchRooms();
  await load();
});
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full flex-col gap-3">
      <div class="flex flex-wrap items-center justify-between gap-2 border-b pb-2">
        <div>
          <h1 class="text-lg font-bold">장비 화면 미리보기</h1>
          <p class="text-xs text-muted-foreground">
            장비가 실제로 그리는 화면을 새 창으로 연다.
          </p>
        </div>

        <div class="flex flex-wrap items-center gap-2">
          <Input
            v-model:value="keyword"
            class="w-40"
            allow-clear
            placeholder="장비명 · 코드 · 호실"
            @press-enter="load"
            @change="load"
          />
          <Select
            v-model:value="buildingId"
            class="w-32"
            allow-clear
            placeholder="건물"
            :options="buildings.map((b) => ({ label: b.name, value: b.id }))"
            @change="handleBuildingChange"
          />
          <Select
            v-model:value="roomId"
            class="w-32"
            allow-clear
            placeholder="호실"
            :options="rooms.map((r) => ({ label: r.name, value: r.id }))"
            @change="load"
          />
          <Button size="small" @click="load">새로고침</Button>
        </div>
      </div>

      <Spin :spinning="loading" class="flex-1 overflow-y-auto">
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
          <div
            v-for="device in devices"
            :key="device.id"
            class="flex flex-col gap-2 rounded-lg border p-3 transition-colors hover:border-primary"
          >
            <div class="flex items-start justify-between gap-2">
              <div class="min-w-0">
                <div class="truncate text-sm font-semibold">{{ device.name }}</div>
                <div class="truncate font-mono text-[11px] text-muted-foreground">
                  {{ device.deviceCode || '코드 없음' }}
                </div>
              </div>
              <Tag :color="device.isOnline ? 'success' : 'default'">
                {{ device.isOnline ? '온라인' : '오프라인' }}
              </Tag>
            </div>

            <div class="space-y-0.5 text-[11px] text-muted-foreground">
              <div class="truncate">{{ device.buildingName || '-' }} · {{ device.roomName || '호실 미배정' }}</div>
              <div>{{ device.deviceType || '종류 미지정' }}</div>
              <div>
                최근 접속
                {{ device.lastConnectedAt ? dayjs(device.lastConnectedAt).format('MM-DD HH:mm') : '-' }}
              </div>
            </div>

            <Button block :disabled="!device.previewUrl" @click="openPreview(device)">
              <IconifyIcon icon="lucide:external-link" class="mr-1 size-4" />
              미리보기
            </Button>
          </div>
        </div>

        <Empty v-if="!loading && devices.length === 0" description="미리볼 장비가 없습니다." class="py-10" />
      </Spin>
    </div>
  </Page>
</template>
