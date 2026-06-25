<script setup lang="ts">
import type { Props } from './types';

import { preferences } from '@vben-core/preferences';
import {
  Card,
  Separator,
  Tabs,
  TabsList,
  TabsTrigger,
} from '@vben-core/shadcn-ui';

import { Page, FileUpload } from '../../components';

defineOptions({
  name: 'ProfileUI',
});

withDefaults(defineProps<Props>(), {
  title: '프로젝트 정보',
  tabs: () => [],
});

const emit = defineEmits<{
  (e: 'change-avatar', url: string): void;
}>();

const tabsValue = defineModel<string>('modelValue');
</script>
<template>
  <Page auto-content-height>
    <div class="flex size-full">
      <Card class="w-1/6 flex-none">
        <div class="mt-4 flex flex-col items-center justify-center gap-4 h-48">
          <FileUpload
            mode="avatar"
            :value="(userInfo?.avatar ?? preferences.app.defaultAvatar) || ''"
            @change="(val) => emit('change-avatar', Array.isArray(val) ? val[0] as string : val as string)"
          />
          <span class="text-lg font-semibold">
            {{ userInfo?.realName ?? '' }}
          </span>
          <span class="text-sm text-foreground/80">
            {{ userInfo?.username ?? '' }}
          </span>
        </div>
        <Separator class="my-4" />
        <Tabs v-model="tabsValue" orientation="vertical" class="m-4">
          <TabsList class="grid w-full grid-cols-1 bg-card">
            <TabsTrigger
              v-for="tab in tabs"
              :key="tab.value"
              :value="tab.value"
              class="h-12 justify-start data-[state=active]:bg-primary data-[state=active]:text-primary-foreground"
            >
              {{ tab.label }}
            </TabsTrigger>
          </TabsList>
        </Tabs>
      </Card>
      <Card class="ml-4 w-5/6 flex-auto p-8">
        <slot name="content"></slot>
      </Card>
    </div>
  </Page>
</template>
