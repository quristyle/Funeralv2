<script lang="ts" setup>
import { reactive, ref } from 'vue';

import { ColPage } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import {
  Alert,
  Button,
  Card,
  Checkbox,
  Slider,
  Tag,
  Tooltip,
} from 'ant-design-vue';

const props = reactive({
  leftCollapsedWidth: 5,
  leftCollapsible: true,
  leftMaxWidth: 50,
  leftMinWidth: 20,
  leftWidth: 30,
  resizable: true,
  rightWidth: 70,
  splitHandle: true,
  splitLine: true,
});
const leftMinWidth = ref(props.leftMinWidth || 1);
const leftMaxWidth = ref(props.leftMaxWidth || 100);
</script>
<template>
  <ColPage
    auto-content-height
    description="ColPage는 왼쪽 접기, 드래그를 통한 너비 조정 등의 기능을 지원하는 2열 레이아웃 컴포넌트입니다."
    v-bind="props"
    title="ColPage 2열 레이아웃 컴포넌트"
  >
    <template #title>
      <span class="mr-2 text-2xl font-bold">ColPage 2열 레이아웃 컴포넌트</span>
      <Tag color="hsl(var(--destructive))">Alpha</Tag>
    </template>
    <template #left="{ isCollapsed, expand }">
      <div v-if="isCollapsed" @click="expand">
        <Tooltip title="클릭하여 왼쪽 펼치기">
          <Button shape="circle" type="primary" class="flex-center">
            <template #icon>
              <IconifyIcon class="text-2xl" icon="bi:arrow-right" />
            </template>
          </Button>
        </Tooltip>
      </div>
      <div
        v-else
        :style="{ minWidth: '200px' }"
        class="mr-2 rounded-(--radius) border border-border bg-card p-2"
      >
        <p>여기는 왼쪽 콘텐츠입니다</p>
        <p>여기는 왼쪽 콘텐츠입니다</p>
        <p>여기는 왼쪽 콘텐츠입니다</p>
        <p>여기는 왼쪽 콘텐츠입니다</p>
        <p>여기는 왼쪽 콘텐츠입니다</p>
      </div>
    </template>
    <Card class="ml-2" title="기본 사용법">
      <div class="flex flex-col gap-2">
        <div class="flex gap-2">
          <span class="flex items-center gap-1.5">
            <Checkbox v-model:checked="props.resizable" /> 드래그로 너비 조정 가능
          </span>
          <span class="flex items-center gap-1.5">
            <Checkbox v-model:checked="props.splitLine" /> 드래그 구분선 표시
          </span>
          <span class="flex items-center gap-1.5">
            <Checkbox v-model:checked="props.splitHandle" /> 드래그 핸들 표시
          </span>
          <span class="flex items-center gap-1.5">
            <Checkbox v-model:checked="props.leftCollapsible" /> 왼쪽 접기 가능
          </span>
        </div>
        <div class="flex items-center gap-2">
          <span>왼쪽 최소 너비 백분율：</span>
          <Slider
            v-model:value="leftMinWidth"
            :max="props.leftMaxWidth - 1"
            :min="1"
            class="w-25"
            @after-change="(value) => (props.leftMinWidth = value as number)"
          />
          <span>왼쪽 최대 너비 백분율：</span>
          <Slider
            v-model:value="props.leftMaxWidth"
            :max="100"
            :min="leftMaxWidth + 1"
            class="w-25"
            @after-change="(value) => (props.leftMaxWidth = value as number)"
          />
        </div>
        <Alert message="실험적 컴포넌트" show-icon type="warning">
          <template #description>
            <p>
              2열 레이아웃 컴포넌트는 Page 컴포넌트를 확장한 상대적으로 기초적인 레이아웃 컴포넌트로, 왼쪽 접기(드래그로 인해 왼쪽 너비가 최소 너비보다 작아질 때 접힘 상태로 전환 가능), 드래그 너비 조정 등의 기능을 지원합니다.
            </p>
            <p>위의 너비 설정 값은 백분율이며, 최솟값은 1, 최댓값은 100입니다.</p>
            <p class="font-bold text-red-600">
              이것은 실험적인 컴포넌트이며, 사용법이 변경될 수 있고 최종적으로 채택되지 않을 수도 있습니다. 문서에 공식적으로 등장하기 전까지는 운영 환경에서의 사용을 권장하지 않습니다.
            </p>
          </template>
        </Alert>
      </div>
    </Card>
  </ColPage>
</template>
