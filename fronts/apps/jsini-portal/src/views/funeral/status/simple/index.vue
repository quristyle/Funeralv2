<script lang="ts" setup>
/**
 * 빈소현황 — 심플. 옛 `page/monitor/room_status_simple.jsp`.
 *
 * 로비 전광판에 띄우는 화면이다. 옛 화면은 호실을 큰 칸으로 늘어놓고 사용 중이면
 * 고인 이름과 발인 시각을, 비었으면 '공실' 을 굵게 보여 줬다. 글자를 멀리서 읽어야 하므로
 * 다른 화면보다 크게 잡는다.
 *
 * 자동 갱신은 30초다 — 장비 상태까지 얹은 조회라 더 자주 부르면 서버가 아깝다.
 */
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { Page } from '@vben/common-ui';
import { Button, Select, Spin, Switch, message } from 'ant-design-vue';
import dayjs from 'dayjs';
import type { StatusApi } from '#/api/funeral/status';
import { getFuneralStatusBoard } from '#/api/funeral/status';
import { getBuildings } from '#/api/funeral/building';

const REFRESH_MS = 30_000;

const buildings = ref<any[]>([]);
const buildingId = ref<string | undefined>();
const onlyInUse = ref(false);

const loading = ref(false);
const rooms = ref<StatusApi.FuneralStatus[]>([]);
const summary = ref<StatusApi.Summary | null>(null);
const lastLoadedAt = ref<string>('');

let timer: ReturnType<typeof setInterval> | undefined;

/** 건물이 여럿이면 건물별로 묶어 그린다. 하나뿐이면 묶지 않는다. */
const grouped = computed(() => {
  const map = new Map<string, StatusApi.FuneralStatus[]>();
  for (const r of rooms.value) {
    const key = r.buildingName || '건물 미지정';
    const list = map.get(key) ?? [];
    list.push(r);
    map.set(key, list);
  }
  return [...map.entries()].map(([name, items]) => ({ name, items }));
});

async function load(silent = false) {
  if (!silent) loading.value = true;
  try {
    const board = await getFuneralStatusBoard({
      buildingId: buildingId.value || undefined,
      onlyInUse: onlyInUse.value,
    });
    rooms.value = board?.rooms ?? [];
    summary.value = board?.summary ?? null;
    lastLoadedAt.value = dayjs().format('HH:mm:ss');
  } catch {
    if (!silent) message.error('현황을 불러오지 못했습니다.');
  } finally {
    loading.value = false;
  }
}

function fmtTime(value?: string) {
  return value ? dayjs(value).format('MM-DD HH:mm') : '미정';
}

onMounted(async () => {
  try {
    buildings.value = (await getBuildings()) || [];
  } catch {
    buildings.value = [];
  }
  await load();
  timer = setInterval(() => load(true), REFRESH_MS);
});

onUnmounted(() => {
  if (timer) clearInterval(timer);
});
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full flex-col gap-3">
      <div class="flex flex-wrap items-center justify-between gap-3 border-b pb-2">
        <div class="flex items-baseline gap-3">
          <h1 class="text-xl font-bold">빈소 현황</h1>
          <span v-if="summary" class="text-sm text-muted-foreground">
            사용 <b class="text-red-600">{{ summary.usingRooms }}</b> ·
            공실 <b class="text-emerald-600">{{ summary.emptyRooms }}</b> /
            전체 {{ summary.totalRooms }}
          </span>
        </div>

        <div class="flex flex-wrap items-center gap-2">
          <Select
            v-model:value="buildingId"
            class="w-36"
            allow-clear
            placeholder="건물 전체"
            :options="buildings.map((b) => ({ label: b.name, value: b.id }))"
            @change="load()"
          />
          <div class="flex items-center gap-1 text-xs text-muted-foreground">
            <Switch v-model:checked="onlyInUse" size="small" @change="load()" />
            <span>사용 중만</span>
          </div>
          <Button size="small" @click="load()">새로고침</Button>
          <span class="text-[11px] text-muted-foreground">
            {{ lastLoadedAt ? `${lastLoadedAt} 기준 · 30초마다 갱신` : '' }}
          </span>
        </div>
      </div>

      <Spin :spinning="loading" class="flex-1 overflow-y-auto">
        <div v-for="g in grouped" :key="g.name" class="mb-5">
          <div v-if="grouped.length > 1" class="mb-2 text-sm font-semibold text-muted-foreground">
            {{ g.name }}
          </div>

          <div class="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
            <div
              v-for="room in g.items"
              :key="room.roomId"
              class="rounded-lg border p-4 text-center transition-colors"
              :class="
                room.status === 'USING'
                  ? 'border-red-300 bg-red-50 dark:border-red-900 dark:bg-red-950/40'
                  : 'border-emerald-300 bg-emerald-50 dark:border-emerald-900 dark:bg-emerald-950/30'
              "
            >
              <div class="truncate text-xs font-semibold text-muted-foreground">
                {{ room.roomShortName || room.roomName }}
              </div>

              <div class="my-3 min-h-[2.5rem]">
                <div
                  v-if="room.status === 'USING'"
                  class="truncate text-2xl font-extrabold text-red-700 dark:text-red-400"
                >
                  {{ room.deceasedName }}
                </div>
                <div v-else class="text-lg font-bold text-emerald-700 dark:text-emerald-400">
                  공실
                </div>
              </div>

              <div class="space-y-0.5 text-[11px] text-muted-foreground">
                <template v-if="room.status === 'USING'">
                  <div class="truncate">상주 {{ room.chiefMourner || '-' }}</div>
                  <div>발인 {{ fmtTime(room.dischargeTime) }}</div>
                  <div>{{ room.useDays }}일째</div>
                </template>
                <div v-else>대기 중</div>
              </div>

              <div
                v-if="room.deviceCount > 0"
                class="mt-2 border-t pt-1 text-[10px]"
                :class="room.onlineDeviceCount === room.deviceCount ? 'text-emerald-600' : 'text-amber-600'"
              >
                장비 {{ room.onlineDeviceCount }}/{{ room.deviceCount }}
              </div>
            </div>
          </div>
        </div>

        <p v-if="!loading && rooms.length === 0" class="py-10 text-center text-sm text-muted-foreground">
          보여 줄 빈소가 없습니다.
        </p>
      </Spin>
    </div>
  </Page>
</template>
