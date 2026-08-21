<script lang="ts" setup>
import { computed, onMounted, reactive, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Avatar,
  Button,
  Card,
  Col,
  Descriptions,
  DescriptionsItem,
  Form,
  FormItem,
  Input,
  InputPassword,
  message,
  Row,
  Space,
  Spin,
  Tag,
  Upload,
} from 'ant-design-vue';

import {
  changeAdminPassword,
  getMyInfo,
  updateAdmin,
  updateCustomer,
} from '#/api/helpdesk';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';

/**
 * [내 프로필]
 *
 * 원본(JinReception pages/Profile.vue, `/profile`).
 * 이름·이메일·사진을 고치고, 헬프데스크 자체 로그인 비밀번호를 바꾼다.
 *
 * funeralv2 로그인 계정과 비밀번호는 여기서 다루지 않는다(AuthServer 소관).
 * 다만 헬프데스크 자체 로그인은 JinReception 이 아직 쓰고 있어 남겨 두었다.
 */

const helpdesk = useHelpdeskStore();

const loading = ref(false);
const saving = ref(false);
const changingPassword = ref(false);
const info = ref<any>(null);

const form = reactive({ email: '', photo: '', userName: '' });

const passwordForm = reactive({
  confirmPassword: '',
  currentPassword: '',
  newPassword: '',
});

/** 이름 첫 글자. 사진이 없을 때 아바타에 쓴다. */
const avatarInitial = computed(() =>
  (form.userName || info.value?.loginId || '?').charAt(0).toUpperCase(),
);

/**
 * 비밀번호 규칙. 원본과 같다 — 8자 이상, 특수문자와 숫자를 각각 하나 이상 포함.
 */
const passwordError = computed(() => {
  const { confirmPassword, currentPassword, newPassword } = passwordForm;
  if (!currentPassword && !newPassword && !confirmPassword) return '';

  if (!currentPassword) return '현재 비밀번호를 입력하세요.';
  if (newPassword.length < 8) return '새 비밀번호는 8자 이상이어야 합니다.';
  if (!/[!@#$%^&*(),.?":{}|<>]/.test(newPassword)) {
    return '새 비밀번호에 특수문자를 포함하세요.';
  }
  if (!/\d/.test(newPassword)) return '새 비밀번호에 숫자를 포함하세요.';
  if (newPassword !== confirmPassword) return '새 비밀번호가 서로 다릅니다.';
  return '';
});

const canChangePassword = computed(
  () =>
    Boolean(passwordForm.currentPassword) &&
    Boolean(passwordForm.newPassword) &&
    passwordError.value === '',
);

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

/**
 * 사진을 base64 로 읽어 폼에 담는다.
 * 원본과 같은 방식이라 별도 업로드 엔드포인트를 쓰지 않는다.
 */
function beforeUploadPhoto(file: File) {
  if (file.size > 1_000_000) {
    message.warning('사진은 1MB 이하만 올릴 수 있습니다.');
    return false;
  }

  const reader = new FileReader();
  reader.addEventListener('load', (e) => {
    const result = e.target?.result;
    if (typeof result === 'string') form.photo = result;
  });
  reader.readAsDataURL(file);

  // 자동 업로드는 막고 폼에만 담는다.
  return false;
}

function removePhoto() {
  form.photo = '';
}

async function onChangePassword() {
  if (!canChangePassword.value || !info.value?.loginId) return;

  changingPassword.value = true;
  try {
    await changeAdminPassword({
      currentPassword: passwordForm.currentPassword,
      loginId: info.value.loginId,
      newPassword: passwordForm.newPassword,
    });
    message.success('비밀번호를 변경했습니다.');
    passwordForm.currentPassword = '';
    passwordForm.newPassword = '';
    passwordForm.confirmPassword = '';
  } finally {
    changingPassword.value = false;
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

      <Row :gutter="[12, 12]">
        <!-- 프로필 -->
        <Col :lg="14" :xs="24">
          <Card size="small" title="프로필">
            <div class="flex flex-col gap-4 md:flex-row">
              <!-- 사진 -->
              <div class="flex flex-col items-center gap-2">
                <Avatar v-if="form.photo" :size="120" :src="form.photo" />
                <Avatar v-else :size="120">{{ avatarInitial }}</Avatar>

                <Space>
                  <Upload
                    :before-upload="beforeUploadPhoto"
                    :file-list="[]"
                    accept="image/*"
                  >
                    <Button size="small">사진 선택</Button>
                  </Upload>
                  <Button
                    v-if="form.photo"
                    danger
                    size="small"
                    @click="removePhoto"
                  >
                    제거
                  </Button>
                </Space>
                <span class="text-[10px] text-muted-foreground">1MB 이하</span>
              </div>

              <!-- 기본 정보 -->
              <Form class="min-w-0 flex-1" layout="vertical">
                <FormItem label="이름">
                  <Input v-model:value="form.userName" />
                </FormItem>
                <FormItem label="이메일">
                  <Input v-model:value="form.email" />
                </FormItem>
                <Button :loading="saving" type="primary" @click="save">
                  저장
                </Button>
              </Form>
            </div>
          </Card>
        </Col>

        <!-- 비밀번호 -->
        <Col :lg="10" :xs="24">
          <Card size="small" title="비밀번호 변경">
            <Alert
              class="mb-3"
              description="funeralv2 로그인 비밀번호가 아니라, JinReception 에서 쓰는 헬프데스크 자체 로그인 비밀번호입니다."
              message="어떤 비밀번호인가요?"
              show-icon
              type="info"
            />

            <Form layout="vertical">
              <FormItem label="현재 비밀번호">
                <InputPassword v-model:value="passwordForm.currentPassword" />
              </FormItem>
              <FormItem label="새 비밀번호">
                <InputPassword v-model:value="passwordForm.newPassword" />
              </FormItem>
              <FormItem
                :help="passwordError || undefined"
                :validate-status="passwordError ? 'error' : undefined"
                label="새 비밀번호 확인"
              >
                <InputPassword v-model:value="passwordForm.confirmPassword" />
              </FormItem>
            </Form>

            <div class="mb-2 text-[11px] text-muted-foreground">
              8자 이상, 특수문자와 숫자를 각각 하나 이상 포함해야 합니다.
            </div>

            <Button
              :disabled="!canChangePassword"
              :loading="changingPassword"
              type="primary"
              @click="onChangePassword"
            >
              비밀번호 변경
            </Button>
          </Card>
        </Col>
      </Row>
    </Spin>
  </Page>
</template>
