<script lang="ts" setup>
import type { LifeBirthdayApi } from '#/api/life/birthday';

import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useRoute } from 'vue-router';

import { Page, useVbenModal } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import { Button, Card, Select, Switch, Tag } from 'ant-design-vue';
import dayjs from 'dayjs';

import { getBirthdayStats, getTodayMessages } from '#/api/life/birthday';

import BirthdayEditModal from './modules/birthday-edit-modal.vue';
import BirthdayMessageDialog from './modules/birthday-message-dialog.vue';
import MonthlyBirthdayWidget from './modules/monthly-birthday-widget.vue';
import TodayBirthdayWidget from './modules/today-birthday-widget.vue';

/**
 * [생일 축하 — 목록]
 *
 * 원본(GHUB BirthdayList.vue). 12개월 통계 타일 · 오늘 생일자 · 오늘의 축하 메시지 ·
 * 선택 월 생일자를 한 화면에 담는다.
 *
 * 원본과 다른 점:
 * - 소속 필터는 COMPANY_TYPE 공통코드 콤보였지만, 여기서는 이관 명단에서 실제로
 *   나온 companyCode 들로 셀렉트를 채운다 (위젯이 불러온 데이터에서 모은다).
 * - '달력으로 보기' 링크는 메뉴(백엔드 주도)가 담당하므로 뺐다.
 */

const route = useRoute();

const selectedMonth = ref(new Date().getMonth() + 1);
const selectedCompany = ref<string | undefined>();
const loading = ref(false);

const stats = ref<LifeBirthdayApi.MonthStat[]>([]);
const todayMessages = ref<LifeBirthdayApi.Message[]>([]);

const todayWidgetRef = ref<InstanceType<typeof TodayBirthdayWidget> | null>(null);
const monthlyWidgetRef = ref<InstanceType<typeof MonthlyBirthdayWidget> | null>(null);

// ── 소속 셀렉트: 위젯이 불러온 명단에서 companyCode 를 모은다 ──
const companyCodeSet = ref(new Set<string>());

function collectCompanyCodes(people: LifeBirthdayApi.Person[]) {
  let changed = false;
  people.forEach((p) => {
    if (p.companyCode && !companyCodeSet.value.has(p.companyCode)) {
      companyCodeSet.value.add(p.companyCode);
      changed = true;
    }
  });
  if (changed) companyCodeSet.value = new Set(companyCodeSet.value);
}

const companyOptions = computed(() =>
  [...companyCodeSet.value].sort().map((code) => ({ label: code, value: code })),
);

// ── 통계 · 오늘의 메시지 ─────────────────────────────────

async function loadStats() {
  const rows = await getBirthdayStats(selectedCompany.value || undefined);
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

/** 소속 필터 변경 — 위젯은 props 변화로 스스로 다시 불러온다 */
async function onCompanyChange() {
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

// ── 팝업 (수정 · 메시지) — 화면에 하나씩만 둔다 ──────────

const [EditModal, editModalApi] = useVbenModal({
  connectedComponent: BirthdayEditModal,
  destroyOnClose: true,
});

const [MessageModal, messageModalApi] = useVbenModal({
  connectedComponent: BirthdayMessageDialog,
  destroyOnClose: true,
});

function onEdit(person: LifeBirthdayApi.Person) {
  editModalApi.setData({ ...person }).open();
}

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
    <!-- 상단: 제목 + 소속 필터 + 새로고침 -->
    <Card :body-style="{ padding: '10px 14px' }" class="mb-3" size="small">
      <div class="flex flex-wrap items-center justify-between gap-2">
        <span class="flex items-center gap-2 text-base font-bold">
          <IconifyIcon class="size-5 text-pink-500" icon="lucide:gift" />
          생일 축하
        </span>
        <div class="flex items-center gap-2">
          <Select
            v-model:value="selectedCompany"
            :options="companyOptions"
            allow-clear
            placeholder="소속 전체"
            style="width: 160px"
            @change="onCompanyChange"
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
          :company-code="selectedCompany"
          @loaded="collectCompanyCodes"
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
          :company-code="selectedCompany"
          :target-month="selectedMonth"
          @edit="onEdit"
          @loaded="collectCompanyCodes"
          @send="onSend"
        />
      </div>
    </div>

    <!-- 생일 수정 팝업 -->
    <EditModal @success="refreshAll" />
    <!-- 축하 메시지 보내기 팝업 -->
    <MessageModal @sent="refreshAll" />
  </Page>
</template>
