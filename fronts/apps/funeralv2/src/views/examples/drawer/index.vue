<script lang="ts" setup>
import type { DrawerPlacement, DrawerState } from '@vben/common-ui';

import { Page, useVbenDrawer } from '@vben/common-ui';

import { Button, Card } from 'ant-design-vue';

import DocButton from '../doc-button.vue';
import AutoHeightDemo from './auto-height-demo.vue';
import BaseDemo from './base-demo.vue';
import DynamicDemo from './dynamic-demo.vue';
import FormDrawerDemo from './form-drawer-demo.vue';
import inContentDemo from './in-content-demo.vue';
import SharedDataDemo from './shared-data-demo.vue';

defineOptions({ name: 'DrawerExample' });
const [BaseDrawer, baseDrawerApi] = useVbenDrawer({
  // 분리된 컴포넌트 연결
  connectedComponent: BaseDemo,
  // placement: 'left',
});

const [InContentDrawer, inContentDrawerApi] = useVbenDrawer({
  // 분리된 컴포넌트 연결
  connectedComponent: inContentDemo,
  // placement: 'left',
});

const [AutoHeightDrawer, autoHeightDrawerApi] = useVbenDrawer({
  connectedComponent: AutoHeightDemo,
});

const [DynamicDrawer, dynamicDrawerApi] = useVbenDrawer({
  connectedComponent: DynamicDemo,
});

const [SharedDataDrawer, sharedDrawerApi] = useVbenDrawer({
  connectedComponent: SharedDataDemo,
});

const [FormDrawer, formDrawerApi] = useVbenDrawer({
  connectedComponent: FormDrawerDemo,
});

function openBaseDrawer(placement: DrawerPlacement = 'right') {
  baseDrawerApi.setState({ placement }).open();
}

function openBlurDrawer() {
  baseDrawerApi.setState({ overlayBlur: 5 }).open();
}

function openInContentDrawer(placement: DrawerPlacement = 'right') {
  const state: Partial<DrawerState> = { class: '', placement };
  if (placement === 'top') {
    // 페이지 상단 영역의 z-index가 200이므로, 200보다 낮은 값을 설정하여 드로어가 상단에서 미끄러져 나올 때 적절하게 보이도록 합니다.
    state.zIndex = 199;
  }
  inContentDrawerApi.setState(state).open();
}

function openMaxContentDrawer() {
  // 여기서는 데모 편의를 위해 사용되었습니다. 실제 사용 시에는 이러한 설정을 Drawer의 속성에 직접 작성할 수 있습니다.
  inContentDrawerApi.setState({ class: 'w-full', placement: 'right' }).open();
}

function openAutoHeightDrawer() {
  autoHeightDrawerApi.open();
}

function openDynamicDrawer() {
  dynamicDrawerApi.open();
}

function handleUpdateTitle() {
  dynamicDrawerApi.setState({ title: '외부 동적 제목' }).open();
}

function openSharedDrawer() {
  sharedDrawerApi
    .setData({
      content: '외부에서 전달된 데이터 content',
      payload: '외부에서 전달된 데이터 payload',
    })
    .open();
}

function openFormDrawer() {
  formDrawerApi
    .setData({
      // 폼 값
      values: { field1: 'abc', field2: '123' },
    })
    .open();
}
</script>

<template>
  <Page
    auto-content-height
    description="드로어 컴포넌트는 일반적으로 현재 페이지에 오버레이를 표시하여 중요한 정보를 보여주거나 사용자 상호 작용 인터페이스를 제공하는 데 사용됩니다."
    title="드로어 컴포넌트 예시"
  >
    <template #extra>
      <DocButton path="/components/common-ui/vben-drawer" />
    </template>
    <BaseDrawer />
    <InContentDrawer />
    <AutoHeightDrawer />
    <DynamicDrawer />
    <SharedDataDrawer />
    <FormDrawer />

    <Card class="mb-4" title="기본 사용법">
      <p class="mb-3">기본적인 드로어 예시</p>
      <Button class="mb-2" type="primary" @click="openBaseDrawer('right')">
        오른쪽 열기
      </Button>
      <Button
        class="mb-2 ml-2"
        type="primary"
        @click="openBaseDrawer('bottom')"
      >
        하단 열기
      </Button>
      <Button class="mb-2 ml-2" type="primary" @click="openBaseDrawer('left')">
        왼쪽 열기
      </Button>
      <Button class="mb-2 ml-2" type="primary" @click="openBaseDrawer('top')">
        상단 열기
      </Button>
      <Button class="mb-2 ml-2" type="primary" @click="openBlurDrawer">
        마스크 레이어 블러 효과
      </Button>
    </Card>

    <Card class="mb-4" title="콘텐츠 영역에서 열기">
      <p class="mb-3">드로어가 콘텐츠 영역 내에서 열리도록 지정하며, 상단 및 왼쪽 메뉴 영역을 덮지 않습니다.</p>
      <Button class="mb-2" type="primary" @click="openInContentDrawer('right')">
        오른쪽 열기
      </Button>
      <Button
        class="mb-2 ml-2"
        type="primary"
        @click="openInContentDrawer('bottom')"
      >
        하단 열기
      </Button>
      <Button
        class="mb-2 ml-2"
        type="primary"
        @click="openInContentDrawer('left')"
      >
        왼쪽 열기
      </Button>
      <Button
        class="mb-2 ml-2"
        type="primary"
        @click="openInContentDrawer('top')"
      >
        상단 열기
      </Button>
      <Button class="mb-2 ml-2" type="primary" @click="openMaxContentDrawer">
        콘텐츠 영역 전체 화면 열기
      </Button>
    </Card>

    <Card class="mb-4" title="콘텐츠 높이 적응형 스크롤">
      <p class="mb-3">콘텐츠에 따라 스크롤 높이를 자동으로 계산할 수 있습니다.</p>
      <Button type="primary" @click="openAutoHeightDrawer">드로어 열기</Button>
    </Card>

    <Card class="mb-4" title="동적 설정 예시">
      <p class="mb-3">setState를 통해 드로어 데이터를 동적으로 조정합니다.</p>
      <Button type="primary" @click="openDynamicDrawer">드로어 열기</Button>
      <Button class="ml-2" type="primary" @click="handleUpdateTitle">
        외부에서 제목 수정 후 열기
      </Button>
    </Card>

    <Card class="mb-4" title="내외부 데이터 공유 예시">
      <p class="mb-3">sharedData를 공유하여 데이터 상호 작용을 수행합니다.</p>
      <Button type="primary" @click="openSharedDrawer">
        드로어 열기 및 데이터 전달
      </Button>
    </Card>

    <Card class="mb-4" title="폼 드로어 예시">
      <p class="mb-3">드로어를 열고 폼 스키마 및 데이터를 설정합니다.</p>
      <Button type="primary" @click="openFormDrawer">
        드로어 열기 및 폼 스키마/데이터 설정
      </Button>
    </Card>
  </Page>
</template>
