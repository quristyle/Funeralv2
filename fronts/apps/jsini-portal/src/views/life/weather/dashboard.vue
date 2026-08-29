<script lang="ts" setup>
import type { LifeWeatherApi } from '#/api/life/weather';

import { computed, onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import { Button, message, Select, Space, Spin } from 'ant-design-vue';

import { getLatestWeather } from '#/api/life/weather';

import SingleWeatherWidget from './modules/single-weather-widget.vue';
import WeatherForecastChart from './modules/weather-forecast-chart.vue';
import WeatherWarningTimeline from './modules/weather-warning-timeline.vue';
import WeatherWeeklyForecast from './modules/weather-weekly-forecast.vue';

/**
 * [기상 현황판] — 원본 WeatherDashboard.vue 이식.
 *
 * 전 지역 최신 실황(getLatestWeather)을 위젯 그리드로 깔고, 그 위에
 * 오늘의 기상 특보 띠를 얹었다. 위젯을 클릭하면(또는 지역을 선택하면)
 * 아래의 주간 예보 · 예보 추이 차트가 그 지역을 따라간다.
 * 기준 초과 이벤트는 각 위젯이 스스로 표시한다 (getCurrentEvents).
 *
 * 원본 대비: 위젯 1장 + 지역선택 → 전 지역 위젯 그리드로 확장,
 * 시각별 기온 이력 미니 차트(WeatherHistoryChart)는 이식 대상에서 제외했다.
 */

const router = useRouter();

const loading = ref(false);
const weatherData = ref<LifeWeatherApi.Info[]>([]);
const selectedLocationId = ref<null | number>(null);
const warningTimelineRef = ref<InstanceType<typeof WeatherWarningTimeline>>();

const locationOptions = computed(() =>
  weatherData.value.map((w) => ({
    label: w.location,
    value: w.weatherLocationId ?? 0,
  })),
);

const selectedName = computed(
  () =>
    weatherData.value.find((w) => w.weatherLocationId === selectedLocationId.value)?.location ??
    '',
);

/** 전 지역 최신 실황 조회 */
async function fetchWeather() {
  loading.value = true;
  try {
    const res = (await getLatestWeather()) ?? [];
    weatherData.value = res;
    if (res.length === 0) {
      message.info('등록된 기상 관측 지역이 없거나 데이터를 불러올 수 없습니다.');
    } else if (
      !selectedLocationId.value ||
      !res.some((w) => w.weatherLocationId === selectedLocationId.value)
    ) {
      selectedLocationId.value = res[0]?.weatherLocationId ?? null;
    }
  } catch {
    message.error('날씨 정보를 가져오는데 실패했습니다.');
  } finally {
    loading.value = false;
  }
}

/** 화면 전체 새로고침 (실황 + 특보 띠) */
function refreshAll() {
  fetchWeather();
  warningTimelineRef.value?.reload();
}

onMounted(fetchWeather);
</script>

<template>
  <Page auto-content-height>
    <div class="flex h-full flex-col gap-3 overflow-hidden">
      <!-- 도구줄 -->
      <div class="bg-card border-border flex items-center justify-between rounded-lg border p-3">
        <Space>
          <span class="text-sm font-semibold">지역 선택</span>
          <Select
            v-model:value="selectedLocationId"
            :options="locationOptions"
            placeholder="차트 조회 지역 선택"
            style="width: 180px"
          />
        </Space>
        <Space>
          <Button @click="router.push('/life/weather/forecast')">
            <IconifyIcon class="mr-1 size-4" icon="lucide:clock" />
            시간대별 예보
          </Button>
          <Button @click="router.push('/life/weather/warning')">
            <IconifyIcon class="mr-1 size-4" icon="lucide:alert-triangle" />
            특보 현황
          </Button>
          <Button :loading="loading" @click="refreshAll">
            <IconifyIcon class="mr-1 size-4" icon="lucide:rotate-cw" />
            새로고침
          </Button>
        </Space>
      </div>

      <!-- 본문 (내부 스크롤) -->
      <div class="min-h-0 flex-1 space-y-3 overflow-y-auto pr-1">
        <!-- 오늘의 기상 특보 -->
        <WeatherWarningTimeline ref="warningTimelineRef" />

        <!-- 지역별 실황 위젯 그리드 -->
        <Spin :spinning="loading">
          <div class="grid grid-cols-1 gap-3 md:grid-cols-2 xl:grid-cols-3">
            <SingleWeatherWidget
              v-for="w in weatherData"
              :key="w.weatherLocationId ?? w.id"
              :data="w"
              :selected="w.weatherLocationId === selectedLocationId"
              @select="selectedLocationId = w.weatherLocationId ?? null"
            />
          </div>
        </Spin>

        <!-- 선택 지역 주간 예보 -->
        <div v-if="selectedLocationId" class="bg-card border-border rounded-lg border p-4">
          <h3 class="text-foreground mb-2 text-base font-bold">
            주간 예보
            <span class="text-muted-foreground ml-1 text-sm font-normal">{{ selectedName }}</span>
          </h3>
          <WeatherWeeklyForecast :location-id="selectedLocationId" />
        </div>

        <!-- 선택 지역 예보 추이 차트 -->
        <div v-if="selectedLocationId" class="bg-card border-border rounded-lg border p-4">
          <h3 class="text-foreground mb-2 text-base font-bold">
            예보 추이
            <span class="text-muted-foreground ml-1 text-sm font-normal">{{ selectedName }}</span>
          </h3>
          <WeatherForecastChart :location-id="selectedLocationId" />
        </div>
      </div>
    </div>
  </Page>
</template>
