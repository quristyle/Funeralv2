<script lang="ts" setup>
import type { UserPropertyMap } from '#/api/helpdesk';

import { onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Card, List, ListItem, message, Spin, Switch } from 'ant-design-vue';

import { getUserProperties, saveUserProperties } from '#/api/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';

/**
 * [개인 설정]
 *
 * 원본(UserProperties.vue). 값은 서버에 문자열('true'/'false')로 저장된다.
 * 스위치를 누르면 즉시 저장하고, 실패하면 원래 값으로 되돌린다.
 */

/** 화면에 노출할 설정 항목 */
const SETTINGS = [
  {
    description: '주요 변경 사항에 대한 알림을 이메일로 받습니다.',
    key: 'receiveEmail',
    label: '이메일 알림 받기',
  },
  {
    description: '내가 등록한 요청이 처리 완료되었을 때 알림을 받습니다.',
    key: 'notifyOnRequestCompletion',
    label: '접수 완료 시 알림 받기',
  },
];

const loading = ref(false);
const properties = ref<UserPropertyMap>({});

function isOn(key: string) {
  return properties.value[key] === 'true';
}

async function loadData() {
  loading.value = true;
  try {
    properties.value = (await getUserProperties()) ?? {};
  } finally {
    loading.value = false;
  }
}

async function toggle(key: string, label: string, value: boolean) {
  const previous = properties.value[key];
  properties.value = { ...properties.value, [key]: String(value) };

  try {
    await saveUserProperties({ [key]: String(value) });
    message.success(`${label} 설정을 변경했습니다.`);
  } catch {
    // 실패하면 원래 값으로 되돌린다.
    properties.value = { ...properties.value, [key]: previous ?? 'false' };
  }
}

onMounted(loadData);
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Card size="small" title="알림 설정">
      <Spin :spinning="loading">
        <List :data-source="SETTINGS">
          <template #renderItem="{ item }">
            <ListItem>
              <div class="min-w-0 flex-1">
                <div class="font-medium">{{ item.label }}</div>
                <div class="text-xs text-muted-foreground">
                  {{ item.description }}
                </div>
              </div>
              <Switch
                :checked="isOn(item.key)"
                @change="(v: any) => toggle(item.key, item.label, v)"
              />
            </ListItem>
          </template>
        </List>
      </Spin>
    </Card>
  </Page>
</template>
