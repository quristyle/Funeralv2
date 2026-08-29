<script lang="ts" setup>
import type { LifeBirthdayApi } from '#/api/life/birthday';

import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import {
  Avatar,
  Badge,
  Card,
  Collapse,
  CollapsePanel,
  Empty,
  Skeleton,
  TabPane,
  Tabs,
} from 'ant-design-vue';
import dayjs from 'dayjs';

import { getMyMessages, getSentMessages } from '#/api/life/birthday';

/**
 * [내 생일 메시지함]
 *
 * 원본(GHUB MyBirthdayMessages.vue). 받은/보낸 메시지를 탭 둘로 나누고,
 * 연도별 그룹 아코디언(Collapse)으로 보여 준다. 최신 연도는 펼쳐 둔다.
 */

const activeTab = ref<'inbox' | 'sent'>('inbox');
const loading = ref(false);

const received = ref<LifeBirthdayApi.Message[]>([]);
const sent = ref<LifeBirthdayApi.Message[]>([]);

const activeReceivedYears = ref<string[]>([]);
const activeSentYears = ref<string[]>([]);

/** 연도별 그룹 (내림차순). 서버 시각은 UTC ISO — dayjs 로 현지 연도를 쓴다. */
function groupByYear(list: LifeBirthdayApi.Message[]) {
  const groups: Record<string, LifeBirthdayApi.Message[]> = {};
  list.forEach((msg) => {
    const year = dayjs(msg.createdAt).year().toString();
    (groups[year] ??= []).push(msg);
  });
  return Object.entries(groups).sort((a, b) => Number(b[0]) - Number(a[0]));
}

const groupedReceived = computed(() => groupByYear(received.value));
const groupedSent = computed(() => groupByYear(sent.value));

function formatDate(value: string) {
  return dayjs(value).format('YYYY.MM.DD');
}

async function loadData() {
  loading.value = true;
  try {
    const [inbox, outbox] = await Promise.all([getMyMessages(), getSentMessages()]);
    received.value = inbox ?? [];
    sent.value = outbox ?? [];

    // 최신 연도 그룹은 펼쳐 둔다 (원본과 동일)
    activeReceivedYears.value = groupedReceived.value[0]
      ? [groupedReceived.value[0][0]]
      : [];
    activeSentYears.value = groupedSent.value[0] ? [groupedSent.value[0][0]] : [];
  } finally {
    loading.value = false;
  }
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <Card :body-style="{ padding: '12px 16px' }" size="small">
      <!-- 머리말 -->
      <div class="mb-2 flex items-center gap-3">
        <div class="rounded bg-pink-50 p-2 text-pink-500 dark:bg-pink-900/20">
          <IconifyIcon class="size-6" icon="lucide:gift" />
        </div>
        <div>
          <h1 class="text-lg font-black leading-tight">메시지함</h1>
          <p class="text-xs text-muted-foreground">
            따뜻한 생일 축하 소식을 한곳에서 확인하세요.
          </p>
        </div>
      </div>

      <Tabs v-model:active-key="activeTab">
        <!-- 받은 메시지 -->
        <TabPane key="inbox">
          <template #tab>
            <span class="flex items-center gap-2 px-1 font-bold">
              받은 메시지
              <Badge
                v-if="received.length > 0"
                :count="received.length"
                :overflow-count="999"
              />
            </span>
          </template>

          <Skeleton v-if="loading" :paragraph="{ rows: 5 }" active class="py-6" />

          <div
            v-else-if="groupedReceived.length > 0"
            class="max-h-[calc(100vh-260px)] overflow-y-auto pr-1 pt-2"
          >
            <Collapse v-model:active-key="activeReceivedYears" ghost>
              <CollapsePanel v-for="[year, msgs] in groupedReceived" :key="year">
                <template #header>
                  <span class="flex items-center gap-2 text-base font-black">
                    <span class="h-4 w-1 rounded-full bg-pink-500"></span>
                    {{ year }}년
                    <span class="text-xs font-normal text-muted-foreground">
                      ({{ msgs.length }})
                    </span>
                  </span>
                </template>

                <div class="grid gap-3">
                  <div
                    v-for="msg in msgs"
                    :key="msg.id"
                    class="rounded border border-border p-4 shadow-sm transition-colors hover:border-pink-300 dark:hover:border-pink-800"
                  >
                    <div class="flex items-start gap-3">
                      <Avatar :size="40" class="shrink-0">
                        {{ msg.senderName?.charAt(0) }}
                      </Avatar>
                      <div class="min-w-0 flex-1">
                        <div
                          class="mb-2 flex flex-col justify-between gap-1 sm:flex-row sm:items-center"
                        >
                          <div class="flex items-baseline gap-2">
                            <span class="text-sm font-black">
                              {{ msg.senderName }}
                            </span>
                            <span
                              v-if="msg.senderDepartment"
                              class="text-[10px] font-bold text-muted-foreground"
                            >
                              {{ msg.senderDepartment }}
                            </span>
                          </div>
                          <span class="text-[10px] text-muted-foreground">
                            {{ formatDate(msg.createdAt) }}
                          </span>
                        </div>
                        <p class="whitespace-pre-wrap text-sm leading-relaxed">
                          {{ msg.content }}
                        </p>
                      </div>
                    </div>
                  </div>
                </div>
              </CollapsePanel>
            </Collapse>
          </div>

          <Empty
            v-else
            class="py-16"
            description="아직 받은 메시지가 없습니다."
          />
        </TabPane>

        <!-- 보낸 메시지 -->
        <TabPane key="sent">
          <template #tab>
            <span class="flex items-center gap-2 px-1 font-bold">
              보낸 메시지
              <Badge
                v-if="sent.length > 0"
                :count="sent.length"
                :number-style="{ backgroundColor: '#3b82f6' }"
                :overflow-count="999"
              />
            </span>
          </template>

          <Skeleton v-if="loading" :paragraph="{ rows: 5 }" active class="py-6" />

          <div
            v-else-if="groupedSent.length > 0"
            class="max-h-[calc(100vh-260px)] overflow-y-auto pr-1 pt-2"
          >
            <Collapse v-model:active-key="activeSentYears" ghost>
              <CollapsePanel v-for="[year, msgs] in groupedSent" :key="year">
                <template #header>
                  <span class="flex items-center gap-2 text-base font-black">
                    <span class="h-4 w-1 rounded-full bg-blue-500"></span>
                    {{ year }}년
                    <span class="text-xs font-normal text-muted-foreground">
                      ({{ msgs.length }})
                    </span>
                  </span>
                </template>

                <div class="grid gap-3">
                  <div
                    v-for="msg in msgs"
                    :key="msg.id"
                    class="rounded border border-border p-4 shadow-sm transition-colors hover:border-blue-300 dark:hover:border-blue-800"
                  >
                    <div class="flex items-start gap-3">
                      <div
                        class="flex size-10 shrink-0 items-center justify-center rounded bg-blue-50 text-blue-500 dark:bg-blue-900/20"
                      >
                        <IconifyIcon class="size-5" icon="lucide:send" />
                      </div>
                      <div class="min-w-0 flex-1">
                        <div
                          class="mb-2 flex flex-col justify-between gap-1 sm:flex-row sm:items-center"
                        >
                          <div class="flex items-baseline gap-2">
                            <span class="text-xs font-black text-muted-foreground">
                              To.
                            </span>
                            <span class="text-sm font-black">
                              {{ msg.recipientName }}
                            </span>
                            <span
                              v-if="msg.recipientDepartment"
                              class="truncate text-[10px] font-bold text-muted-foreground"
                            >
                              {{ msg.recipientDepartment }}
                            </span>
                          </div>
                          <span class="text-[10px] text-muted-foreground">
                            {{ formatDate(msg.createdAt) }}
                          </span>
                        </div>
                        <p class="whitespace-pre-wrap text-sm leading-relaxed">
                          {{ msg.content }}
                        </p>
                      </div>
                    </div>
                  </div>
                </div>
              </CollapsePanel>
            </Collapse>
          </div>

          <Empty v-else class="py-16" description="보낸 메시지가 없습니다." />
        </TabPane>
      </Tabs>
    </Card>
  </Page>
</template>
