<script lang="ts" setup>
import type { EchartsUIType } from '@vben/plugins/echarts';

import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { EchartsUI, useEcharts } from '@vben/plugins/echarts';

import {
  Alert,
  Button,
  Card,
  Col,
  Empty,
  Row,
  Spin,
  Table,
  Tag,
} from 'ant-design-vue';

import { getServerReport } from '#/api/helpdesk';

/**
 * [장애 예측]
 *
 * 원본(JinReception reports/Prediction.vue, `/reports/prediction`).
 * OADR 의 PREDICTION 쿼리로 최근 6시간 성능 추세에서 뽑아낸 장애 전조 징후를 본다.
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

const columns = [
  { dataIndex: 'Symptom', key: 'Symptom', title: '감지된 장애 징후', width: 200 },
  {
    dataIndex: 'Occurrences',
    key: 'Occurrences',
    title: '발생 건수',
    width: 110,
  },
  { key: 'guide', title: '상세 분석 및 조치 가이드' },
];

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

            <Table
              :columns="columns"
              :data-source="rows"
              :pagination="false"
              :scroll="{ x: 600 }"
              row-key="Symptom"
              size="small"
            >
              <template #emptyText>
                <Empty description="감지된 특이 징후가 없습니다. 시스템이 매우 안정적입니다." />
              </template>

              <template #bodyCell="{ column, record }">
                <template v-if="column.key === 'Symptom'">
                  <div class="font-semibold text-red-700 dark:text-red-400">
                    {{ symptomInfo(record.Symptom).label }}
                  </div>
                  <div class="text-[10px] text-muted-foreground">
                    {{ record.Symptom }}
                  </div>
                </template>

                <template v-else-if="column.key === 'Occurrences'">
                  <Tag
                    :color="Number(record.Occurrences) > 10 ? 'error' : 'warning'"
                  >
                    {{ record.Occurrences }}건
                  </Tag>
                </template>

                <template v-else-if="column.key === 'guide'">
                  <p class="m-0 text-[11px] leading-tight text-muted-foreground">
                    {{ symptomInfo(record.Symptom).desc }}
                  </p>
                  <p
                    v-if="symptomInfo(record.Symptom).action"
                    class="m-0 mt-1 text-[11px] font-semibold leading-tight"
                  >
                    ● 권고: {{ symptomInfo(record.Symptom).action }}
                  </p>
                </template>
              </template>
            </Table>
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
