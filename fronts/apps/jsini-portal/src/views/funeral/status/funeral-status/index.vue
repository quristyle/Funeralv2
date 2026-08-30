<script lang="ts" setup>
import type { StatusApi } from '#/api/funeral/status';

import { computed, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Badge, Card, Col, message, Progress, Row } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getFuneralStatuses } from '#/api/funeral/status';

/**
 * [빈소 통합 실시간 이용 현황]
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **가져오기 방식은 그대로다** — 전량을 한 번에 받아 화면에서 다룬다.
 * 위의 통계 카드가 같은 목록을 세므로, 조회 결과를 `list` 에 담아 둔다.
 * ------------------------------------------------------------
 */

const list = ref<StatusApi.FuneralStatus[]>([]);

// 통계치 계산
const stats = computed(() => {
  const total = list.value.length;
  const using = list.value.filter((item) => item.status === 'USING').length;
  const empty = total - using;
  const rate = total > 0 ? Math.round((using / total) * 100) : 0;
  return { empty, rate, total, using };
});

const [Grid] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'roomName', slots: { default: 'dash' }, title: '빈소명', width: 120 },
      {
        field: 'status',
        // 값이 둘뿐인 칸이라 고르는 칸으로 준다(코드값을 손으로 치지 않게).
        params: {
          filterOptions: [
            { label: '이용중', value: 'USING' },
            { label: '대기중', value: 'EMPTY' },
          ],
        },
        slots: { default: 'status' },
        title: '상태',
        width: 100,
      },
      {
        field: 'deceasedName',
        slots: { default: 'deceasedName' },
        title: '고인명',
        width: 120,
      },
      { field: 'chiefMourner', slots: { default: 'dash' }, title: '상주', width: 180 },
      { field: 'coffinTime', slots: { default: 'dash' }, title: '입관 일시', width: 160 },
      {
        field: 'dischargeTime',
        slots: { default: 'dash' },
        title: '발인 일시',
        width: 160,
      },
      {
        field: 'burialPlace',
        minWidth: 180,
        slots: { default: 'dash' },
        title: '장지',
      },
    ],
    emptyText: '빈소 현황이 없습니다.',
    height: 'auto',
    // 전량 조회다. 이걸 빼면 vxe 가 응답을 `{ result, page }` 로 읽어 한 줄도 안 나온다.
    pagerConfig: { enabled: false },
    proxyConfig: {
      ajax: {
        query: async () => {
          try {
            const data = await getFuneralStatuses();
            list.value = data || [];
          } catch {
            message.error('현황 데이터 조회 실패');
            list.value = [];
          }
          return list.value;
        },
      },
    },
    rowConfig: { keyField: 'roomId' },
  },
});
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <!-- 통계 요약 카드 영역 -->
    <Row :gutter="16" class="mb-4">
      <Col :span="6">
        <Card title="전체 빈소" class="text-center">
          <div class="text-3xl font-extrabold text-primary">{{ stats.total }}개소</div>
        </Card>
      </Col>
      <Col :span="6">
        <Card title="사용 중 빈소" class="text-center">
          <div class="text-3xl font-extrabold text-red-600">{{ stats.using }}개소</div>
        </Card>
      </Col>
      <Col :span="6">
        <Card title="잔여 빈소" class="text-center">
          <div class="text-3xl font-extrabold text-green-600">{{ stats.empty }}개소</div>
        </Card>
      </Col>
      <Col :span="6">
        <Card title="빈소 가동률" class="text-center">
          <div class="flex items-center justify-center gap-4">
            <Progress type="circle" :percent="stats.rate" :size="50" stroke-color="#ff4d4f" />
            <span class="text-2xl font-bold">{{ stats.rate }}%</span>
          </div>
        </Card>
      </Col>
    </Row>

    <!-- 현황 테이블 -->
    <Grid table-title="빈소 통합 실시간 이용 현황">
      <template #status="{ row }">
        <Badge
          :status="row.status === 'USING' ? 'error' : 'default'"
          :text="row.status === 'USING' ? '이용중' : '대기중'"
        />
      </template>

      <template #deceasedName="{ row }">
        <span v-if="row.status === 'USING'" class="font-semibold text-foreground">
          {{ row.deceasedName }}
          <span class="text-xs text-muted-foreground ml-1">({{ row.deceasedGender === 'MALE' ? '남' : '여' }}, {{ row.deceasedAge }}세)</span>
        </span>
        <span v-else class="text-muted-foreground">-</span>
      </template>

      <!-- 값이 비면 빈칸 대신 '-' 를 보여 준다(원본과 같다). 여러 칸이 함께 쓴다. -->
      <template #dash="{ column, row }">
        <span v-if="row[column.field]">{{ row[column.field] }}</span>
        <span v-else class="text-muted-foreground">-</span>
      </template>
    </Grid>
  </Page>
</template>
