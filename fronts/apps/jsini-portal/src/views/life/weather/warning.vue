<script lang="ts" setup>
import type { LifeWeatherApi } from '#/api/life/weather';

import { computed, nextTick, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import { Button, Empty, Select, Space, Spin, Tag } from 'ant-design-vue';

import { getWarnings, getWarningZones } from '#/api/life/weather';

import WeatherWarningRegionMatch from './modules/weather-warning-region-match.vue';
import { elapsedFromNow, formatTmFc, parseTmFc } from './modules/weather-shared';

/**
 * [기상 특보 현황] — 원본 WeatherWarning.vue 이식.
 *
 * 최근 7일 특보(getWarnings(all))에서 48시간 이내 것만 추려
 * 특보번호별로 묶은 타임라인(왼쪽)과, 선택한 발표분의 통보문 상세
 * (오른쪽, WeatherWarningRegionMatch)를 보여 준다.
 * 관리지역이 걸린 문장은 목록 카드 안에서도 요약(Target Area)으로 보인다.
 */

interface WarningGroup {
  warningNum?: null | string;
  latest: LifeWeatherApi.Warning;
  items: LifeWeatherApi.Warning[];
}

const warnings = ref<LifeWeatherApi.Warning[]>([]);
const warningZones = ref<any[]>([]);
const loading = ref(false);
const selectedWarningId = ref<null | number>(null);
const searchCommand = ref<string | undefined>(undefined);

async function fetchWarningZones() {
  try {
    warningZones.value = (await getWarningZones()) ?? [];
  } catch {
    warningZones.value = [];
  }
}

const zoneMap = computed(() => {
  const map: Record<string, any> = {};
  warningZones.value.forEach((z) => {
    map[z.regId] = z;
  });
  return map;
});

/** 최근 48시간 특보 조회 후 최신순 정렬, 첫 그룹 자동 선택 */
async function fetchWarnings() {
  loading.value = true;
  selectedWarningId.value = null;
  try {
    const all = (await getWarnings(true)) ?? [];
    const cutoff = Date.now() - 48 * 60 * 60 * 1000;
    warnings.value = all
      .filter((w) => (parseTmFc(w.tmFc)?.valueOf() ?? 0) >= cutoff)
      .sort((a, b) => b.tmFc.localeCompare(a.tmFc));

    await nextTick();
    const first = groupedWarnings.value[0]?.latest?.id ?? null;
    if (first) selectedWarningId.value = first;
  } catch {
    warnings.value = [];
  } finally {
    loading.value = false;
  }
}

/** 명령(발표·해제…) 필터 선택지 */
const commandOptions = computed(() => {
  const cmds = new Set<string>();
  warnings.value.forEach((w) => {
    if (w.command) cmds.add(w.command);
  });
  return [...cmds].sort().map((c) => ({ label: c, value: c }));
});

const filteredWarnings = computed(() =>
  searchCommand.value
    ? warnings.value.filter((w) => w.command === searchCommand.value)
    : warnings.value,
);

/** 특보번호별 그룹 (그룹 안·그룹 간 모두 최신순) */
const groupedWarnings = computed<WarningGroup[]>(() => {
  const groups: Record<string, LifeWeatherApi.Warning[]> = {};
  filteredWarnings.value.forEach((w) => {
    const key = w.warningNum || `temp-${w.id}`;
    (groups[key] ??= []).push(w);
  });

  return Object.values(groups)
    .filter((items) => items.length > 0)
    .map((items) => {
      const sorted = [...items].sort((a, b) => b.tmFc.localeCompare(a.tmFc));
      return {
        items: sorted,
        latest: sorted[0]!,
        warningNum: sorted[0]!.warningNum,
      };
    })
    .sort((a, b) => b.latest.tmFc.localeCompare(a.latest.tmFc));
});

/** 그룹 최신 발표분에서 관리지역별 관련 문장 제목 요약 */
function getLocationSentences(warning: LifeWeatherApi.Warning | undefined) {
  if (
    !warning?.matchedLocations?.length ||
    !warning.sentences ||
    warning.sentences.length === 0
  ) {
    return [];
  }

  const result: { location: LifeWeatherApi.Location; sentences: any[] }[] = [];

  warning.matchedLocations.forEach((loc) => {
    const code = loc.warningAreaCode;
    if (!code || !zoneMap.value[code]) return;

    const zone = zoneMap.value[code];
    const keywords = new Set<string>();
    if (zone.regKo) keywords.add(zone.regKo);
    if (zone.regName) {
      zone.regName.split(/\s+/).forEach((w: string) => {
        if (w.trim()) keywords.add(w.trim());
      });
    }

    const locSentences = warning
      .sentences!.filter((s: any) => {
        const text = `${s.title || ''} ${s.content}`;
        return [...keywords].some((k) => text.includes(k));
      })
      .sort((a: any, b: any) => a.sequence - b.sequence)
      // 같은 내용 문장 중복 제거
      .filter(
        (s: any, index: number, self: any[]) =>
          index === self.findIndex((t) => t.content === s.content),
      );

    if (locSentences.length > 0) {
      result.push({ location: loc, sentences: locSentences });
    }
  });
  return result;
}

function selectWarning(id: number) {
  selectedWarningId.value = id;
}

onMounted(() => {
  fetchWarningZones();
  fetchWarnings();
});
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full flex-col gap-3 overflow-hidden">
      <!-- 머리 (모바일에서는 줄바꿈으로 잘림을 막는다) -->
      <div
        class="bg-card border-border flex flex-wrap items-center justify-between gap-2 rounded-lg border p-3"
      >
        <h2 class="flex items-center gap-2 text-lg font-bold text-red-600">
          <IconifyIcon class="size-5" icon="lucide:alert-triangle" />
          기상 특보 현황
        </h2>
        <Space wrap>
          <Select
            v-model:value="searchCommand"
            :options="commandOptions"
            allow-clear
            class="max-w-full"
            placeholder="명령 필터"
            style="width: 160px"
          />
          <Button :loading="loading" @click="fetchWarnings">
            <IconifyIcon class="mr-1 size-4" icon="lucide:rotate-cw" />
            새로고침
          </Button>
        </Space>
      </div>

      <div class="grid min-h-0 flex-1 grid-cols-1 gap-3 lg:grid-cols-12">
        <!-- 목록 (왼쪽) -->
        <div class="min-h-0 overflow-y-auto pr-1 lg:col-span-5">
          <Spin :spinning="loading">
            <div v-if="groupedWarnings.length > 0" class="relative space-y-3 pl-5">
              <!-- 타임라인 세로선 -->
              <div class="bg-border absolute bottom-2 left-1.5 top-2 w-0.5"></div>

              <div v-for="group in groupedWarnings" :key="group.latest.id" class="relative">
                <!-- 타임라인 점 -->
                <span
                  class="border-card absolute -left-5 top-2 size-3.5 rounded-full border-2 bg-red-500"
                ></span>

                <div
                  class="bg-card border-border rounded-lg border border-l-4 border-l-red-500 shadow-sm transition-all hover:shadow-md"
                >
                  <!-- 카드 머리 -->
                  <div class="border-border bg-muted/40 flex items-center justify-between gap-2 rounded-t-lg border-b px-3 py-2">
                    <div class="flex min-w-0 flex-1 items-center gap-2">
                      <div class="text-muted-foreground max-w-[60%] truncate text-xs font-bold">
                        {{ group.latest.title }}
                      </div>
                      <div class="text-muted-foreground flex shrink-0 items-center gap-1 text-[9px]">
                        <span class="font-mono opacity-70">
                          {{ formatTmFc(group.latest.tmFc, 'MM-DD HH:mm') }}
                        </span>
                        <span class="opacity-60">({{ elapsedFromNow(group.latest.tmFc) }})</span>
                      </div>
                    </div>
                    <div
                      v-if="group.warningNum"
                      class="shrink-0 rounded border border-red-200 bg-red-500/10 px-1.5 py-0.5 font-mono text-[9px] text-red-500 dark:border-red-900"
                    >
                      {{ group.warningNum }}
                    </div>
                  </div>

                  <div class="p-2.5">
                    <!-- 정보 영역 (가로 배치) -->
                    <div class="mb-2 flex items-stretch gap-2">
                      <!-- 발표 이력 -->
                      <div
                        class="border-border bg-muted/30 flex min-w-0 flex-1 flex-col rounded border p-1.5"
                      >
                        <div class="text-muted-foreground mb-1 text-[8px] font-bold uppercase tracking-wider opacity-70">
                          History
                        </div>
                        <div class="flex flex-wrap gap-1">
                          <div
                            v-for="item in group.items"
                            :key="item.id"
                            :class="{
                              'bg-card border-blue-400 shadow-sm ring-1 ring-blue-300':
                                selectedWarningId === item.id,
                            }"
                            class="hover:bg-card flex cursor-pointer items-center gap-1 rounded border border-transparent px-1.5 py-0.5 transition-all"
                            @click="selectWarning(item.id)"
                          >
                            <span class="text-muted-foreground font-mono text-[9px]">
                              {{ formatTmFc(item.tmFc, 'HH:mm') }}
                            </span>
                            <Tag
                              v-if="item.command"
                              :color="item.command.includes('해제') ? 'green' : 'red'"
                              class="!m-0 !px-1 !text-[9px] !leading-4"
                            >
                              {{ item.command }}
                            </Tag>
                          </div>
                        </div>
                      </div>

                      <!-- 관리지역 요약 -->
                      <div
                        v-if="getLocationSentences(group.latest).length > 0"
                        class="flex min-w-0 flex-[1.5] flex-col rounded-lg border border-indigo-200 bg-indigo-500/10 p-1.5 dark:border-indigo-800/50"
                      >
                        <div class="mb-1 text-[8px] font-bold uppercase tracking-wider text-indigo-400 opacity-80">
                          Target Area
                        </div>
                        <div class="flex-1 space-y-1.5">
                          <div
                            v-for="(item, idx) in getLocationSentences(group.latest)"
                            :key="idx"
                            class="border-b border-indigo-200/40 pb-1.5 last:border-0 last:pb-0 dark:border-indigo-800/30"
                          >
                            <div class="flex flex-col gap-0.5">
                              <div class="flex items-center justify-between">
                                <span class="truncate text-xs font-black text-indigo-800 dark:text-indigo-300">
                                  {{ item.location.name }}
                                </span>
                                <span
                                  v-if="
                                    item.location.warningAreaCode &&
                                    zoneMap[item.location.warningAreaCode]
                                  "
                                  class="text-muted-foreground ml-1 shrink-0 text-[8px]"
                                >
                                  {{ zoneMap[item.location.warningAreaCode]?.regKo }}
                                </span>
                              </div>
                              <div class="flex flex-wrap gap-1">
                                <template v-for="s in item.sentences" :key="s.id">
                                  <span
                                    v-if="s.title"
                                    class="bg-card flex items-center gap-0.5 rounded border border-red-200 px-1 py-0.5 text-[8px] font-bold text-red-600 shadow-sm dark:border-red-900/40 dark:text-red-400"
                                  >
                                    <span class="size-1 animate-pulse rounded-full bg-red-500"></span>
                                    {{ s.title.replace(/^o\s*/, '') }}
                                  </span>
                                </template>
                              </div>
                            </div>
                          </div>
                        </div>
                      </div>
                    </div>

                    <div class="text-muted-foreground line-clamp-1 text-[11px] italic opacity-90">
                      {{ group.latest.content }}
                    </div>
                    <div v-if="group.latest.other" class="text-muted-foreground mt-1 truncate text-[9px]">
                      <span class="font-bold">※</span> {{ group.latest.other }}
                    </div>
                  </div>
                </div>
              </div>
            </div>

            <div
              v-else-if="!loading"
              class="text-muted-foreground flex flex-col items-center justify-center gap-3 py-20"
            >
              <IconifyIcon class="size-14 opacity-40" icon="lucide:alert-triangle" />
              <p>현재 발효중인 특보 정보가 없습니다.</p>
            </div>
          </Spin>
        </div>

        <!-- 상세 (오른쪽) -->
        <div class="min-h-0 overflow-y-auto pr-1 lg:col-span-7">
          <WeatherWarningRegionMatch
            v-if="selectedWarningId"
            :warning-id="selectedWarningId"
          />
          <Empty
            v-else
            class="mt-20"
            description="목록에서 특보를 선택하여 상세 정보를 확인하세요."
          />
        </div>
      </div>
    </div>
  </Page>
</template>
