<script lang="ts" setup>
import { ref, onMounted } from 'vue';
import { Page, useVbenModal } from '@vben/common-ui';
import { Card, Form, Input, Button, message } from 'ant-design-vue';
import { getMyInfo, updateMyInfo } from '#/api/funeral/info';

const profile = ref<any>({
  userId: '',
  loginId: '',
  userName: '',
  email: '',
  phone: '',
  roleName: '',
  lastLoginAt: ''
});

const loading = ref<boolean>(false);

const [PasswordModal, passwordModalApi] = useVbenModal({
  title: '비밀번호 변경',
  destroyOnClose: true,
});

const passForm = ref({
  currentPassword: '',
  newPassword: '',
  confirmPassword: ''
});

async function fetchMyInfo() {
  loading.value = true;
  try {
    const data = await getMyInfo();
    profile.value = data || {};
  } catch (error) {
    message.error('내 프로필 정보를 불러올 수 없습니다.');
  } finally {
    loading.value = false;
  }
}

async function handleUpdateProfile() {
  try {
    await updateMyInfo(profile.value);
    message.success('프로필 정보가 수정되었습니다.');
    fetchMyInfo();
  } catch (error) {
    message.error('수정 실패');
  }
}

function openPasswordModal() {
  passForm.value = { currentPassword: '', newPassword: '', confirmPassword: '' };
  passwordModalApi.open();
}

async function handlePasswordSave() {
  if (!passForm.value.currentPassword || !passForm.value.newPassword) {
    message.warning('비밀번호를 입력해주세요.');
    return;
  }
  if (passForm.value.newPassword !== passForm.value.confirmPassword) {
    message.error('새 비밀번호가 일치하지 않습니다.');
    return;
  }
  // 가상 저장
  message.success('비밀번호가 안전하게 변경되었습니다.');
  passwordModalApi.close();
}

onMounted(() => {
  fetchMyInfo();
});
</script>

<template>
  <Page auto-content-height>
    <div class="max-w-2xl mx-auto space-y-6">
      <Card title="내 프로필 정보 관리" :loading="loading">
        <Form layout="vertical">
          <div class="grid grid-cols-2 gap-4">
            <Form.Item label="로그인 ID">
              <Input :value="profile.loginId" disabled />
            </Form.Item>
            <Form.Item label="소속 권한 그룹">
              <Input :value="profile.roleName" disabled />
            </Form.Item>
          </div>

          <Form.Item label="사용자 성명" required>
            <Input v-model:value="profile.userName" />
          </Form.Item>

          <Form.Item label="이메일 주소">
            <Input v-model:value="profile.email" placeholder="example@email.com" />
          </Form.Item>

          <Form.Item label="비상 연락처">
            <Input v-model:value="profile.phone" placeholder="전화번호 입력" />
          </Form.Item>

          <div class="text-xs text-muted-foreground mb-4">
            최근 로그인 기록 시각: {{ profile.lastLoginAt || '최근 기록 없음' }}
          </div>

          <div class="flex justify-between mt-6">
            <Button type="default" @click="openPasswordModal">비밀번호 변경</Button>
            <Button type="primary" @click="handleUpdateProfile">프로필 저장</Button>
          </div>
        </Form>
      </Card>
    </div>

    <!-- 패스워드 변경 모달 -->
    <PasswordModal @ok="handlePasswordSave">
      <div class="p-6">
        <Form layout="vertical">
          <Form.Item label="현재 비밀번호" required>
            <Input.Password v-model:value="passForm.currentPassword" placeholder="현재 비밀번호 입력" />
          </Form.Item>
          <Form.Item label="새 비밀번호" required>
            <Input.Password v-model:value="passForm.newPassword" placeholder="새 비밀번호 입력" />
          </Form.Item>
          <Form.Item label="새 비밀번호 확인" required>
            <Input.Password v-model:value="passForm.confirmPassword" placeholder="새 비밀번호 다시 한 번 입력" />
          </Form.Item>
        </Form>
      </div>
    </PasswordModal>
  </Page>
</template>
