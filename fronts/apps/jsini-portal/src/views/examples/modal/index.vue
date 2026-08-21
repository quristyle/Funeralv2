<script lang="ts" setup>
import { onBeforeUnmount } from 'vue';

import {
  alert,
  clearAllAlerts,
  confirm,
  Page,
  prompt,
  useVbenModal,
} from '@vben/common-ui';

import { Button, Card, Flex, message } from 'ant-design-vue';

import DocButton from '../doc-button.vue';
import AutoHeightDemo from './auto-height-demo.vue';
import BaseDemo from './base-demo.vue';
import BlurDemo from './blur-demo.vue';
import DragDemo from './drag-demo.vue';
import DynamicDemo from './dynamic-demo.vue';
import FormModalDemo from './form-modal-demo.vue';
import InContentModalDemo from './in-content-demo.vue';
import NestedDemo from './nested-demo.vue';
import SharedDataDemo from './shared-data-demo.vue';

defineOptions({ name: 'ModalExample' });

const [BaseModal, baseModalApi] = useVbenModal({
  // 분리된 컴포넌트 연결
  connectedComponent: BaseDemo,
});

const [InContentModal, inContentModalApi] = useVbenModal({
  // 분리된 컴포넌트 연결
  connectedComponent: InContentModalDemo,
});

const [AutoHeightModal, autoHeightModalApi] = useVbenModal({
  connectedComponent: AutoHeightDemo,
});

const [DragModal, dragModalApi] = useVbenModal({
  connectedComponent: DragDemo,
});

const [DynamicModal, dynamicModalApi] = useVbenModal({
  connectedComponent: DynamicDemo,
});

const [SharedDataModal, sharedModalApi] = useVbenModal({
  connectedComponent: SharedDataDemo,
});

const [FormModal, formModalApi] = useVbenModal({
  connectedComponent: FormModalDemo,
});

const [NestedModal, nestedModalApi] = useVbenModal({
  connectedComponent: NestedDemo,
});

const [BlurModal, blurModalApi] = useVbenModal({
  connectedComponent: BlurDemo,
});

function openBaseModal() {
  baseModalApi.open();
}

function openInContentModal() {
  inContentModalApi.open();
}

function openAutoHeightModal() {
  autoHeightModalApi.open();
}

function openDragModal() {
  dragModalApi.open();
}

function openDynamicModal() {
  dynamicModalApi.open();
}

function openSharedModal() {
  sharedModalApi
    .setData({
      content: '외부에서 전달된 데이터 content',
      payload: '외부에서 전달된 데이터 payload',
    })
    .open();
}

function openNestedModal() {
  nestedModalApi.open();
}

function openBlurModal() {
  blurModalApi.open();
}

function handleUpdateTitle() {
  dynamicModalApi.setState({ title: '외부 동적 제목' }).open();
}

function openFormModal() {
  formModalApi
    .setData({
      // 폼 값
      values: { field1: 'abc', field2: '123' },
    })
    .open();
}

function openAlert() {
  alert({
    content: '이것은 팝업창입니다',
    icon: 'success',
  }).then(() => {
    message.info('사용자가 팝업창을 닫았습니다');
  });
}

onBeforeUnmount(() => {
  // 모든 팝업창 제거
  clearAllAlerts();
});

function openConfirm() {
  confirm({
    beforeClose({ isConfirm }) {
      if (!isConfirm) return;
      // 여기서 비동기 작업을 수행할 수 있습니다.
      return new Promise((resolve) => {
        setTimeout(() => {
          resolve(true);
        }, 1000);
      });
    },
    centered: false,
    content: '이것은 확인 팝업창입니다',
    icon: 'question',
  })
    .then(() => {
      message.success('사용자가 작업을 확인했습니다');
    })
    .catch(() => {
      message.error('사용자가 작업을 취소했습니다');
    });
}

async function openPrompt() {
  prompt<string>({
    async beforeClose({ isConfirm, value }) {
      if (isConfirm && value === '치즈') {
        message.error('치즈를 먹을 수 없습니다');
        return false;
      }
    },
    componentProps: { placeholder: '치즈를 먹을 수 없습니다...' },
    content: '점심에 무엇을 드셨나요?',
    icon: 'question',
    overlayBlur: 3,
  })
    .then((res) => {
      message.success(`사용자 입력: ${res}`);
    })
    .catch(() => {
      message.error('사용자가 입력을 취소했습니다');
    });
}
</script>

<template>
  <Page
    auto-content-height
    description="팝업 컴포넌트는 현재 페이지를 떠나지 않고 추가 정보, 폼 또는 작업 프롬프트를 표시하는 데 자주 사용됩니다. 더 많은 API는 컴포넌트 문서를 참조하세요."
    title="팝업 컴포넌트 예제"
  >
    <template #extra>
      <DocButton path="/components/common-ui/vben-modal" />
    </template>
    <BaseModal />
    <InContentModal />
    <AutoHeightModal />
    <DragModal />
    <DynamicModal />
    <SharedDataModal />
    <FormModal />
    <NestedModal />
    <BlurModal />
    <Flex wrap="wrap" class="w-full" gap="10">
      <Card class="w-75" title="기본 사용법">
        <p>기본적인 팝업 예제입니다.</p>
        <template #actions>
          <Button type="primary" @click="openBaseModal">팝업 열기</Button>
        </template>
      </Card>

      <Card class="w-75" title="컨테이너 지정 + 닫은 후 파괴되지 않음">
        <p>콘텐츠 영역에서 팝업을 여는 예제입니다.</p>
        <template #actions>
          <Button type="primary" @click="openInContentModal">팝업 열기</Button>
        </template>
      </Card>

      <Card class="w-75" title="콘텐츠 높이 자동 조절">
        <p>콘텐츠에 따라 자동으로 높이가 조절됩니다.</p>
        <template #actions>
          <Button type="primary" @click="openAutoHeightModal">
            팝업 열기
          </Button>
        </template>
      </Card>

      <Card class="w-75" title="드래그 가능 예제">
        <p>draggable 설정을 통해 드래그 기능을 활성화할 수 있습니다.</p>
        <template #actions>
          <Button type="primary" @click="openDragModal"> 팝업 열기 </Button>
        </template>
      </Card>

      <Card class="w-75" title="동적 설정 예제">
        <p>setState를 통해 팝업 데이터를 동적으로 조정합니다.</p>
        <template #extra>
          <Button type="link" @click="openDynamicModal">팝업 열기</Button>
        </template>
        <template #actions>
          <Button type="primary" @click="handleUpdateTitle">
            외부에서 제목 수정 후 열기
          </Button>
        </template>
      </Card>

      <Card class="w-75" title="내외부 데이터 공유 예제">
        <p>sharedData를 공유하여 데이터를 주고받습니다.</p>
        <template #actions>
          <Button type="primary" @click="openSharedModal">
            팝업 열기 및 데이터 전달
          </Button>
        </template>
      </Card>

      <Card class="w-75" title="폼 팝업 예제">
        <p>팝업과 폼의 결합</p>
        <template #actions>
          <Button type="primary" @click="openFormModal"> 폼 팝업 열기 </Button>
        </template>
      </Card>

      <Card class="w-75" title="중첩 팝업 예제">
        <p>이미 열려 있는 팝업 내에서 다시 팝업을 엽니다.</p>
        <template #actions>
          <Button type="primary" @click="openNestedModal">중첩 팝업 열기</Button>
        </template>
      </Card>

      <Card class="w-75" title="마스크 블러 예제">
        <p>마스크 레이어에 반투명 유리 같은 블러 효과를 적용합니다.</p>
        <template #actions>
          <Button type="primary" @click="openBlurModal">팝업 열기</Button>
        </template>
      </Card>
      <Card class="w-75" title="경량 알림 팝업">
        <template #extra>
          <DocButton path="/components/common-ui/vben-alert" />
        </template>
        <p>빠른 메서드를 통해 동적 알림 팝업을 생성하며, 간단한 알림, 확인, 입력 등에 적합합니다.</p>
        <template #actions>
          <Button type="primary" @click="openAlert">Alert</Button>
          <Button type="primary" @click="openConfirm">Confirm</Button>
          <Button type="primary" @click="openPrompt">Prompt</Button>
        </template>
      </Card>
    </Flex>
  </Page>
</template>
