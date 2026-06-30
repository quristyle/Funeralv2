<script lang="ts" setup>
import { Page } from '@vben/common-ui';
import { Empty, Spin } from 'ant-design-vue';
import { useStatusData } from './composables/use-status-data';
import StatusSearchForm from './modules/status-search-form.vue';
import BuildingSection from './modules/building-section.vue';

const {
  searchForm,
  roomEnterDates,
  funeralDates,
  loading,
  hasLoaded,
  collapsedBuildings,
  toggleBuilding,
  onSearch,
  onReset,
  filteredBuildings,
  getRoomsByBuilding,
  getBuildingSummary,
  devices,
} = useStatusData();
</script>

<template>
  <Page auto-content-height>
    <!-- ── 상단 검색 필터 바 (Horizontal) ───────────────────────────── -->
    <StatusSearchForm
      v-model="searchForm"
      v-model:room-enter-dates="roomEnterDates"
      v-model:funeral-dates="funeralDates"
      @search="onSearch"
      @reset="onReset"
    />

    <!-- ── 빈소 현황 대시보드 콘텐츠 영역 ────────────────────────────── -->
    <div class="flex-1 overflow-auto bg-background/50 rounded-lg p-2">
      <div v-if="loading" class="flex h-96 items-center justify-center">
        <Spin size="large" tip="빈소 현황 데이터를 조회 중입니다..." />
      </div>

      <div v-else-if="!hasLoaded" class="flex h-96 items-center justify-center">
        <Empty description="회사 필터를 설정하고 검색 버튼을 클릭하여 현황 조회를 시작해주세요." />
      </div>

      <div v-else-if="filteredBuildings.length === 0" class="flex h-96 items-center justify-center">
        <Empty description="조회 가능한 건물 정보가 존재하지 않습니다." />
      </div>

      <div v-else class="space-y-8">
        <!-- 건물별 루프 섹션 -->
        <BuildingSection
          v-for="building in filteredBuildings"
          :key="building.id"
          :building="building"
          :rooms="getRoomsByBuilding(building.id)"
          :devices="devices"
          :collapsed="!!collapsedBuildings[building.id]"
          :summary="getBuildingSummary(building.id)"
          @toggle="toggleBuilding(building.id)"
        />
      </div>
    </div>
  </Page>
</template>
