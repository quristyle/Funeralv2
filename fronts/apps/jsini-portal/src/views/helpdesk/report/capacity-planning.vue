<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  Col,
  Empty,
  Progress,
  Row,
  Spin,
  Tag,
} from 'ant-design-vue';

import { getServerReport } from '#/api/helpdesk';

/**
 * [용량 계획]
 *
 * 원본(JinReception reports/CapacityPlanning.vue, `/reports/capacity-planning`).
 * EXECUTIVE 쿼리의 건강 점수를 인프라 증설 판단 근거로 풀어 보여준다.
 */

const loading = ref(false);
const executive = ref<null | Record<string, any>>(null);

/** 점수 구간별 진단. 원본의 healthStatus 와 같은 기준이다. */
const healthStatus = computed(() => {
  const score = Number(executive.value?.Server_Health_Score ?? 0);

  if (score >= 90) {
    return {
      color: 'success',
      desc: '시스템이 매우 안정적인 상태이며, 현재 사양으로 충분한 여유가 있습니다.',
      hex: '#10B981',
      label: '최적 (Optimal)',
      score,
    };
  }
  if (score >= 75) {
    return {
      color: 'processing',
      desc: '전반적으로 안정적이나, 피크 시점에 미세한 지연이 감지됩니다.',
      hex: '#3B82F6',
      label: '양호 (Good)',
      score,
    };
  }
  if (score >= 60) {
    return {
      color: 'warning',
      desc: '특정 서브시스템에서 병목이 발생하고 있습니다. 정밀 점검을 권고합니다.',
      hex: '#F59E0B',
      label: '주의 (Warning)',
      score,
    };
  }
  return {
    color: 'error',
    desc: '빈번한 성능 장애가 발생 중입니다. 즉각적인 인프라 최적화 또는 증설이 시급합니다.',
    hex: '#EF4444',
    label: '위기 (Critical)',
    score,
  };
});

/** 점수 감점 기준. 원본의 scoringCriteria. */
const SCORING_CRITERIA = [
  {
    desc: '쿼리 경합 및 데이터 변경 지연 발생 시',
    label: '임시DB/로그 병목',
    point: '-25점',
  },
  {
    desc: '인덱스 부재로 인한 강제 디스크 읽기 발생 시',
    label: '메모리 부족(PageIO)',
    point: '-20점',
  },
  {
    desc: '물리적 스토리지 속도가 25ms를 초과할 시',
    label: '디스크 응답 저하',
    point: '-20점',
  },
];

/** 하단 운영 가이드 카드. 원본의 고정 문구를 유지했다. */
const OPERATION_GUIDE = [
  {
    caption: '데이터 정합성',
    note: '성능 지표 수집 신뢰도 99.9% 확보 중',
    title: 'SLA 목표치 대비 안정',
  },
  {
    caption: '자원 최적화',
    note: 'I/O Stall 감소 시 점수 15% 상승 기대',
    title: '병목 쿼리 집중 튜닝 권고',
  },
  {
    caption: '차기 확장 계획',
    note: '비즈니스 증가율 대비 자원 수용력 충분',
    title: '현 사양 유지 가능',
  },
];

async function loadData() {
  loading.value = true;
  try {
    const exec = await getServerReport<Record<string, any>[]>('EXECUTIVE');
    executive.value = exec?.[0] ?? null;
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
          인프라 운영 효율 및 건강 점수(Health Score) 리포트
        </span>
        <Button :loading="loading" @click="loadData">점수 재산출</Button>
      </div>
    </Card>

    <Spin :spinning="loading">
      <Empty
        v-if="!executive && !loading"
        description="건강 점수를 불러오지 못했습니다."
      />

      <template v-else-if="executive">
        <Row :gutter="[12, 12]">
          <!-- 1. 메인 건강 점수 -->
          <Col :lg="14" :xs="24">
            <Card size="small">
              <template #title>
                <span class="text-xs font-semibold uppercase text-muted-foreground">
                  Monthly System Reliability Score
                </span>
              </template>
              <template #extra>
                <Tag :color="healthStatus.color">{{ healthStatus.label }}</Tag>
              </template>

              <div class="flex flex-col items-center py-6">
                <div class="flex items-baseline">
                  <span class="text-7xl font-bold">
                    {{ Math.round(healthStatus.score) }}
                  </span>
                  <span class="ml-2 text-xl font-semibold text-muted-foreground">
                    / 100
                  </span>
                </div>

                <div class="mt-8 w-full px-10">
                  <Progress
                    :percent="Math.round(healthStatus.score)"
                    :show-info="false"
                    :stroke-color="healthStatus.hex"
                  />
                </div>

                <p
                  class="mt-8 rounded border border-border bg-accent/40 p-3 text-center text-sm font-medium leading-relaxed"
                >
                  {{ healthStatus.desc }}
                </p>
              </div>
            </Card>
          </Col>

          <!-- 2. 점수 산출 근거 -->
          <Col :lg="10" :xs="24">
            <Card size="small">
              <template #title>
                <span class="text-xs font-semibold uppercase text-muted-foreground">
                  점수 산출 기준 (Deduction Criteria)
                </span>
              </template>

              <div
                v-for="item in SCORING_CRITERIA"
                :key="item.label"
                class="flex items-start justify-between border-b border-border py-3 first:pt-0 last:border-b-0"
              >
                <div>
                  <div class="text-xs font-semibold">{{ item.label }}</div>
                  <div class="mt-1 text-[10px] text-muted-foreground">
                    {{ item.desc }}
                  </div>
                </div>
                <Tag class="font-mono">{{ item.point }}</Tag>
              </div>

              <div
                class="mt-4 rounded border border-border p-3 text-[10px] italic leading-snug text-muted-foreground"
              >
                * 본 점수는 최근 1개월간 발생한 병목 이벤트의 빈도와 강도를 가중치
                분석하여 자동 산출되었습니다. 인프라 투자 및 확장 계획의 핵심 근거
                데이터로 활용됩니다.
              </div>
            </Card>
          </Col>
        </Row>

        <!-- 3. 핵심 운영 가이드 -->
        <Row :gutter="[12, 12]" class="mt-3">
          <Col v-for="item in OPERATION_GUIDE" :key="item.caption" :lg="8" :xs="24">
            <Card size="small">
              <div class="text-[10px] font-semibold uppercase text-muted-foreground">
                {{ item.caption }}
              </div>
              <div class="mt-1 text-base font-semibold">{{ item.title }}</div>
              <p class="m-0 mt-2 text-[11px] text-muted-foreground">
                {{ item.note }}
              </p>
            </Card>
          </Col>
        </Row>

        <Alert
          class="mt-3"
          description="이 화면의 문구 일부는 원본과 동일한 고정 안내입니다. 실제 수치는 건강 점수만 OADR 에서 계산되어 내려옵니다."
          message="안내"
          show-icon
          type="info"
        />
      </template>
    </Spin>
  </Page>
</template>
