<script lang="ts" setup>
import { onMounted, ref } from 'vue';

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
  Space,
  Tag,
  Textarea,
} from 'ant-design-vue';

import {
  isPushSubscribed,
  sendTestPush,
  subscribePush,
  unsubscribePush,
} from '#/api/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';

/**
 * [알림 설정]
 *
 * 원본(NotificationSettings.vue)의 웹푸시 구독 관리와 테스트 발송을 옮겼다.
 *
 * 주의: 웹푸시 구독은 서비스 워커가 등록되어 있어야 동작한다. JinReception 은 PWA 였지만
 * funeralv2 프론트는 아직 서비스 워커를 등록하지 않아 구독 버튼은 비활성으로 표시된다.
 * 테스트 발송은 서버가 처리하므로 서비스 워커 없이도 동작한다.
 */

const permission = ref<NotificationPermission | 'unsupported'>('default');
const swAvailable = ref(false);
const subscribed = ref(false);
const busy = ref(false);

const testForm = ref({
  body: '본문에 표현될 내용을 입력하세요.',
  title: '테스트 제목',
  url: '/helpdesk/dashboard',
});

/** 브라우저 지원 여부와 현재 구독 상태를 확인한다. */
async function checkStatus() {
  if (!('Notification' in window)) {
    permission.value = 'unsupported';
    return;
  }
  permission.value = Notification.permission;

  if (!('serviceWorker' in navigator) || !('PushManager' in window)) return;

  const registration = await navigator.serviceWorker.getRegistration();
  swAvailable.value = Boolean(registration?.active);
  if (!swAvailable.value) return;

  const ready = await navigator.serviceWorker.ready;
  const browserSubscription = await ready.pushManager.getSubscription();
  if (!browserSubscription) {
    subscribed.value = false;
    return;
  }

  // 브라우저에 구독이 남아 있어도 서버에서 지워졌을 수 있으니 서버 기준으로 확인한다.
  const result = await isPushSubscribed(browserSubscription.endpoint).catch(
    () => null,
  );
  subscribed.value = Boolean(result?.isSubscribed);
}

async function onSubscribe() {
  busy.value = true;
  try {
    const granted = await Notification.requestPermission();
    permission.value = granted;
    if (granted !== 'granted') {
      message.warning('브라우저에서 알림 권한이 허용되지 않았습니다.');
      return;
    }

    const registration = await navigator.serviceWorker.ready;
    const subscription = await registration.pushManager.subscribe({
      userVisibleOnly: true,
    });

    await subscribePush(subscription.toJSON());
    subscribed.value = true;
    message.success('알림 구독을 등록했습니다.');
  } catch (error) {
    message.error(`구독에 실패했습니다: ${(error as Error).message}`);
  } finally {
    busy.value = false;
  }
}

async function onUnsubscribe() {
  busy.value = true;
  try {
    const registration = await navigator.serviceWorker.ready;
    const subscription = await registration.pushManager.getSubscription();
    if (subscription) {
      await unsubscribePush(subscription.endpoint);
      await subscription.unsubscribe();
    }
    subscribed.value = false;
    message.success('알림 구독을 해제했습니다.');
  } finally {
    busy.value = false;
  }
}

async function onSendTest() {
  busy.value = true;
  try {
    await sendTestPush(testForm.value);
    message.success('테스트 알림을 발송했습니다.');
  } finally {
    busy.value = false;
  }
}

onMounted(checkStatus);
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Alert
      v-if="!swAvailable"
      class="mb-3"
      description="브라우저 푸시 구독은 서비스 워커가 등록된 환경에서만 동작합니다. 현재 funeralv2 프론트에는 서비스 워커가 없어 구독 등록·해제는 사용할 수 없습니다. 아래 테스트 발송은 서버가 처리하므로 정상 동작합니다."
      message="이 브라우저에서는 푸시 구독을 사용할 수 없습니다"
      show-icon
      type="warning"
    />

    <Card class="mb-3" size="small" title="구독 상태">
      <Descriptions :column="{ md: 3, xs: 1 }" size="small">
        <DescriptionsItem label="브라우저 권한">
          <Tag
            :color="
              permission === 'granted'
                ? 'success'
                : permission === 'denied'
                  ? 'error'
                  : 'default'
            "
          >
            {{ permission }}
          </Tag>
        </DescriptionsItem>
        <DescriptionsItem label="서비스 워커">
          <Tag :color="swAvailable ? 'success' : 'default'">
            {{ swAvailable ? '활성' : '없음' }}
          </Tag>
        </DescriptionsItem>
        <DescriptionsItem label="서버 구독">
          <Tag :color="subscribed ? 'success' : 'default'">
            {{ subscribed ? '등록됨' : '미등록' }}
          </Tag>
        </DescriptionsItem>
      </Descriptions>

      <Space class="mt-2">
        <Button
          :disabled="!swAvailable || subscribed"
          :loading="busy"
          type="primary"
          @click="onSubscribe"
        >
          구독 등록
        </Button>
        <Button
          :disabled="!swAvailable || !subscribed"
          :loading="busy"
          danger
          @click="onUnsubscribe"
        >
          구독 해제
        </Button>
        <Button @click="checkStatus">상태 새로고침</Button>
      </Space>
    </Card>

    <Card size="small" title="테스트 발송">
      <Form layout="vertical">
        <FormItem label="제목">
          <Input v-model:value="testForm.title" />
        </FormItem>
        <FormItem label="본문">
          <Textarea v-model:value="testForm.body" :rows="3" />
        </FormItem>
        <FormItem label="클릭 시 이동할 경로">
          <Input v-model:value="testForm.url" />
        </FormItem>
      </Form>
      <Button :loading="busy" type="primary" @click="onSendTest">
        테스트 발송
      </Button>
    </Card>
  </Page>
</template>
