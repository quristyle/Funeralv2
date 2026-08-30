<script lang="ts" setup>
import type { LifeBirthdayApi } from '#/api/life/birthday';

import { onMounted, ref, watch } from 'vue';

import { IconifyIcon } from '@vben/icons';

import { Avatar, Button, Card, Skeleton } from 'ant-design-vue';
import dayjs from 'dayjs';

import { getTodayBirthdays } from '#/api/life/birthday';

/**
 * [오늘의 생일자 위젯]
 *
 * 원본(GHUB TodayBirthdayWidget.vue)을 ant-design-vue 로 옮겼다.
 * 생일 정본이 포털 계정으로 옮겨지며 썸네일은 없다 — 이니셜 아바타만 그린다.
 * 올해 받은 메시지 수를 함께 보여 주고, '축하' 버튼은 부모에게 올린다
 * (메시지 팝업은 부모 화면이 하나만 들고 있다).
 */

const props = defineProps<{
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

async function reload() {
  loading.value = true;
  try {
    people.value =
      (await getTodayBirthdays(
        props.companyId || undefined,
        props.departmentId || undefined,
      )) ?? [];
  } finally {
    loading.value = false;
  }
}

watch(() => [props.companyId, props.departmentId], reload);
onMounted(reload);

defineExpose({ reload });
</script>

<template>
  <Card :body-style="{ padding: '12px' }" size="small">
    <template #title>
      <div class="flex items-center justify-between">
        <span class="flex items-center gap-2 text-base font-bold">
          <span class="h-4 w-1 rounded-full bg-pink-500"></span>
          오늘의 생일자
        </span>
        <span class="text-right leading-tight">
          <span class="block text-xs font-bold text-pink-500">
            {{ people.length }}명
          </span>
          <span class="block text-[10px] font-normal text-muted-foreground">
            {{ dayjs().format('MM.DD (ddd)') }}
          </span>
        </span>
      </div>
    </template>

    <Skeleton v-if="loading" :paragraph="{ rows: 2 }" active />

    <div
      v-else-if="people.length > 0"
      class="max-h-[200px] space-y-2 overflow-y-auto pr-1"
    >
      <div
        v-for="user in people"
        :key="user.id"
        class="flex items-center gap-3 rounded p-2 transition-colors hover:bg-accent"
      >
        <Avatar :size="40" class="shrink-0">
          {{ user.name?.charAt(0) }}
        </Avatar>

        <div class="min-w-0 flex-1">
          <div class="flex items-center gap-1.5">
            <p class="truncate text-sm font-bold">{{ user.name }}</p>
            <!-- 올해 받은 메시지 수 -->
            <span
              v-if="(user.messageCount ?? 0) > 0"
              class="flex shrink-0 items-center gap-0.5 rounded-full border border-pink-200 bg-pink-50 px-1.5 py-0.5 dark:border-pink-800 dark:bg-pink-900/20"
              title="올해 받은 축하 메시지"
            >
              <IconifyIcon class="size-3 text-pink-500" icon="lucide:gift" />
              <span class="text-[10px] font-bold text-pink-600 dark:text-pink-400">
                {{ user.messageCount }}
              </span>
            </span>
          </div>
          <p class="truncate text-[11px] text-muted-foreground">
            {{ user.departmentName || user.companyName || '임직원' }}
          </p>
        </div>

        <Button v-perm:create size="small" type="primary" @click="emit('send', user)">
          축하
        </Button>
      </div>
    </div>

    <div v-else class="py-6 text-center text-sm text-muted-foreground">
      오늘 생일인 임직원이 없습니다.
    </div>
  </Card>
</template>
