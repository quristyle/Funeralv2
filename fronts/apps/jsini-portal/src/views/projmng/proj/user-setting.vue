<script setup lang="ts">
/**
 * [내 프로젝트 사용자 정보]
 *
 * 원본: ProjMngWasm `Pages/Proj/ProjUserSetting.razor` (`/proj-user-setting`).
 * 프로시저: `sp_dev_user_exec` (`/Proj/sys` 경로로 보낸다 — 원본 `isServerFix=true`)
 *
 * 원본은 프로젝트관리가 들고 있는 자기 사용자 레코드를 **고치는** 화면이었다.
 * 계정을 JSini 포털로 단일화하면서 편집 기능을 걷어냈다(결정 Q4) —
 * 사람의 정본 정보는 포털 계정에 있고, 고치는 곳도 포털 한 곳이어야 한다.
 *
 * 지금 이 화면은 보여 주기만 한다.
 *   1) JSini 계정 — 정본
 *   2) 프로젝트관리가 들고 있는 사용자 레코드 — 저장 프로시저가 담당자 표시·감사에 쓰는 값
 *
 * 조회 기준은 포털 로그인 아이디다. 서버도 같은 값(`X-User-Id`)을 `req_ss_user_id` 로 쓴다.
 * 원본에 있던 테마·글꼴 선택은 이식하지 않았다 — 포털 환경설정이 같은 일을 한다.
 */
import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  Descriptions,
  DescriptionsItem,
  Tag,
} from 'ant-design-vue';

import { dbCont } from '#/api/projmng';
import { useJsiniUser } from '#/composables/use-jsini-user';

const {
  companyName,
  deptName,
  email: jsiniEmail,
  loginId,
  phone: jsiniPhone,
  roles,
  userName: jsiniUserName,
} = useJsiniUser();

const loading = ref(false);
/** 포털 계정에 대응하는 프로젝트관리 사용자 레코드가 있는지 */
const recordFound = ref(true);

/** 프로젝트관리가 들고 있는 사용자 레코드(표시용) */
const record = ref({
  user_id: '',
  user_name: '',
  user_name_eng: '',
  email: '',
  phone_num: '',
  remark: '',
});

async function load() {
  loading.value = true;
  try {
    const userId = loginId.value;
    const result = await dbCont(
      'sp_dev_user_exec',
      { user_id: userId, user_name: '' },
      'srch',
      { isServerFix: true },
    );

    // 프로시저는 조건에 맞는 사용자를 모두 돌려줄 수 있다. 내 아이디와 정확히 같은 행만 쓴다.
    const row = (result.data ?? []).find(
      (r: any) => String(r?.user_id ?? '') === userId,
    );

    if (!row) {
      recordFound.value = false;
      record.value = { ...record.value, user_id: userId };
      return;
    }

    recordFound.value = true;
    record.value = {
      user_id: String(row.user_id ?? userId),
      user_name: String(row.user_name ?? ''),
      user_name_eng: String(row.user_name_eng ?? ''),
      email: String(row.email ?? ''),
      phone_num: String(row.phone_num ?? ''),
      remark: String(row.remark ?? ''),
    };
  } finally {
    loading.value = false;
  }
}

onMounted(load);
</script>

<template>
  <Page auto-content-height>
    <Card class="mb-2" size="small" title="JSini 계정">
      <template #extra>
        <Button size="small" @click="$router.push('/profile')">
          개인 설정에서 수정
        </Button>
      </template>

      <Descriptions :column="{ md: 3, xs: 1 }" size="small">
        <DescriptionsItem label="로그인 아이디">
          {{ loginId || '-' }}
        </DescriptionsItem>
        <DescriptionsItem label="이름">
          {{ jsiniUserName || '-' }}
        </DescriptionsItem>
        <DescriptionsItem label="이메일">
          {{ jsiniEmail || '-' }}
        </DescriptionsItem>
        <DescriptionsItem label="연락처">
          {{ jsiniPhone || '-' }}
        </DescriptionsItem>
        <DescriptionsItem label="소속">
          {{ [companyName, deptName].filter(Boolean).join(' · ') || '-' }}
        </DescriptionsItem>
        <DescriptionsItem label="역할">
          <template v-if="roles.length > 0">
            <Tag v-for="role in roles" :key="role">{{ role }}</Tag>
          </template>
          <span v-else class="text-muted-foreground">배정된 역할 없음</span>
        </DescriptionsItem>
      </Descriptions>
    </Card>

    <Alert
      v-if="!recordFound"
      class="mb-2"
      :message="`프로젝트관리에 '${loginId}' 사용자 레코드가 없습니다.`"
      description="프로젝트관리 화면에서 담당자로 표시되려면 같은 아이디의 사용자 레코드가 있어야 합니다. 프로젝트 참여자 화면에서 확인하세요."
      show-icon
      type="warning"
    />

    <Card :loading="loading" size="small" title="프로젝트관리 사용자 레코드">
      <Alert
        class="mb-3"
        message="읽기 전용입니다. 이름·이메일·연락처의 정본은 JSini 포털 계정이며, 고치는 곳도 포털 개인 설정 한 곳입니다."
        show-icon
        type="info"
      />

      <Descriptions :column="{ md: 3, xs: 1 }" size="small">
        <DescriptionsItem label="사용자 ID">
          {{ record.user_id || '-' }}
        </DescriptionsItem>
        <DescriptionsItem label="이름">
          {{ record.user_name || '-' }}
        </DescriptionsItem>
        <DescriptionsItem label="영문 이름">
          {{ record.user_name_eng || '-' }}
        </DescriptionsItem>
        <DescriptionsItem label="이메일">
          {{ record.email || '-' }}
        </DescriptionsItem>
        <DescriptionsItem label="연락처">
          {{ record.phone_num || '-' }}
        </DescriptionsItem>
        <DescriptionsItem label="비고">
          {{ record.remark || '-' }}
        </DescriptionsItem>
      </Descriptions>
    </Card>

  </Page>
</template>
