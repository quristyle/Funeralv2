<script lang="ts" setup>
import type { TippyProps } from '@vben/common-ui';

import { reactive } from 'vue';

import { Page, Tippy } from '@vben/common-ui';

import { Button, Card, Flex } from 'ant-design-vue';

import { useVbenForm } from '#/adapter/form';

const tippyProps = reactive<TippyProps>({
  animation: 'shift-away',
  arrow: true,
  content: '이것은 힌트입니다',
  delay: [200, 200],
  duration: 200,
  followCursor: false,
  hideOnClick: false,
  inertia: true,
  maxWidth: 'none',
  placement: 'top',
  theme: 'dark',
  trigger: 'mouseenter focusin',
});

function parseBoolean(value: string) {
  switch (value) {
    case 'false': {
      return false;
    }
    case 'true': {
      return true;
    }
    default: {
      return value;
    }
  }
}

const [Form] = useVbenForm({
  handleValuesChange(values) {
    Object.assign(tippyProps, {
      ...values,
      delay: [values.delay1, values.delay2],
      followCursor: parseBoolean(values.followCursor),
      hideOnClick: parseBoolean(values.hideOnClick),
      trigger: values.trigger.join(' '),
    });
  },
  schema: [
    {
      component: 'RadioGroup',
      componentProps: {
        buttonStyle: 'solid',
        class: 'w-full',
        options: [
          { label: '자동', value: 'auto' },
          { label: '어두움', value: 'dark' },
          { label: '밝음', value: 'light' },
        ],
        optionType: 'button',
      },
      defaultValue: tippyProps.theme,
      fieldName: 'theme',
      label: '테마',
    },
    {
      component: 'Select',
      componentProps: {
        class: 'w-full',
        options: [
          { label: '위로 슬라이드', value: 'shift-away' },
          { label: '아래로 슬라이드', value: 'shift-toward' },
          { label: '확대/축소', value: 'scale' },
          { label: '원근법', value: 'perspective' },
          { label: '페이드 인', value: 'fade' },
        ],
      },
      defaultValue: tippyProps.animation,
      fieldName: 'animation',
      label: '애니메이션 유형',
    },
    {
      component: 'RadioGroup',
      componentProps: {
        buttonStyle: 'solid',
        options: [
          { label: '예', value: true },
          { label: '아니오', value: false },
        ],
        optionType: 'button',
      },
      defaultValue: tippyProps.inertia,
      fieldName: 'inertia',
      label: '애니메이션 관성',
    },
    {
      component: 'Select',
      componentProps: {
        class: 'w-full',
        options: [
          { label: '상단', value: 'top' },
          { label: '상단 왼쪽', value: 'top-start' },
          { label: '상단 오른쪽', value: 'top-end' },
          { label: '하단', value: 'bottom' },
          { label: '하단 왼쪽', value: 'bottom-start' },
          { label: '하단 오른쪽', value: 'bottom-end' },
          { label: '왼쪽', value: 'left' },
          { label: '왼쪽 상단', value: 'left-start' },
          { label: '왼쪽 하단', value: 'left-end' },
          { label: '오른쪽', value: 'right' },
          { label: '오른쪽 상단', value: 'right-start' },
          { label: '오른쪽 하단', value: 'right-end' },
        ],
      },
      defaultValue: tippyProps.placement,
      fieldName: 'placement',
      label: '위치',
    },
    {
      component: 'InputNumber',
      componentProps: {
        addonAfter: 'ms',
      },
      defaultValue: tippyProps.duration,
      fieldName: 'duration',
      label: '애니메이션 시간',
    },
    {
      component: 'InputNumber',
      componentProps: {
        addonAfter: 'ms',
      },
      defaultValue: 100,
      fieldName: 'delay1',
      label: '표시 지연',
    },
    {
      component: 'InputNumber',
      componentProps: {
        addonAfter: 'ms',
      },
      defaultValue: 100,
      fieldName: 'delay2',
      label: '숨김 지연',
    },
    {
      component: 'Input',
      defaultValue: tippyProps.content,
      fieldName: 'content',
      label: '내용',
    },
    {
      component: 'RadioGroup',
      componentProps: {
        buttonStyle: 'solid',
        options: [
          { label: '예', value: true },
          { label: '아니오', value: false },
        ],
        optionType: 'button',
      },
      defaultValue: tippyProps.arrow,
      fieldName: 'arrow',
      label: '화살표 표시',
    },
    {
      component: 'Select',
      componentProps: {
        class: 'w-full',
        options: [
          { label: '따라가지 않음', value: 'false' },
          { label: '완전히 따라감', value: 'true' },
          { label: '가로만', value: 'horizontal' },
          { label: '세로만', value: 'vertical' },
          { label: '초기만', value: 'initial' },
        ],
      },
      defaultValue: tippyProps.followCursor?.toString(),
      fieldName: 'followCursor',
      label: '마우스 커서 추적',
    },
    {
      component: 'Select',
      componentProps: {
        class: 'w-full',
        mode: 'multiple',
        options: [
          { label: '마우스 오버', value: 'mouseenter' },
          { label: '클릭됨', value: 'click' },
          { label: '포커스', value: 'focusin' },
          { label: '수동', value: 'manual' },
        ],
      },
      defaultValue: tippyProps.trigger?.split(' '),
      fieldName: 'trigger',
      label: '트리거 방식',
    },
    {
      component: 'Select',
      componentProps: {
        class: 'w-full',
        options: [
          { label: '아니오', value: 'false' },
          { label: '예', value: 'true' },
          { label: '토글', value: 'toggle' },
        ],
      },
      defaultValue: tippyProps.hideOnClick?.toString(),
      dependencies: {
        componentProps(_, formAction) {
          return {
            disabled: !formAction.values.trigger.includes('click'),
          };
        },
        triggerFields: ['trigger'],
      },
      fieldName: 'hideOnClick',
      help: '트리거 방식이 `click`일 때만 유효합니다.',
      label: '클릭 후 숨김',
    },
    {
      component: 'Input',
      componentProps: {
        allowClear: true,
        placeholder: 'none, 200px',
      },
      defaultValue: tippyProps.maxWidth,
      fieldName: 'maxWidth',
      label: '최대 너비',
    },
  ],
  showDefaultActions: false,
  wrapperClass: 'grid-cols-1 md:grid-cols-2 lg:grid-cols-3',
});

function goDoc() {
  window.open('https://atomiks.github.io/tippyjs/v6/all-props/');
}
</script>
<template>
  <Page title="Tippy">
    <template #description>
      <div class="flex items-center">
        <p>
          Tippy는 툴팁, 가이드 등 다양한 대화형 힌트를 만드는 데 사용할 수 있는 경량 힌트 도구 라이브러리입니다.
        </p>
        <Button type="link" size="small" @click="goDoc">문서 보기</Button>
      </div>
    </template>
    <Card title="지시어 형식 사용">
      <p class="mb-4">
        지시어 형식은 간결하며, 툴팁을 표시해야 하는 컴포넌트에 직접 v-tippy를 사용하여 설정을 전달합니다. 고정된 내용의 툴팁에 적합합니다.
      </p>
      <Flex warp="warp" gap="20" align="center">
        <Button v-tippy="'기본 설정을 사용한 힌트입니다.'">기본 설정</Button>

        <Button
          v-tippy="{ theme: 'light', content: '항상 light 테마인 힌트입니다.' }"
        >
          테마 지정
        </Button>
        <Button
          v-tippy="{
            theme: 'light',
            content: '이 힌트는 컴포넌트 활성화 100ms 후에 표시됩니다.',
            delay: 100,
          }"
        >
          지연 지정
        </Button>
        <Button
          v-tippy="{
            content: '이 힌트의 애니메이션은 `scale`입니다.',
            animation: 'scale',
          }"
        >
          애니메이션 지정
        </Button>
      </Flex>
    </Card>
    <Card title="컴포넌트 형식 사용" class="mt-4">
      <div class="flex w-full justify-center">
        <Tippy v-bind="tippyProps">
          <Button>이 컴포넌트에 마우스를 올려 효과를 체험해 보세요.</Button>
        </Tippy>
      </div>

      <Form class="mt-4" />
      <template #actions>
        <p
          class="cursor-default text-secondary-foreground hover:text-secondary-foreground"
        >
          더 많은 설정은
          <Button type="link" size="small" @click="goDoc">문서 보기</Button>
          를 참조하세요. 여기에는 자주 사용되는 일부 설정만 나열되어 있습니다.
        </p>
      </template>
    </Card>
  </Page>
</template>
