<script lang="ts" setup>
import type { BuildingApi } from '#/api/funeral/building';

import { computed, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Badge, Card, Col, message, Row } from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getDeceasedList } from '#/api/funeral/building';

/**
 * [실시간 고인 이송 및 진행 현황]
 *
 * ------------------------------------------------------------
 * [2026-08-30] ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로 옮겼다.
 * 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **가져오기 방식은 그대로다** — 전량을 한 번에 받는다. 프런트 페이징(10줄)만
 * 걷어냈다(전량 조회 화면은 페이저를 두지 않는다).
 *
 * 위에 있던 '고인명 또는 호실명' 검색칸도 걷어냈다 — 머리글 아래 필터줄이
 * 고인명·배정 빈소 칸마다 같은 일을 하고, 조건을 넣는 자리가 두 곳이 되면
 * 언젠가 둘이 어긋난다.
 * ------------------------------------------------------------
 */

const deceasedList = ref<BuildingApi.Deceased[]>([]);

// 단계별 인원 집계 — 상태 코드는 정본 셋이다 (47번 문서 D-RS1)
const stats = computed(() => {
  const inHospital = deceasedList.value.filter(
    (item) => item.status === 'FUNERAL_IN_PROGRESS',
  ).length;
  const discharged = deceasedList.value.filter(
    (item) => item.status === 'FUNERAL_DEPARTURE_COMPLETED',
  ).length;
  const completed = deceasedList.value.filter(
    (item) => item.status === 'COMPLETED',
  ).length;
  return { completed, discharged, inHospital };
});

const [Grid] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', slots: { default: 'dash' }, title: '고인명', width: 120 },
      {
        field: 'gender',
        // 화면에 보이는 글자('남성'·'여성')와 저장된 값('MALE'·'FEMALE')이 다르다.
        params: {
          filterOptions: [
            { label: '남성', value: 'MALE' },
            { label: '여성', value: 'FEMALE' },
          ],
        },
        slots: { default: 'gender' },
        title: '성별',
        width: 100,
      },
      {
        field: 'age',
        formatter: ({ cellValue }: any) => `${cellValue}세`,
        // 화면에는 '세' 가 붙는다. 필터가 훑을 글자를 그것으로 맞춘다.
        params: { filterText: (row: any) => `${row.age}세` },
        title: '연세',
        width: 100,
      },
      {
        field: 'roomName',
        slots: { default: 'dash' },
        title: '배정 빈소',
        width: 150,
      },
      {
        field: 'religion',
        slots: { default: 'dash' },
        title: '종교',
        width: 120,
      },
      {
        field: 'deathDate',
        slots: { default: 'dash' },
        title: '작고 일시',
        width: 180,
      },
      {
        field: 'status',
        // 값이 정해진 칸이라 고르는 칸으로 준다. 상태 코드는 정본 셋 (D-RS1).
        params: {
          filterOptions: [
            { label: '장례 진행중', value: 'FUNERAL_IN_PROGRESS' },
            { label: '출상 완료', value: 'FUNERAL_DEPARTURE_COMPLETED' },
            { label: '장례 종료', value: 'COMPLETED' },
          ],
        },
        slots: { default: 'status' },
        title: '장례 단계',
        minWidth: 130,
      },
    ],
    emptyText: '고인 현황이 없습니다.',
    height: 'auto',
    // 전량 조회다. 이걸 빼면 vxe 가 응답을 `{ result, page }` 로 읽어 한 줄도 안 나온다.
    pagerConfig: { enabled: false },
    proxyConfig: {
      ajax: {
        query: async () => {
          try {
            const data = await getDeceasedList();
            deceasedList.value = data || [];
          } catch {
            message.error('고인 현황 로드 실패');
            deceasedList.value = [];
          }
          return deceasedList.value;
        },
      },
    },
    rowConfig: { keyField: 'id' },
  },
});
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <!-- 장례 진행 단계 현황 카드 -->
    <Row :gutter="16" class="mb-4">
      <Col :span="8">
        <Card class="bg-blue-50/50">
          <div class="text-xs text-muted-foreground font-semibold">장례 진행중</div>
          <div class="text-3xl font-extrabold text-blue-600 mt-2">{{ stats.inHospital }}명</div>
        </Card>
      </Col>
      <Col :span="8">
        <Card class="bg-orange-50/50">
          <div class="text-xs text-muted-foreground font-semibold">발인 완료</div>
          <div class="text-3xl font-extrabold text-orange-600 mt-2">{{ stats.discharged }}명</div>
        </Card>
      </Col>
      <Col :span="8">
        <Card class="bg-gray-50/50">
          <div class="text-xs text-muted-foreground font-semibold">장례 정산 완료</div>
          <div class="text-3xl font-extrabold text-gray-600 mt-2">{{ stats.completed }}명</div>
        </Card>
      </Col>
    </Row>

    <!-- 고인 현황 그리드 리스트 -->
    <Grid table-title="실시간 고인 이송 및 진행 현황">
      <template #gender="{ row }">
        <span>{{ row.gender === 'MALE' ? '남성' : '여성' }}</span>
      </template>

      <template #status="{ row }">
        <Badge
          v-if="row.status === 'FUNERAL_IN_PROGRESS'"
          status="processing"
          text="장례 진행중"
        />
        <Badge
          v-else-if="row.status === 'FUNERAL_DEPARTURE_COMPLETED'"
          status="warning"
          text="출상 완료"
        />
        <Badge v-else status="default" text="장례 종료" />
      </template>

      <!-- 값이 비면 빈칸 대신 '-' 를 보여 준다(원본과 같다). 여러 칸이 함께 쓴다. -->
      <template #dash="{ column, row }">
        <span v-if="row[column.field]">{{ row[column.field] }}</span>
        <span v-else class="text-muted-foreground">-</span>
      </template>
    </Grid>
  </Page>
</template>
