<script lang="ts" setup>
import type { LifeBirthdayApi } from '#/api/life/birthday';

import { computed, onMounted, ref, watch } from 'vue';

import { Avatar, Button, Card, Empty, Spin, Tag } from 'ant-design-vue';
import dayjs from 'dayjs';

import { getBirthdayList } from '#/api/life/birthday';

/**
 * [선택 월 생일자 위젯]
 *
 * 원본(GHUB MonthlyBirthdayWidget.vue)을 ant-design-vue 로 옮겼다.
 * 발생일(occurrenceDate — 음력이면 양력 환산) 기준으로 날짜별 그룹을 만든다.
 * '축하'는 부모에게 올린다 (팝업은 부모 화면이 하나만 들고 있다).
 * 생일 수정은 여기서 하지 않는다 — [계정 관리] 화면에서 한다 (A안).
 */

const props = defineProps<{
  /** 보여줄 월 (1~12) */
  targetMonth: number;
  /** 회사 필터 (없으면 전체) */
  companyId?: string;
  /** 부서 필터 (없으면 전체) */
  departmentId?: string;
}>();

const emit = defineEmits<{
  (e: 'send', person: LifeBirthdayApi.Person): void;
}>();

const loading = ref(false);
const people = ref<LifeBirthdayApi.Person[]>([]);

/** 발생일별 그룹. 서버 시각이 UTC ISO 여도 dayjs 가 현지 날짜로 잘라 준다. */
const grouped = computed(() => {
  const groups: Record<string, LifeBirthdayApi.Person[]> = {};
  people.value.forEach((b) => {
    const d = dayjs(b.occurrenceDate || b.birthDate);
    const key = d.isValid() ? d.format('YYYY-MM-DD') : String(b.birthDate);
    (groups[key] ??= []).push(b);
  });
  return Object.keys(groups)
    .sort()
    .map((date) => ({
      date,
      day: dayjs(date).format('DD'),
      dayName: dayjs(date).format('ddd'),
      members: groups[date]!,
    }));
});

/** 부서를 보여 주고, 있으면 회사를 보조로 붙인다 */
function belongLabel(b: LifeBirthdayApi.Person) {
  if (b.departmentName && b.companyName) {
    return `${b.departmentName} · ${b.companyName}`;
  }
  return b.departmentName || b.companyName || '임직원';
}

async function reload() {
  loading.value = true;
  try {
    people.value =
      (await getBirthdayList(
        props.targetMonth,
        props.companyId || undefined,
        props.departmentId || undefined,
      )) ?? [];
  } finally {
    loading.value = false;
  }
}

watch(() => [props.targetMonth, props.companyId, props.departmentId], reload);
onMounted(reload);

defineExpose({ reload });
</script>

<template>
  <Card :body-style="{ padding: '12px' }" size="small">
    <template #title>
      <div class="flex items-center justify-between">
        <span class="flex items-center gap-2 text-base font-bold">
          <span class="h-4 w-1 rounded-full bg-blue-500"></span>
          {{ targetMonth }}월 생일자
        </span>
        <Tag color="blue">{{ people.length }}명</Tag>
      </div>
    </template>

    <Spin :spinning="loading">
      <div
        v-if="grouped.length > 0"
        class="max-h-[500px] space-y-1 overflow-y-auto pr-1"
      >
        <div
          v-for="group in grouped"
          :key="group.date"
          class="flex items-start gap-3 rounded border border-transparent p-1.5 transition-colors hover:border-border hover:bg-accent/50"
        >
          <!-- 날짜 상자 (왼쪽) -->
          <div
            class="flex h-12 w-12 shrink-0 flex-col items-center justify-center rounded border border-blue-100 text-blue-600 dark:border-blue-800/50 dark:text-blue-400"
          >
            <div class="text-lg font-black leading-none">{{ group.day }}</div>
            <div class="mt-0.5 text-[9px] font-bold opacity-70">
              {{ group.dayName }}
            </div>
          </div>

          <!-- 인원 (오른쪽) -->
          <div class="flex flex-1 flex-wrap gap-2">
            <div
              v-for="b in group.members"
              :key="b.id"
              class="flex min-w-[150px] items-center gap-2 rounded border border-border px-2.5 py-1.5 shadow-sm"
            >
              <Avatar :size="30" class="shrink-0">
                {{ b.name?.charAt(0) }}
              </Avatar>
              <div class="flex min-w-0 flex-1 flex-col">
                <div class="flex items-center gap-1 text-sm font-bold">
                  <span class="truncate">{{ b.name }}</span>
                  <Tag
                    v-if="b.isLunar"
                    class="!mr-0 shrink-0 !px-1 !text-[9px] !leading-4"
                    color="orange"
                  >
                    음력
                  </Tag>
                </div>
                <div class="truncate text-[10px] text-muted-foreground">
                  {{ belongLabel(b) }}
                </div>
              </div>

              <!-- 축하 -->
              <Button
                v-perm:create
                class="!h-auto shrink-0 !p-0 !text-[11px] font-bold"
                size="small"
                type="link"
                @click="emit('send', b)"
              >
                축하
              </Button>
            </div>
          </div>
        </div>
      </div>

      <Empty
        v-else
        :description="`${targetMonth}월 생일자가 없습니다.`"
        :image="Empty.PRESENTED_IMAGE_SIMPLE"
      />
    </Spin>
  </Card>
</template>
