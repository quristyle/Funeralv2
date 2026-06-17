<script lang="ts" setup>
import type { CountToProps, TransitionPresets } from '@vben/common-ui';

import { reactive } from 'vue';

import { CountTo, Page, TransitionPresetsKeys } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import {
  Button,
  Card,
  Col,
  Form,
  FormItem,
  Input,
  InputNumber,
  message,
  Row,
  Select,
  Switch,
} from 'ant-design-vue';

const props = reactive<CountToProps & { transition: TransitionPresets }>({
  decimal: '.',
  decimals: 2,
  decimalStyle: {
    fontSize: 'small',
    fontStyle: 'italic',
  },
  delay: 0,
  disabled: false,
  duration: 2000,
  endVal: 100_000,
  mainStyle: {
    color: 'hsl(var(--primary))',
    fontSize: 'xx-large',
    fontWeight: 'bold',
  },
  prefix: '₩',
  prefixStyle: {
    paddingRight: '0.5rem',
  },
  separator: ',',
  startVal: 0,
  suffix: '원',
  suffixStyle: {
    paddingLeft: '0.5rem',
  },
  transition: 'easeOutQuart',
});

function changeNumber() {
  props.endVal =
    Math.floor(Math.random() * 100_000_000) / 10 ** (props.decimals || 0);
}

function openDocumentation() {
  window.open('https://vueuse.org/core/useTransition/', '_blank');
}

function onStarted() {
  message.loading({
    content: '애니메이션 시작됨',
    duration: 0,
    key: 'animator-info',
  });
}

function onFinished() {
  message.success({
    content: '애니메이션 종료됨',
    duration: 2,
    key: 'animator-info',
  });
}
</script>
<template>
  <Page title="CountTo" description="숫자 스크롤 애니메이션 컴포넌트.">
    <template #description>
      <span>
        useTransition을 사용하여 캡슐화된 숫자 스크롤 애니메이션 컴포넌트로, 현재 값을 변경할 때마다 트랜지션 애니메이션이 생성됩니다.
      </span>
      <Button type="link" @click="openDocumentation">
        useTransition 문서 보기
      </Button>
    </template>
    <Card title="기본 용법">
      <div class="flex-center w-full pb-4">
        <CountTo v-bind="props" @started="onStarted" @finished="onFinished" />
      </div>
      <Form :model="props">
        <Row :gutter="20">
          <Col :span="8">
            <FormItem label="시작 값" name="startVal">
              <InputNumber v-model:value="props.startVal" />
            </FormItem>
          </Col>
          <Col :span="8">
            <FormItem label="현재 값" name="endVal">
              <InputNumber
                v-model:value="props.endVal"
                class="w-full"
                :precision="props.decimals"
              >
                <template #addonAfter>
                  <IconifyIcon
                    v-tippy="`랜덤 값 설정`"
                    class="size-5 cursor-pointer outline-hidden"
                    icon="ix:random-filled"
                    @click="changeNumber"
                  />
                </template>
              </InputNumber>
            </FormItem>
          </Col>
          <Col :span="8">
            <FormItem label="애니메이션 비활성화" name="disabled">
              <Switch v-model="props.disabled" />
            </FormItem>
          </Col>
          <Col :span="8">
            <FormItem label="애니메이션 지연" name="delay">
              <InputNumber v-model:value="props.delay" :min="0" />
            </FormItem>
          </Col>
          <Col :span="8">
            <FormItem label="지속 시간" name="duration">
              <InputNumber v-model:value="props.duration" :min="0" />
            </FormItem>
          </Col>

          <Col :span="8">
            <FormItem label="소수점 자릿수" name="decimals">
              <InputNumber
                v-model:value="props.decimals"
                :min="0"
                :precision="0"
              />
            </FormItem>
          </Col>
          <Col :span="8">
            <FormItem label="구분자" name="separator">
              <Input v-model:value="props.separator" />
            </FormItem>
          </Col>
          <Col :span="8">
            <FormItem label="소수점" name="decimal">
              <Input v-model:value="props.decimal" />
            </FormItem>
          </Col>
          <Col :span="8">
            <FormItem label="애니메이션" name="transition">
              <Select v-model:value="props.transition">
                <Select.Option
                  v-for="preset in TransitionPresetsKeys"
                  :key="preset"
                  :value="preset"
                >
                  {{ preset }}
                </Select.Option>
              </Select>
            </FormItem>
          </Col>
          <Col :span="8">
            <FormItem label="접두사" name="prefix">
              <Input v-model:value="props.prefix" />
            </FormItem>
          </Col>
          <Col :span="8">
            <FormItem label="접미사" name="suffix">
              <Input v-model:value="props.suffix" />
            </FormItem>
          </Col>
        </Row>
      </Form>
    </Card>
  </Page>
</template>
