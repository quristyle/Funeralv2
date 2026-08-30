<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import { Alert, Button, Card, Col, Row, Spin, Tag } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getServerReport } from '#/api/helpdesk';

/**
 * [장애 예측]
 *
 * 원본(JinReception reports/Prediction.vue, `/reports/prediction`).
 * OADR 의 PREDICTION 쿼리로 최근 6시간 성능 추세에서 뽑아낸 장애 전조 징후를 본다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * 가져오기 방식은 그대로다 — PREDICTION 을 한 번에 전량 받아 차트와 표가 같은
 * 배열을 본다. 그래서 표는 `:table-data` 로 받는다.
 * ------------------------------------------------------------
 */

const loading = ref(false);
const rows = ref<Record<string, any>[]>([]);

const chartRef = ref<EchartsUIType>();
const { renderEcharts } = useEcharts(chartRef);

/** 징후별 한글 라벨과 조치 가이드. 원본의 symptomMap 을 그대로 옮겼다. */
interface SymptomInfo {
  action: string;
  color: string;
  desc: string;
  label: string;
}

const SYMPTOMS: Record<string, SymptomInfo> = {
  'Log Throughput Risk': {
    action: '대량 배치를 분산 처리하거나 로그 파일 전용 디스크 성능 점검 권고',
    color: 'warning',
    desc: '데이터 변경(CUD) 기록 속도가 디스크 성능 한계에 도달했습니다.',
    label: '로그 처리량 위기',
  },
  'Memory Pressure': {
    action: 'Missing Index(누락된 인덱스) 생성 및 PLE 지표 정밀 확인 필요',
    color: 'error',
    desc: '데이터를 담을 메모리가 부족하여 디스크에서 직접 데이터를 읽어오는 빈도가 높습니다.',
    label: '메모리 압박 감지',
  },
  'TempDB Saturation Risk': {
    action: '복잡한 JOIN/SORT 쿼리 최적화 및 불필요한 임시 테이블 제거 권고',
    color: 'error',
    desc: '임시 테이블이나 정렬 작업이 너무 많아 DB 내부 작업 공간이 부족한 상태입니다.',
    label: '임시 DB 포화 위험',
  },
};

/** 매핑에 없는 징후는 원문 그대로 보여준다. */
function symptomInfo(symptom?: string): SymptomInfo {
  return (
    SYMPTOMS[symptom ?? ''] ?? {
      action: '',
      color: 'default',
      desc: '',
      label: symptom ?? '',
    }
  );
}

/** 하단 기술 가이드. 원본의 고정 안내 블록. */
const TECH_GUIDE = [
  {
    desc: '임시 계산용 공간이 꽉 찼음을 뜻합니다. 정렬이 많은 쿼리를 찾으십시오.',
    title: 'TempDB Saturation',
    tone: 'text-red-600',
  },
  {
    desc: '데이터를 쓰는 속도가 디스크 성능을 넘어서고 있습니다. 배치 분산이 필요합니다.',
    title: 'Log Throughput',
    tone: 'text-orange-600',
  },
  {
    desc: '데이터 캐시 공간 부족입니다. 인덱스 최적화로 메모리 효율을 높여야 합니다.',
    title: 'Memory Pressure',
    tone: 'text-blue-600',
  },
];

const [Grid] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      {
        field: 'Symptom',
        // 값이 정해진 칸이다 — 저장된 값(영문)에 한글 이름표를 붙여 고르게 한다.
        params: {
          filterOptions: Object.entries(SYMPTOMS).map(([value, info]) => ({
            label: info.label,
            value,
          })),
        },
        // 한글 이름표와 원문을 두 줄로 쌓는다. 전역 `showOverflow` 를 끄지 않으면 잘린다.
        showOverflow: false,
        slots: { default: 'Symptom' },
        title: '감지된 장애 징후',
        width: 200,
      },
      {
        field: 'Occurrences',
        slots: { default: 'Occurrences' },
        title: '발생 건수',
        width: 110,
      },
      {
        align: 'left',
        // 징후에서 끌어온 안내라 행에 없는 칸이다. 훑을 글자를 직접 준다.
        field: 'guide',
        minWidth: 320,
        params: {
          filterText: (row: any) => {
            const info = symptomInfo(row.Symptom);
            return `${info.desc} ${info.action}`;
          },
          sort: false,
        },
        // 안내 문구는 두 줄로 접힌다. 전역 `showOverflow` 를 끄지 않으면 한 줄로 잘린다.
        showOverflow: false,
        slots: { default: 'guide' },
        title: '상세 분석 및 조치 가이드',
      },
    ],
    // 행 배열은 `:table-data` 로 간다.
    data: [],
    emptyText: '감지된 특이 징후가 없습니다. 시스템이 매우 안정적입니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 위쪽 '패턴 재분석' 이 부르는 것과 같은 함수를 준다.
    gridFeatures: { onRefresh: () => loadData() },
    height: 300,
    // 전량을 한 번에 받는 표다. 켜 두면 응답을 `{ result, page }` 로 읽어 한 행도 안 나온다.
    pagerConfig: { enabled: false },
    rowConfig: { keyField: 'Symptom' },
  } as any,
});

/** 막대 색은 심각도 순서대로. 원본과 같은 팔레트. */
const BAR_COLORS = ['#EF4444', '#F59E0B', '#3B82F6'];

function drawChart() {
  renderEcharts({
    grid: { bottom: 24, containLabel: true, left: 10, right: 16, top: 20 },
    series: [
      {
        barMaxWidth: 40,
        data: rows.value.map((d, index) => ({
          itemStyle: {
            borderRadius: 6,
            color: BAR_COLORS[index % BAR_COLORS.length],
          },
          value: Number(d.Occurrences ?? 0),
        })),
        name: '감지 횟수',
        type: 'bar',
      },
    ],
    tooltip: { trigger: 'axis' },
    xAxis: {
      axisLabel: { fontSize: 10, interval: 0, width: 90, overflow: 'break' },
      data: rows.value.map((d) => symptomInfo(d.Symptom).label),
      type: 'category',
    },
    yAxis: { minInterval: 1, type: 'value' },
  });
}

async function loadData() {
  loading.value = true;
  try {
    rows.value =
      (await getServerReport<Record<string, any>[]>('PREDICTION')) ?? [];
    drawChart();
  } finally {
    loading.value = false;
  }
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <span class="text-base font-semibold">
          시스템 장애 전조 징후 탐지 및 예방 분석 리포트
        </span>
        <Button :loading="loading" danger @click="loadData">패턴 재분석</Button>
      </div>
    </Card>

    <Alert
      class="mb-3"
      message="최근 6시간 동안 수집된 성능 트렌드를 바탕으로 발생 빈도가 높은 장애 전조 패턴을 추출했습니다."
      show-icon
      type="info"
    />

    <Spin :spinning="loading">
      <Row :gutter="[12, 12]">
        <!-- 1. 징후별 발생 빈도 -->
        <Col :lg="10" :xs="24">
          <Card size="small">
            <template #title>
              <span class="text-xs font-semibold uppercase text-muted-foreground">
                Symptom Occurrence Frequency
              </span>
            </template>

            <EchartsUI ref="chartRef" height="250px" />
            <p class="mt-3 text-center text-[10px] italic text-muted-foreground">
              "발생 횟수가 많을수록 장애로 이어질 확률이 높습니다."
            </p>
          </Card>
        </Col>

        <!-- 2. 상세 진단 소견 -->
        <Col :lg="14" :xs="24">
          <Card :body-style="{ padding: 0 }" size="small">
            <template #title>
              <span class="text-xs font-semibold uppercase text-red-600">
                장애 예방 정밀 진단 소견
              </span>
            </template>

            <Grid :table-data="rows">
              <template #Symptom="{ row }">
                <div class="font-semibold text-red-700 dark:text-red-400">
                  {{ symptomInfo(row.Symptom).label }}
                </div>
                <div class="text-[10px] text-muted-foreground">
                  {{ row.Symptom }}
                </div>
              </template>

              <template #Occurrences="{ row }">
                <Tag :color="Number(row.Occurrences) > 10 ? 'error' : 'warning'">
                  {{ row.Occurrences }}건
                </Tag>
              </template>

              <template #guide="{ row }">
                <p class="m-0 text-[11px] leading-tight text-muted-foreground">
                  {{ symptomInfo(row.Symptom).desc }}
                </p>
                <p
                  v-if="symptomInfo(row.Symptom).action"
                  class="m-0 mt-1 text-[11px] font-semibold leading-tight"
                >
                  ● 권고: {{ symptomInfo(row.Symptom).action }}
                </p>
              </template>
            </Grid>
          </Card>
        </Col>
      </Row>

      <!-- 3. 기술 가이드 -->
      <Card class="mt-3" size="small">
        <Row :gutter="[12, 12]">
          <Col v-for="item in TECH_GUIDE" :key="item.title" :lg="8" :xs="24">
            <span class="text-xs font-semibold" :class="item.tone">
              {{ item.title }}
            </span>
            <p class="m-0 text-[11px] text-muted-foreground">{{ item.desc }}</p>
          </Col>
        </Row>
      </Card>
    </Spin>
  </Page>
</template>
