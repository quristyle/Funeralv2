<script lang="ts" setup>
import { onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Button,
  Card,
  Descriptions,
  DescriptionsItem,
  Form,
  FormItem,
  Input,
  message,
  Spin,
  Tag,
} from 'ant-design-vue';

import { getMyInfo, updateAdmin, updateCustomer } from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';

/**
 * [내 프로필]
 *
 * 원본(Profile.vue)에서 헬프데스크 쪽 정보(이름·이메일·사진)만 옮겼다.
 * 비밀번호 변경은 계정이 funeralv2 로 단일화되어 헬프데스크에서 다루지 않는다.
 */

const helpdesk = useHelpdeskStore();

const loading = ref(false);
const saving = ref(false);
const info = ref<any>(null);

const form = reactive({ email: '', photo: '', userName: '' });

async function load() {
  loading.value = true;
  try {
    info.value = await getMyInfo();
    form.userName = info.value?.userName ?? '';
    form.email = info.value?.email ?? '';
    form.photo = info.value?.photo ?? '';
  } catch {
    info.value = null;
  } finally {
    loading.value = false;
  }
}

async function save() {
  const userId = helpdesk.helpdeskUserId;
  if (!userId) {
    message.warning('연결된 헬프데스크 계정이 없습니다.');
    return;
  }

  saving.value = true;
  try {
    await (helpdesk.isAdmin
      ? updateAdmin(userId, { ...form })
      : updateCustomer(userId, { ...form }));
    message.success('프로필을 저장했습니다.');
    await load();
  } finally {
    saving.value = false;
  }
}

onMounted(async () => {
  await helpdesk.loadIdentity();
  if (helpdesk.helpdeskUserId) await load();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Alert
      class="mb-3"
      description="로그인 계정과 비밀번호는 funeralv2 계정 설정에서 관리합니다. 이 화면에서는 헬프데스크에 표시되는 이름과 연락처만 수정합니다."
      message="계정 정보 안내"
      show-icon
      type="info"
    />

    <Spin :spinning="loading">
      <Card class="mb-3" size="small" title="연결 정보">
        <Descriptions :column="{ md: 3, xs: 1 }" size="small">
          <DescriptionsItem label="헬프데스크 계정">
            {{ info?.loginId ?? '-' }}
          </DescriptionsItem>
          <DescriptionsItem label="구분">
            <Tag :color="helpdesk.isAdmin ? 'blue' : 'green'">
              {{ helpdesk.isAdmin ? '담당자' : '고객' }}
            </Tag>
          </DescriptionsItem>
          <DescriptionsItem label="소속 회사">
            {{ info?.company?.name ?? '-' }}
          </DescriptionsItem>
        </Descriptions>
      </Card>

      <Card size="small" title="프로필">
        <Form layout="vertical" style="max-width: 480px">
          <FormItem label="이름">
            <Input v-model:value="form.userName" />
          </FormItem>
          <FormItem label="이메일">
            <Input v-model:value="form.email" />
          </FormItem>
          <FormItem label="사진 URL">
            <Input v-model:value="form.photo" />
          </FormItem>
        </Form>
        <Button :loading="saving" type="primary" @click="save">저장</Button>
      </Card>
    </Spin>
  </Page>
</template>
