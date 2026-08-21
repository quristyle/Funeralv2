<script lang="ts" setup>
import { Loading, Page, Spinner } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import { refAutoReset } from '@vueuse/core';
import { Button, Card, Spin } from 'ant-design-vue';

const spinning = refAutoReset(false, 3000);
const loading = refAutoReset(false, 3000);

const spinningV = refAutoReset(false, 3000);
const loadingV = refAutoReset(false, 3000);
</script>
<template>
  <Page
    title="Vben Loading"
    description="로딩 상태 컴포넌트입니다. 이 컴포넌트는 다른 컨테이너 컴포넌트에 로딩 마스크 레이어를 추가할 수 있습니다. 사용 시 컨테이너는 relative 위치 지정이 필요합니다."
  >
    <Card title="Antd Spin">
      <template #actions>Ant Design 컴포넌트 라이브러리의 기본 Spin 컴포넌트 데모입니다.</template>
      <Spin :spinning="spinning" tip="로딩 중...">
        <Button type="primary" @click="spinning = true">Spin 표시</Button>
      </Spin>
    </Card>

    <Card title="Vben Loading" v-loading="loadingV" class="mt-4">
      <template #extra>
        <Button type="primary" @click="loadingV = true">
          v-loading 지시어
        </Button>
      </template>
      <template #actions>
        Loading 컴포넌트는 텍스트를 설정할 수 있으며, 로딩 아이콘을 교체할 수 있는 icon 슬롯도 제공합니다.
      </template>
      <div class="flex gap-4">
        <div class="size-40">
          <Loading
            :spinning="loading"
            text="로딩 중..."
            class="flex-center size-full"
          >
            <Button type="primary" @click="loading = true">기본 애니메이션</Button>
          </Loading>
        </div>
        <div class="size-40">
          <Loading :spinning="loading" class="flex-center size-full">
            <Button type="primary" @click="loading = true">사용자 정의 애니메이션 1</Button>
            <template #icon>
              <IconifyIcon
                icon="svg-spinners:ring-resize"
                class="size-10 text-primary"
              />
            </template>
          </Loading>
        </div>
        <div class="size-40">
          <Loading :spinning="loading" class="flex-center size-full">
            <Button type="primary" @click="loading = true">사용자 정의 애니메이션 2</Button>
            <template #icon>
              <IconifyIcon
                icon="svg-spinners:bars-scale"
                class="size-10 text-primary"
              />
            </template>
          </Loading>
        </div>
      </div>
    </Card>

    <Card
      title="Vben Spinner"
      v-spinning="spinningV"
      class="mt-4 overflow-hidden"
      :body-style="{
        position: 'relative',
        overflow: 'hidden',
      }"
    >
      <template #extra>
        <Button type="primary" @click="spinningV = true">
          v-spinning 지시어
        </Button>
      </template>
      <template #actions>
        Spinner 컴포넌트는 Loading 컴포넌트의 특수 사례로, 고정된 단일 스타일만 가집니다.
      </template>
      <Spinner :spinning="spinning" class="flex-center size-40">
        <Button type="primary" @click="spinning = true">Spinner 표시</Button>
      </Spinner>
    </Card>
  </Page>
</template>
