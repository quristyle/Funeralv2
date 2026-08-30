<script setup lang="ts">
/**
 * [계정 정보]
 *
 * 사용자가 고칠 수 없는, 시스템이 남기는 기록만 모아 보여 준다.
 * 기본 설정 탭은 입력 폼이라 여기에 섞으면 "고칠 수 있는 값" 처럼 보인다.
 *
 * 두 곳에서 받는다.
 *  - `/auth/user/info`     계정 자체의 값 (가입일 · 비밀번호 · 마지막 접속)
 *  - `/auth/user/activity` 기록에서 계산해야 하는 값 (접속 이력 · 실패 · 횟수)
 *
 * [무엇을 보여 주나]
 * 사람이 자기 계정 화면에서 실제로 궁금해하는 것에 맞췄다.
 *   · 지금 이 접속이 나인가        → 최근 로그인 · 접속 IP · 기기
 *   · 지난번에는 언제 들어왔나      → 이전 접속
 *   · 누가 내 아이디를 두드렸나     → 최근 로그인 실패
 *   · 비밀번호를 언제 바꿔야 하나   → 만료 남은 기간
 * 뒤의 둘은 남의 접근을 알아채는 단서라서 눈에 띄게 둔다.
 *
 * [글자 크기]
 * 다른 프로필 탭은 vben 폼이라 `0.875rem`(=12.25px)로 그려진다. antd 의 Descriptions ·
 * 그리드는 자기 기준(14px)을 쓰기 때문에 그대로 두면 이 탭만 커 보인다.
 * 그래서 아래 스타일에서 그 기준을 폼 쪽에 맞춘다.
 *
 * ------------------------------------------------------------
 * [2026-08-30] 접속 기록 표를 ant-design-vue `<Table>` 에서 `useVbenVxeGrid` 로
 * 옮겼다. 정렬·필터는 공통 레이어(`adapter/vxe-grid-features.ts`)가 붙인다.
 *
 * **가져오기 방식은 그대로다** — `/auth/user/activity` 가 준 최근 10건을
 * `:table-data` 로 넘긴다. 이 화면은 `Page` 가 아니라 프로필 탭 안이라
 * `page-fill-last` 가 없다. 줄 수가 10 으로 정해져 있어 높이를 주지 않고
 * 내용만큼 그리게 둔다(원본 `<Table>` 과 같다).
 * ------------------------------------------------------------
 */
import type { AccountActivity, LoginLog } from '#/api';

import { computed, onMounted, ref } from 'vue';

import { formatDateTime } from '@vben/utils';

import {
  Alert,
  Descriptions,
  DescriptionsItem,
  Skeleton,
  Tag,
  Tooltip,
} from 'ant-design-vue';

import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { getAccountActivityApi, getUserInfoApi } from '#/api';

interface AccountInfo {
  createdAt?: null | string;
  lastLoginAt?: null | string;
  lastLoginIp?: null | string;
  passwordChangedAt?: null | string;
  passwordDaysRemaining?: null | number;
  passwordExpired?: boolean;
  passwordExpiresAt?: null | string;
  passwordExpiryDays?: null | number;
  /** 이관으로 만들어진 계정만 값이 있다 */
  msaSource?: null | string;
  roleNames?: null | string[];
  roles?: null | string[];
}

const info = ref<AccountInfo>({});
const activity = ref<AccountActivity | null>(null);
const loading = ref(true);

/** 값이 없을 때 빈칸으로 두면 '못 불러온 것' 과 구분되지 않는다. */
function dt(value?: null | string) {
  return value ? formatDateTime(value) : '기록 없음';
}

/** 만료 정책이 꺼져 있으면(설정 0) 남은 일수 칸 자체가 의미 없다. */
const policyOn = computed(() => (info.value.passwordExpiryDays ?? 0) > 0);

/** 남은 기간을 색으로도 구분한다 — 7일 이하면 눈에 띄어야 한다. */
const remainColor = computed(() => {
  const days = info.value.passwordDaysRemaining;
  if (info.value.passwordExpired || days === 0) return 'error';
  if (days !== null && days !== undefined && days <= 7) return 'warning';
  return 'success';
});

/** 역할은 이름으로 보여 준다. 식별자는 사람이 읽기 어렵다. */
const roleLabels = computed(() => {
  const names = info.value.roleNames ?? [];
  const ids = info.value.roles ?? [];
  return names.length > 0 ? names : ids;
});

/** 실패 이유를 사람 말로 바꾼다. */
function failLabel(reason?: null | string) {
  switch (reason) {
    case 'BAD_PASSWORD': {
      return '비밀번호 불일치';
    }
    case 'NOT_FOUND': {
      return '없는 아이디';
    }
    default: {
      return '실패';
    }
  }
}

/** 기기 원문은 길어서 표를 무너뜨린다. 줄인 이름을 쓰고 원문은 마우스를 올려 본다. */
function deviceLabel(row: LoginLog) {
  return row.device || (row.userAgent ? '알 수 없는 기기' : '기록 없음');
}

/** 그리드에 넘길 줄. 못 받았으면 빈 목록이다. */
const recent = computed<LoginLog[]>(() => activity.value?.recent ?? []);

const [LoginLogGrid] = useVbenVxeGrid({
  // `gridFeatures` 는 vxe 타입에 없다(공통 레이어가 읽고 떼어 낸다). 그래서 `as any`.
  gridOptions: {
    columns: [
      {
        field: 'at',
        // 화면에 보이는 것은 포맷한 시각이다. 필터가 훑을 글자를 그것으로 맞춘다.
        params: { filterText: (row: any) => dt(row.at) },
        slots: { default: 'at' },
        title: '시각',
        width: 150,
      },
      {
        align: 'center',
        field: 'success',
        // 값이 둘뿐인 칸이라 고르는 칸으로 준다.
        params: {
          filterOptions: [
            { label: '성공', value: true },
            { label: '실패', value: false },
          ],
        },
        slots: { default: 'success' },
        title: '결과',
        width: 110,
      },
      {
        field: 'ip',
        slots: { default: 'ip' },
        title: '접속 IP',
        width: 130,
      },
      {
        field: 'device',
        // 줄인 이름을 보여 주므로 필터도 그 글자를 훑는다.
        params: { filterText: (row: any) => deviceLabel(row as LoginLog) },
        minWidth: 200,
        slots: { default: 'device' },
        title: '기기',
      },
    ],
    emptyText: '접속 기록이 없습니다.',
    // 재조회 아이콘 — `:table-data` 라 그리드가 조회 방법을 모른다.
    // 계정 값과 접속 기록을 `load` 한 번이 함께 받아 온다.
    gridFeatures: { onRefresh: () => load() },
    // 최근 10건을 한 번에 넘긴다. 페이저를 두지 않는다.
    pagerConfig: { enabled: false },
    // 줄 수가 10 으로 정해져 있어 높이를 고정하지 않는다.
  } as any,
});

async function load() {
  try {
    // 둘은 서로를 기다릴 필요가 없다. 함께 부른다.
    // 활동 기록을 못 받아도 계정 값은 보여 준다 — 화면 전체가 비는 것보다 낫다.
    const [userInfo, act] = await Promise.all([
      getUserInfoApi(),
      getAccountActivityApi(10).catch(() => null),
    ]);
    info.value = userInfo as AccountInfo;
    activity.value = act;
  } finally {
    loading.value = false;
  }
}

onMounted(load);
</script>

<template>
  <Skeleton v-if="loading" active :paragraph="{ rows: 6 }" />

  <div v-else class="account-info flex flex-col gap-4">
    <Alert
      v-if="info.passwordExpired"
      description="'비밀번호 변경' 탭에서 새 비밀번호로 바꾸기 전까지 다른 기능을 이용할 수 없습니다."
      message="비밀번호 사용 기간이 지났습니다"
      show-icon
      type="error"
    />

    <!--
      최근 30일 안에 실패가 있으면 알린다. 접속 기록을 펼쳐 보지 않아도
      "누가 두드렸다" 는 사실은 바로 알아야 한다.
    -->
    <Alert
      v-if="(activity?.recentFailCount ?? 0) > 0"
      show-icon
      type="warning"
      :message="`최근 30일 안에 로그인 실패가 ${activity?.recentFailCount}번 있었습니다`"
      :description="
        activity?.lastFail
          ? `마지막 실패: ${dt(activity.lastFail.at)} · ${activity.lastFail.ip ?? '주소 없음'} (${failLabel(activity.lastFail.failReason)})`
          : undefined
      "
    />

    <!-- ── 계정 ─────────────────────────────────────────── -->
    <Descriptions bordered :column="1" size="small" title="계정">
      <DescriptionsItem label="가입일">
        {{ dt(info.createdAt) }}
        <span
          v-if="activity"
          class="text-muted-foreground ml-1"
        >
          ({{ activity.accountAgeDays }}일째)
        </span>
      </DescriptionsItem>

      <DescriptionsItem label="역할">
        <template v-if="roleLabels.length > 0">
          <Tag v-for="name in roleLabels" :key="name">{{ name }}</Tag>
        </template>
        <span v-else class="text-muted-foreground">배정된 역할이 없습니다.</span>
      </DescriptionsItem>

      <!-- 이관 계정만 값이 있다. 저쪽 시스템의 어느 레코드에서 왔는지다. -->
      <DescriptionsItem v-if="info.msaSource" label="이관 출처">
        <span class="font-mono">{{ info.msaSource }}</span>
      </DescriptionsItem>
    </Descriptions>

    <!-- ── 접속 ─────────────────────────────────────────── -->
    <Descriptions bordered :column="1" size="small" title="접속">
      <DescriptionsItem label="최근 로그인">
        {{ dt(info.lastLoginAt) }}
        <span class="text-muted-foreground ml-1 font-mono">
          {{ info.lastLoginIp || '' }}
        </span>
      </DescriptionsItem>

      <!--
        지난번 접속. 낯선 시각·주소가 보이면 남이 들어온 것이다 —
        그 판단을 사용자가 할 수 있게 지금 접속과 나란히 둔다.
      -->
      <DescriptionsItem label="이전 접속">
        <template v-if="activity?.previousLogin">
          {{ dt(activity.previousLogin.at) }}
          <span class="text-muted-foreground ml-1 font-mono">
            {{ activity.previousLogin.ip || '' }}
          </span>
          <span v-if="activity.previousLogin.device" class="text-muted-foreground ml-1">
            · {{ activity.previousLogin.device }}
          </span>
        </template>
        <span v-else class="text-muted-foreground">기록 없음</span>
      </DescriptionsItem>

      <DescriptionsItem label="로그인 횟수">
        <template v-if="activity">
          {{ activity.loginCount.toLocaleString() }}회
          <span class="text-muted-foreground ml-1 text-xs">
            (기록을 남기기 시작한 뒤)
          </span>
        </template>
        <span v-else class="text-muted-foreground">기록 없음</span>
      </DescriptionsItem>
    </Descriptions>

    <!-- ── 비밀번호 ─────────────────────────────────────── -->
    <Descriptions bordered :column="1" size="small" title="비밀번호">
      <DescriptionsItem label="변경일">
        {{ dt(info.passwordChangedAt) }}
      </DescriptionsItem>

      <DescriptionsItem v-if="policyOn" label="만료">
        <div class="flex flex-wrap items-center gap-2">
          <Tag :color="remainColor">
            {{
              info.passwordExpired
                ? '만료됨'
                : `${info.passwordDaysRemaining}일 남음`
            }}
          </Tag>
          <span class="text-muted-foreground text-xs">
            {{ dt(info.passwordExpiresAt) }} 까지 ({{
              info.passwordExpiryDays
            }}일 주기)
          </span>
        </div>
      </DescriptionsItem>
    </Descriptions>

    <!-- ── 접속 기록 ────────────────────────────────────── -->
    <div>
      <div class="mb-2 font-medium">최근 접속 기록</div>

      <LoginLogGrid :table-data="recent">
        <template #at="{ row }">
          {{ dt((row as LoginLog).at) }}
        </template>

        <template #success="{ row }">
          <Tag v-if="(row as LoginLog).success" color="success">성공</Tag>
          <Tag v-else color="error">
            {{ failLabel((row as LoginLog).failReason) }}
          </Tag>
        </template>

        <template #ip="{ row }">
          <span class="font-mono">{{ (row as LoginLog).ip || '기록 없음' }}</span>
        </template>

        <!-- 줄인 이름을 보여 주고 원문은 마우스를 올려 본다 -->
        <template #device="{ row }">
          <Tooltip :title="(row as LoginLog).userAgent || ''">
            <span>{{ deviceLabel(row as LoginLog) }}</span>
          </Tooltip>
        </template>
      </LoginLogGrid>
    </div>

    <p class="text-muted-foreground text-xs">
      접속 IP 는 요청 헤더에서 얻은 값이라 참고용입니다.
      기록은 이 표가 만들어진 뒤의 로그인부터 쌓입니다.
      모르는 접속이 보이면 '비밀번호 변경' 탭에서 비밀번호를 바꾸세요.
    </p>
  </div>
</template>

<style scoped>
/*
  [글자 크기를 다른 탭에 맞춘다]

  다른 프로필 탭은 vben 폼이라 `0.875rem`(루트 14px 기준 = 12.25px)으로 그려진다.
  antd 의 Descriptions 와 vxe 그리드는 자기 기준(14px)을 쓰기 때문에 이 탭만 커 보였다.
  그쪽을 폼 기준으로 내린다 — 반대로 폼을 올리면 프로필 밖의 모든 화면이 함께 커진다.
  (표를 그리드로 옮기면서 `.ant-table*` 자리를 `.vxe-table*` 로 바꿨다.)
*/
.account-info {
  font-size: 0.875rem;
}

.account-info :deep(.ant-descriptions-item-label),
.account-info :deep(.ant-descriptions-item-content),
.account-info :deep(.ant-descriptions-title),
.account-info :deep(.vxe-grid),
.account-info :deep(.vxe-table),
.account-info :deep(.vxe-table .vxe-header--column),
.account-info :deep(.vxe-table .vxe-body--column),
.account-info :deep(.vxe-table--empty-content),
.account-info :deep(.ant-alert-message),
.account-info :deep(.ant-alert-description) {
  font-size: 0.875rem;
}

/* 묶음 제목은 조금만 키운다. 표 제목이 본문과 같은 크기면 구분이 안 된다. */
.account-info :deep(.ant-descriptions-title) {
  font-size: 0.9375rem;
}

/* 라벨 칸 너비를 맞춰 세 표의 왼쪽 줄이 어긋나지 않게 한다. */
.account-info :deep(.ant-descriptions-item-label) {
  width: 140px;
}
</style>
