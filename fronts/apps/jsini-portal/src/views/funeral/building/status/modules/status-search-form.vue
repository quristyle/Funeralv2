<script lang="ts" setup>
/**
 * 빈소현황 필터.
 *
 * 예전에는 회사·건물·층·고인·입실·발인 여섯 칸이 늘 펼쳐져 세로 96px 을 먹었다.
 * 회사와 시설은 한 번 고르면 잘 바뀌지 않으므로 **한 줄로 접고**, 기간 두 칸은
 * '상세 필터' 뒤로 보낸다 (준수사항 4).
 *
 * 시설은 **여럿 고를 수 있다.** 전에는 '전체' 아니면 한 곳뿐이라 다섯 곳 중
 * 둘만 보는 것이 불가능했다. 목록은 회사로 묶어 그린다 — 여러 운영사를 한
 * 계정이 보면 같은 이름의 건물("본관")이 섞이기 때문이다.
 */
import type { FacilityGroup } from '../composables/use-status-data';

import { computed, ref } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { Button, Input, RangePicker, Select, Tooltip } from 'ant-design-vue';

import BizSelect from '#/components/BizSelect.vue';

const props = defineProps<{
  modelValue: {
    companyId: string;
    floorId: string;
    name: string;
  };
  selectedFacilityIds: string[];
  /** 고를 수 있는 시설 전량 (선택으로 걸러지기 전) */
  facilities: FacilityGroup[];
  roomEnterDates: any;
  funeralDates: any;
}>();

const emit = defineEmits<{
  (e: 'update:modelValue', val: typeof props.modelValue): void;
  (e: 'update:selectedFacilityIds', val: string[]): void;
  (e: 'update:roomEnterDates', val: any): void;
  (e: 'update:funeralDates', val: any): void;
  (e: 'search'): void;
  (e: 'reset'): void;
}>();

const showDetail = ref(false);

/** 시설 목록을 회사로 묶은 것 — 셀렉트의 그룹 머리글이 된다. */
const facilityGroups = computed(() => {
  const map = new Map<string, { name: string; items: FacilityGroup[] }>();
  for (const f of props.facilities) {
    const key = f.companyId || '_';
    if (!map.has(key)) map.set(key, { name: f.companyName || '회사 미지정', items: [] });
    map.get(key)!.items.push(f);
  }
  return [...map.values()];
});

/** 회사가 하나뿐이면 그룹 머리글이 군더더기다. */
const showGroupLabel = computed(() => facilityGroups.value.length > 1);

const hasDetailFilter = computed(
  () => !!props.roomEnterDates || !!props.funeralDates || !!props.modelValue.floorId,
);
</script>

<template>
  <div class="mb-2 rounded-lg border border-border bg-card px-3 py-2">
    <!-- 늘 보이는 한 줄 — 회사 · 시설 · 고인명 -->
    <div class="flex flex-wrap items-center gap-2">
      <BizSelect
        :value="modelValue.companyId"
        type="funeralCompany"
        placeholder="회사 전체"
        show-all
        class="w-32 sm:w-44"
        size="small"
        @update:value="(val) => emit('update:modelValue', { ...modelValue, companyId: val as string })"
        @change="emit('search')"
      />

      <Select
        :value="selectedFacilityIds"
        mode="multiple"
        placeholder="시설 전체"
        size="small"
        class="min-w-[140px] flex-1 sm:min-w-[200px] sm:max-w-[420px]"
        :max-tag-count="3"
        allow-clear
        option-filter-prop="label"
        @update:value="(val) => emit('update:selectedFacilityIds', (val as string[]) ?? [])"
      >
        <template v-if="showGroupLabel">
          <Select.OptGroup v-for="g in facilityGroups" :key="g.name" :label="g.name">
            <Select.Option v-for="f in g.items" :key="f.id" :value="f.id" :label="f.name">
              {{ f.name }}
              <span class="text-muted-foreground">· {{ f.summary.using }}/{{ f.summary.total }}</span>
            </Select.Option>
          </Select.OptGroup>
        </template>
        <template v-else>
          <Select.Option
            v-for="f in facilities"
            :key="f.id"
            :value="f.id"
            :label="f.name"
          >
            {{ f.name }}
            <span class="text-muted-foreground">· {{ f.summary.using }}/{{ f.summary.total }}</span>
          </Select.Option>
        </template>
      </Select>

      <Input
        :value="modelValue.name"
        placeholder="고인명"
        allow-clear
        size="small"
        class="w-24 sm:w-32"
        @input="(e) => emit('update:modelValue', { ...modelValue, name: (e.target as HTMLInputElement).value })"
        @press-enter="emit('search')"
      />

      <Button size="small" type="primary" @click="emit('search')">검색</Button>

      <Tooltip :title="hasDetailFilter ? '상세 필터가 걸려 있습니다' : '층 · 기간으로 더 좁히기'">
        <Button
          size="small"
          :type="hasDetailFilter ? 'default' : 'text'"
          :danger="hasDetailFilter"
          @click="showDetail = !showDetail"
        >
          <span class="flex items-center gap-1">
            <IconifyIcon icon="lucide:sliders-horizontal" class="size-3.5" />
            상세
            <IconifyIcon
              :icon="showDetail ? 'lucide:chevron-up' : 'lucide:chevron-down'"
              class="size-3"
            />
          </span>
        </Button>
      </Tooltip>

      <Button size="small" type="text" @click="emit('reset')">초기화</Button>
    </div>

    <!-- 상세 필터 — 층 · 입실 · 발인. 접혀 있는 것이 기본이다. -->
    <div
      v-if="showDetail"
      class="mt-2 flex flex-wrap items-center gap-2 border-t border-border/60 pt-2"
    >
      <span class="text-xs text-muted-foreground">층</span>
      <BizSelect
        :value="modelValue.floorId"
        type="floor"
        :params="{ buildingId: selectedFacilityIds.length === 1 ? selectedFacilityIds[0] : '' }"
        placeholder="층 전체"
        show-all
        class="w-32"
        size="small"
        @update:value="(val) => emit('update:modelValue', { ...modelValue, floorId: val as string })"
        @change="emit('search')"
      />

      <span class="ml-2 text-xs text-muted-foreground">입실</span>
      <RangePicker
        :value="roomEnterDates"
        size="small"
        class="w-full sm:w-56"
        @change="(val) => emit('update:roomEnterDates', val)"
      />

      <span class="ml-2 text-xs text-muted-foreground">발인</span>
      <RangePicker
        :value="funeralDates"
        size="small"
        class="w-full sm:w-56"
        @change="(val) => emit('update:funeralDates', val)"
      />

      <Button size="small" type="primary" class="ml-2" @click="emit('search')">적용</Button>
    </div>
  </div>
</template>
