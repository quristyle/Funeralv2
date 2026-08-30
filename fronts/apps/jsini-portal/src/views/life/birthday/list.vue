<script lang="ts" setup>
import type { LifeBirthdayApi } from '#/api/life/birthday';

import { onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';

import { Page, useVbenModal } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import { Button, Card, Switch, Tag } from 'ant-design-vue';
import dayjs from 'dayjs';

import { getBirthdayStats, getTodayMessages } from '#/api/life/birthday';
import BizSelect from '#/components/BizSelect.vue';

import BirthdayMessageDialog from './modules/birthday-message-dialog.vue';
import MonthlyBirthdayWidget from './modules/monthly-birthday-widget.vue';
import TodayBirthdayWidget from './modules/today-birthday-widget.vue';

/**
 * [생일 축하 — 목록]
 *
 * 원본(GHUB BirthdayList.vue). 12개월 통계 타일 · 오늘 생일자 · 오늘의 축하 메시지 ·
 * 선택 월 생일자를 한 화면에 담는다.
 *
 * 생일 정본은 포털 계정(scom.accounts)이다 (A안) — 등록·수정 UI 는 없고,
 * [계정 관리] 화면에서 한다. 소속 필터는 회사 → 부서 2단(BizSelect)이다.
 */

const route = useRoute();

const selectedMonth = ref(new Date().getMonth() + 1);
const selectedCompanyId = ref<string>('');
const selectedDepartmentId = ref<string>('');
const loading = ref(false);

const stats = ref<LifeBirthdayApi.MonthStat[]>([]);
const todayMessages = ref<LifeBirthdayApi.Message[]>([]);

const todayWidgetRef = ref<InstanceType<typeof TodayBirthdayWidget> | null>(null);
const monthlyWidgetRef = ref<InstanceType<typeof MonthlyBirthdayWidget> | null>(null);

// ── 통계 · 오늘의 메시지 ─────────────────────────────────

async function loadStats() {
  const rows = await getBirthdayStats(
    selectedCompanyId.value || undefined,
    selectedDepartmentId.value || undefined,
  );
  stats.value =
    rows && rows.length > 0
      ? rows
      : Array.from({ length: 12 }, (_, i) => ({
          month: i + 1,
          total: 0,
          solar: 0,
          lunar: 0,
        }));
}

async function loadTodayMessages() {
  todayMessages.value = (await getTodayMessages()) ?? [];
}

/** 전체 리로드 — 통계 · 오늘의 메시지 · 두 위젯 */
async function refreshAll() {
  loading.value = true;
  try {
    await Promise.all([
      loadStats(),
      loadTodayMessages(),
      todayWidgetRef.value?.reload(),
      monthlyWidgetRef.value?.reload(),
    ]);
  } finally {
    loading.value = false;
  }
}

// ── 소속 필터 (회사 → 부서) ──────────────────────────────
// 회사를 바꾸면 부서 선택을 초기화한다. 부서 BizSelect 는 companyId 가
// 비어 있는 동안 조회하지 않는다 (BizSelect 의 dept 대기 규칙).

function onCompanyChange() {
  selectedDepartmentId.value = '';
  onFilterChange();
}

/** 필터 변경 — 통계는 여기서, 두 위젯은 props 변화로 스스로 다시 불러온다 */
async function onFilterChange() {
  loading.value = true;
  try {
    await loadStats();
  } finally {
    loading.value = false;
  }
}

// ── 자동 갱신 (5분) — 원본과 동일 ────────────────────────

const autoRefresh = ref(true);
let refreshInterval: null | ReturnType<typeof setInterval> = null;

function startRefreshTimer() {
  stopRefreshTimer();
  refreshInterval = setInterval(refreshAll, 5 * 60 * 1000);
}

function stopRefreshTimer() {
  if (refreshInterval) {
    clearInterval(refreshInterval);
    refreshInterval = null;
  }
}

watch(autoRefresh, (enabled) => (enabled ? startRefreshTimer() : stopRefreshTimer()));
onUnmounted(stopRefreshTimer);

// ── 팝업 (메시지) — 화면에 하나만 둔다 ───────────────────

const [MessageModal, messageModalApi] = useVbenModal({
  connectedComponent: BirthdayMessageDialog,
  destroyOnClose: true,
});

function onSend(person: LifeBirthdayApi.Person) {
  messageModalApi.setData({ ...person }).open();
}

onMounted(async () => {
  if (autoRefresh.value) startRefreshTimer();
  // 라우트 쿼리에 월이 있으면 초기 선택월로 (원본과 동일)
  const m = Number.parseInt(String(route.query.month ?? ''), 10);
  if (!Number.isNaN(m) && m >= 1 && m <= 12) selectedMonth.value = m;

  loading.value = true;
  try {
    await Promise.all([loadStats(), loadTodayMessages()]);
  } finally {
    loading.value = false;
  }
});
</script>

<template>
  <Page auto-content-height>
    <!-- 상단: 제목 + 소속 필터(회사 → 부서) + 새로고침 -->
    <Card :body-style="{ padding: '10px 14px' }" class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <span class="flex items-center gap-2 text-base font-bold">
          <IconifyIcon class="size-5 text-pink-500" icon="lucide:gift" />
          생일 축하
        </span>
        <div class="flex flex-wrap items-center gap-2">
          <span class="text-xs text-muted-foreground">
            생일 등록·수정은 [계정 관리] 화면에서 합니다
          </span>
          <BizSelect
            v-model:value="selectedCompanyId"
            :show-all="true"
            placeholder="회사 전체"
            style="width: 160px"
            type="company"
            @change="onCompanyChange"
          />
          <BizSelect
            v-model:value="selectedDepartmentId"
            :params="{ companyId: selectedCompanyId }"
            :show-all="true"
            placeholder="부서 전체"
            style="width: 160px"
            type="dept"
            @change="onFilterChange"
          />
          <Button :loading="loading" @click="refreshAll">
            <IconifyIcon class="size-4" icon="lucide:refresh-cw" />
          </Button>
        </div>
      </div>
    </Card>

    <!-- 12개월 통계 타일 (클릭으로 월 선택) -->
    <div class="mb-3 grid grid-cols-6 gap-2 md:grid-cols-12">
      <button
        v-for="stat in stats"
        :key="stat.month"
        class="flex flex-col items-center justify-center rounded border py-2 transition-all"
        :class="
          selectedMonth === stat.month
            ? 'border-transparent bg-primary text-primary-foreground shadow ring-2 ring-primary/30'
            : 'border-border bg-card hover:border-primary/50'
        "
        type="button"
        @click="selectedMonth = stat.month"
      >
        <span
          class="mb-0.5 text-[10px] font-black"
          :class="
            selectedMonth === stat.month
              ? 'text-primary-foreground/80'
              : 'text-muted-foreground'
          "
        >
          {{ stat.month }}월
        </span>
        <span class="flex items-baseline gap-0.5">
          <span class="text-lg font-black leading-none">{{ stat.total }}</span>
          <span
            class="text-[9px] font-bold"
            :class="
              selectedMonth === stat.month
                ? 'text-primary-foreground/80'
                : 'text-muted-foreground'
            "
          >
            명
          </span>
        </span>
      </button>
    </div>

    <div class="flex flex-col gap-3 lg:flex-row">
      <!-- 왼쪽: 오늘 생일자 + 오늘의 축하 메시지 -->
      <div class="flex min-w-0 flex-1 flex-col gap-3">
        <TodayBirthdayWidget
          ref="todayWidgetRef"
          :company-id="selectedCompanyId"
          :department-id="selectedDepartmentId"
          @send="onSend"
        />

        <Card
          v-if="todayMessages.length > 0"
          :body-style="{ padding: '12px' }"
          size="small"
        >
          <template #title>
            <div class="flex items-center justify-between">
              <span class="flex items-center gap-2 text-base font-bold">
                <span class="h-4 w-1 rounded-full bg-pink-500"></span>
                오늘의 축하 메시지
              </span>
              <span class="flex items-center gap-2 font-normal">
                <span class="text-[10px] font-bold text-muted-foreground">
                  자동 갱신
                </span>
                <Switch v-model:checked="autoRefresh" size="small" />
                <Tag color="pink">{{ todayMessages.length }}개</Tag>
              </span>
            </div>
          </template>

          <div class="max-h-[360px] space-y-3 overflow-y-auto pr-1">
            <div
              v-for="msg in todayMessages"
              :key="msg.id"
              class="rounded-lg border border-transparent bg-pink-50/40 p-3 transition-colors hover:border-pink-200 dark:bg-pink-900/10 dark:hover:border-pink-800"
            >
              <div class="mb-1.5 flex items-start justify-between">
                <div class="flex flex-col">
                  <span class="text-xs font-black text-pink-600 dark:text-pink-400">
                    To. {{ msg.recipientName }}
                  </span>
                  <span class="text-[10px] text-muted-foreground">
                    From. {{ msg.senderName }}
                    <template v-if="msg.senderDepartment">
                      ({{ msg.senderDepartment }})
                    </template>
                  </span>
                </div>
                <span class="text-[10px] text-muted-foreground">
                  {{ dayjs(msg.createdAt).format('HH:mm') }}
                </span>
              </div>
              <p class="whitespace-pre-wrap text-sm leading-relaxed">
                {{ msg.content }}
              </p>
            </div>
          </div>
        </Card>
      </div>

      <!-- 오른쪽: 선택 월 생일자 -->
      <div class="min-w-0 flex-1">
        <MonthlyBirthdayWidget
          ref="monthlyWidgetRef"
          :company-id="selectedCompanyId"
          :department-id="selectedDepartmentId"
          :target-month="selectedMonth"
          @send="onSend"
        />
      </div>
    </div>

    <!-- 축하 메시지 보내기 팝업 -->
    <MessageModal @sent="refreshAll" />
  </Page>
</template>
