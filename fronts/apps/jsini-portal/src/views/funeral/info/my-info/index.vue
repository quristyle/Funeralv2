<script lang="ts" setup>
import type { InfoApi } from '#/api/funeral/info';

/**
 * 나의 정보 — 옛 `page/ui_config.jsp` 의 왼쪽 칸.
 *
 * 옛 화면은 사진 · 아이디 · 성명 · 이메일 · 최종 로그인을 보여 주고 그 자리에서
 * 이름과 메일을 고쳤다. 지금은 계정 자체가 AuthServer 소관이라 여기서 고치지 않는다 —
 * 프로필 수정은 포털의 Profile 화면이 정본이다. 이 화면은 장례식장 쪽에서 본
 * 내 상태(맡은 건물 수 · 사용 중 빈소)와 업무 설정을 보여 준다.
 */
import { onMounted, ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import { Button, Card, message, Skeleton, Statistic, Tag } from 'ant-design-vue';
import dayjs from 'dayjs';

import { getMyInfo } from '#/api/funeral/info';

const router = useRouter();
const loading = ref(true);
const info = ref<InfoApi.MyInfo | null>(null);

async function load() {
  loading.value = true;
  try {
    info.value = (await getMyInfo()) ?? null;
  } catch {
    message.error('내 정보를 불러오지 못했습니다.');
  } finally {
    loading.value = false;
  }
}

onMounted(load);
</script>

<template>
  <Page auto-content-height>
    <div class="mx-auto flex h-full w-full max-w-4xl flex-col gap-4 overflow-y-auto pb-2">
      <Skeleton v-if="loading" active avatar :paragraph="{ rows: 5 }" />

      <template v-else-if="info">
        <Card size="small">
          <div class="flex flex-wrap items-center justify-between gap-4">
            <div class="flex items-center gap-3">
              <div
                class="flex size-14 items-center justify-center rounded-full border bg-muted text-xl font-bold text-muted-foreground"
              >
                {{ info.userId?.slice(0, 2)?.toUpperCase() || '??' }}
              </div>
              <div>
                <div class="text-base font-bold">{{ info.userId || '(알 수 없음)' }}</div>
                <div class="mt-1 flex items-center gap-2">
                  <Tag v-if="info.role" color="blue">{{ info.role }}</Tag>
                  <span v-else class="text-xs text-muted-foreground">역할 정보 없음</span>
                </div>
              </div>
            </div>

            <div class="flex gap-2">
              <Button @click="router.push('/profile')">
                <IconifyIcon icon="lucide:user-cog" class="mr-1 size-4" />
                프로필 수정
              </Button>
              <Button type="primary" @click="router.push('/setting/work-options')">
                <IconifyIcon icon="lucide:sliders-horizontal" class="mr-1 size-4" />
                업무 설정
              </Button>
            </div>
          </div>
        </Card>

        <!--
          예전에는 '안 읽은 알림' 칸이 하나 더 있었다. 그 칸이 가리키던
          알림정보 화면(`/info/notice`)을 2026-09-03 에 걷어내면서 함께 뺐다 —
          누를 곳이 없는 숫자를 남겨 둘 이유가 없다.
        -->
        <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
          <Card size="small">
            <Statistic title="맡은 건물" :value="info.buildingCount" suffix="곳" />
          </Card>
          <Card size="small">
            <Statistic title="사용 중 빈소" :value="info.roomsInUse" suffix="실" />
          </Card>
        </div>

        <Card title="내 업무 설정" size="small">
          <template #extra>
            <Button type="link" size="small" @click="router.push('/setting/work-options')">
              바꾸기
            </Button>
          </template>
          <ul class="divide-y">
            <li
              v-for="s in info.settings"
              :key="s.code"
              class="flex items-center justify-between gap-4 py-2 first:pt-0 last:pb-0"
            >
              <div class="min-w-0">
                <div class="text-sm">{{ s.name }}</div>
                <div class="font-mono text-[10px] text-muted-foreground/70">
                  {{ s.groupName }} · {{ s.code }}
                  <span v-if="s.updatedAt"> · {{ dayjs(s.updatedAt).format('YYYY-MM-DD') }}</span>
                </div>
              </div>
              <Tag :color="s.enabled ? 'success' : 'default'">
                {{ s.enabled ? '켬' : '끔' }}
              </Tag>
            </li>
          </ul>
          <p v-if="info.settings.length === 0" class="text-sm text-muted-foreground">
            설정 항목이 없습니다.
          </p>
        </Card>
      </template>
    </div>
  </Page>
</template>
