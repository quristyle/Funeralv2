<script lang="ts" setup>
import type { LifeWeatherApi } from '#/api/life/weather';

import { computed, onMounted, ref, watch } from 'vue';

import { Alert, Divider, Empty, message, Spin, Tag } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getWarningFullDetails } from '#/api/life/weather';

import { formatTmFc } from './weather-shared';

/**
 * [특보 상세 · 관리지역 매칭] — 원본 WeatherWarningRegionMatch.vue 이식.
 *
 * getWarningFullDetails 한 번으로 특보 · 통보문 · 구역별 발효 현황 ·
 * 매칭된 관리지역 · 문장을 받아, 통보문 본문에서 관리지역 구역명을 노랗게 강조한다.
 * 본문은 HTML 이스케이프 후 강조 태그만 끼워 넣는다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] 구역별 상세 발효 현황 표를 ant-design-vue `<Table>` 에서
 * `useVbenVxeGrid` 로 옮겼다. 정렬·필터는 공통 레이어
 * (`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **가져오기 방식은 그대로다** — 위의 한 번 조회로 받은 `details` 를
 * `:table-data` 로 넘긴다. 이 화면은 `Page` 가 아니라 상세 패널 안이라
 * `page-fill-last` 가 없다. 그래서 원본 `:scroll="{ y: 320 }"` 를
 * `height: 320` 숫자로 옮겼다.
 * ------------------------------------------------------------
 */

const props = defineProps<{
  warningId: number;
}>();

const loading = ref(false);
const warning = ref<LifeWeatherApi.Warning | null>(null);
const msg = ref<any>(null);
const details = ref<any[]>([]);
const matchedLocations = ref<LifeWeatherApi.Location[]>([]);
const relatedZones = ref<any[]>([]);
const sentences = ref<any[]>([]);

async function fetchData() {
  if (!props.warningId) return;
  loading.value = true;
  try {
    const data = await getWarningFullDetails(props.warningId);
    warning.value = data?.warning ?? null;
    msg.value = data?.msg ?? null;
    details.value = data?.details ?? [];
    matchedLocations.value = data?.matchedLocations ?? [];
    relatedZones.value = data?.relatedZones ?? [];
    sentences.value = data?.sentences ?? [];
  } catch {
    message.warning('상세 정보를 불러오지 못했습니다.');
  } finally {
    loading.value = false;
  }
}

const zoneMap = computed(() => {
  const map: Record<string, any> = {};
  relatedZones.value.forEach((z) => {
    map[z.regId] = z;
  });
  return map;
});

/** 강조할 구역명 키워드 (긴 것 먼저) */
const highlightKeywords = computed(() => {
  const keywords = new Set<string>();
  relatedZones.value.forEach((zone) => {
    if (zone.regKo) keywords.add(zone.regKo);
    if (zone.regName) {
      zone.regName.split(/\s+/).forEach((word: string) => {
        if (word.trim()) keywords.add(word.trim());
      });
    }
  });
  return [...keywords].sort((a, b) => b.length - a.length);
});

/** 관리지역별 관련 문장 묶음 */
const locationSentences = computed(() => {
  if (matchedLocations.value.length === 0 || sentences.value.length === 0) return [];
  const result: { location: LifeWeatherApi.Location; sentences: any[] }[] = [];

  matchedLocations.value.forEach((loc) => {
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

    const locSentences = sentences.value
      .filter((s) => {
        const text = `${s.title || ''} ${s.content}`;
        return [...keywords].some((k) => text.includes(k));
      })
      .sort((a, b) => a.sequence - b.sequence);

    if (locSentences.length > 0) {
      result.push({ location: loc, sentences: locSentences });
    }
  });
  return result;
});

function escapeHtml(text: string) {
  return text
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

function escapeRegExp(text: string) {
  return text.replaceAll(/[$()*+.?[\\\]^{|}]/g, String.raw`\$&`);
}

/** 본문을 이스케이프한 뒤 구역명 키워드만 강조 태그로 감싼다 */
function highlightText(text?: null | string) {
  if (!text) return '';
  const escaped = escapeHtml(text);
  if (highlightKeywords.value.length === 0) return escaped;
  const pattern = highlightKeywords.value.map((k) => escapeRegExp(escapeHtml(k))).join('|');
  const regex = new RegExp(`(${pattern})`, 'g');
  return escaped.replaceAll(regex, '<span class="highlight-region">$1</span>');
}

const [DetailGrid] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      { field: 'areaName', title: '발효구역', width: 140 },
      {
        field: 'warnVarName',
        slots: { default: 'warnVarName' },
        title: '특보종류',
        width: 110,
      },
      {
        field: 'startTime',
        minWidth: 180,
        // 화면에 보이는 것은 포맷한 시각이다. 필터가 훑을 글자를 그것으로 맞춘다.
        params: {
          filterText: (row: any) =>
            `${formatTmFc(row.startTime)} ${row.endTime ? formatTmFc(row.endTime) : ''}`,
        },
        // 발효시각은 '해제예정' 을 아래에 한 줄 더 그린다. 전역 `showOverflow` 로는
        // 한 줄로 잘리므로 이 칸만 푼다.
        showOverflow: false,
        slots: { default: 'startTime' },
        title: '발효시각',
      },
    ],
    emptyText: '구역별 발효 현황이 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 이 패널은 `fetchData` 한 번이 통보문까지 다 받아 오므로 그것을 준다.
    gridFeatures: { onRefresh: () => fetchData() },
    height: 320,
    // 전량을 한 번에 넘긴다. 페이저를 두지 않는다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'id' },
  } as any,
});

watch(() => props.warningId, fetchData);
onMounted(fetchData);
</script>

<template>
  <Spin :spinning="loading">
    <!-- 영향 받는 관리 지역 -->
    <Alert v-if="matchedLocations.length > 0" class="mb-3" show-icon type="error">
      <template #message>
        <span class="font-bold">영향을 받는 관리 지역 ({{ matchedLocations.length }}개)</span>
      </template>
      <template #description>
        <div class="mt-1 flex flex-wrap gap-1">
          <Tag v-for="loc in matchedLocations" :key="loc.id" color="red">
            <span class="mr-1 text-[10px] opacity-70">{{ loc.warningAreaCode }}</span>
            <span class="font-bold">{{ loc.name }}</span>
            <template v-if="loc.warningAreaCode && zoneMap[loc.warningAreaCode]">
              <span class="mx-1 opacity-50">|</span>
              <span>{{ zoneMap[loc.warningAreaCode]?.regKo }}</span>
              <span v-if="zoneMap[loc.warningAreaCode]?.regName" class="ml-1 text-[10px] opacity-90">
                [{{ zoneMap[loc.warningAreaCode]?.regName }}]
              </span>
            </template>
          </Tag>
        </div>
      </template>
    </Alert>
    <Alert
      v-else
      class="mb-3"
      message="이 특보에 영향을 받는 관리 지역이 없습니다."
      show-icon
      type="info"
    />

    <div class="bg-card border-border rounded-lg border p-4">
      <div v-if="msg" class="space-y-4 text-sm">
        <div>
          <span class="text-muted-foreground font-bold">[제목]</span>
          <p class="mt-1 text-lg font-bold text-blue-600 dark:text-blue-400">{{ msg.t1 }}</p>

          <!-- 특보 메타 정보 -->
          <div
            v-if="warning"
            class="border-border bg-muted/30 mt-3 rounded border p-3 text-xs"
          >
            <div class="grid grid-cols-2 gap-x-4 gap-y-1">
              <div class="flex">
                <span class="text-muted-foreground w-16 shrink-0">특보번호:</span>
                <span class="font-medium">{{ warning.warningNum || '-' }}</span>
              </div>
              <div class="flex">
                <span class="text-muted-foreground w-16 shrink-0">발표시각:</span>
                <span class="font-medium">{{ formatTmFc(warning.tmFc) }}</span>
              </div>
              <div class="flex">
                <span class="text-muted-foreground w-16 shrink-0">발표관서:</span>
                <span class="font-medium">{{ warning.stnId }}</span>
              </div>
              <div class="flex">
                <span class="text-muted-foreground w-16 shrink-0">발효번호:</span>
                <span class="font-medium">{{ warning.tmSeq }}</span>
              </div>
              <div
                v-if="warning.command"
                class="border-border col-span-2 mt-1 flex border-t pt-1"
              >
                <span class="text-muted-foreground w-16 shrink-0">명령:</span>
                <span class="font-medium">{{ warning.command }}</span>
              </div>
              <div v-if="warning.other" class="col-span-2 mt-1 flex">
                <span class="text-muted-foreground w-16 shrink-0">참고:</span>
                <span class="font-medium">{{ warning.other }}</span>
              </div>
            </div>
          </div>

          <!-- 관리지역별 관련 특보 현황 -->
          <div v-if="locationSentences.length > 0" class="mt-4">
            <span class="text-xs font-bold text-indigo-600 dark:text-indigo-400">
              [관리지역별 관련 특보 현황]
            </span>
            <div class="mt-2 space-y-2">
              <div
                v-for="(item, idx) in locationSentences"
                :key="idx"
                class="rounded border border-indigo-200 bg-indigo-500/10 p-2 dark:border-indigo-800"
              >
                <div class="mb-1 flex items-center">
                  <Tag color="blue">{{ item.location.name }}</Tag>
                  <span
                    v-if="item.location.warningAreaCode && zoneMap[item.location.warningAreaCode]"
                    class="text-muted-foreground text-[11px]"
                  >
                    {{ zoneMap[item.location.warningAreaCode]?.regKo }}
                    {{
                      zoneMap[item.location.warningAreaCode]?.regName
                        ? `(${zoneMap[item.location.warningAreaCode]?.regName})`
                        : ''
                    }}
                  </span>
                </div>
                <div class="ml-1 space-y-1 border-l-2 border-indigo-300 pl-2 dark:border-indigo-700">
                  <div v-for="s in item.sentences" :key="s.id" class="text-[11px]">
                    <div
                      v-if="s.title"
                      class="font-bold text-indigo-800 dark:text-indigo-300"
                      v-html="highlightText(s.title)"
                    ></div>
                    <div
                      class="text-foreground/80 ml-1 whitespace-pre-wrap leading-tight"
                      v-html="highlightText(s.content)"
                    ></div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <Divider class="!my-3" />
        <div>
          <span class="text-muted-foreground font-bold">[발표내용]</span>
          <p class="mt-1 whitespace-pre-wrap" v-html="highlightText(msg.t2)"></p>
        </div>

        <template v-if="msg.t6">
          <Divider class="!my-3" />
          <div>
            <span class="font-bold text-red-600">[특보 발효 현황]</span>
            <p
              class="mt-1 whitespace-pre-wrap rounded border border-red-200 bg-red-500/10 p-3 dark:border-red-900"
              v-html="highlightText(msg.t6)"
            ></p>
          </div>
        </template>

        <template v-if="msg.t7">
          <Divider class="!my-3" />
          <div>
            <span class="font-bold text-orange-600">[예비특보 현황]</span>
            <p
              class="mt-1 whitespace-pre-wrap rounded border border-orange-200 bg-orange-500/10 p-3 dark:border-orange-900"
              v-html="highlightText(msg.t7)"
            ></p>
          </div>
        </template>

        <!-- 구역별 상세 발효 현황 -->
        <template v-if="details.length > 0">
          <Divider class="!my-3" />
          <div>
            <span class="font-bold text-teal-600">[구역별 상세 발효 현황]</span>
            <DetailGrid class="mt-2" :table-data="details">
              <template #warnVarName="{ row }">
                <Tag color="green">{{ row.warnVarName }}</Tag>
              </template>
              <template #startTime="{ row }">
                <div class="flex flex-col">
                  <span class="text-xs font-bold">{{ formatTmFc(row.startTime) }}</span>
                  <span v-if="row.endTime" class="text-muted-foreground mt-0.5 text-[10px]">
                    해제예정: {{ formatTmFc(row.endTime) }}
                  </span>
                </div>
              </template>
            </DetailGrid>
          </div>
        </template>
      </div>
      <Empty v-else description="통보문 데이터가 없습니다." />
    </div>
  </Spin>
</template>

<style scoped>
:deep(.highlight-region) {
  background-color: #fef08a; /* yellow-200 */
  color: #dc2626; /* red-600 */
  font-weight: bold;
  padding: 0 2px;
  border-radius: 2px;
  box-shadow: 0 0 0 1px #fecaca;
}
</style>
