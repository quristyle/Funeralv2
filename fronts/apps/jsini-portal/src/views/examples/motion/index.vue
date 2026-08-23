<script lang="ts" setup>
import { reactive } from 'vue';

import { Page } from '@vben/common-ui';
import { Motion, MotionGroup, MotionPresets } from '@vben/plugins/motion';

import { refAutoReset, watchDebounced } from '@vueuse/core';
import {
  Button,
  Card,
  Col,
  Form,
  FormItem,
  InputNumber,
  Row,
  Select,
} from 'ant-design-vue';
// 이 예시에서는 visible 유형의 애니메이션을 사용하지 않습니다. VisibleOnce 및 Visible 유형은 컴포넌트가 뷰포트에 들어와 표시될 때 애니메이션을 실행합니다.
const presets = MotionPresets.filter((v) => !v.includes('Visible'));
const showCard1 = refAutoReset(true, 100);
const showCard2 = refAutoReset(true, 100);
const showCard3 = refAutoReset(true, 100);
const motionProps = reactive({
  delay: 0,
  duration: 300,
  enter: { scale: 1 },
  hovered: { scale: 1.1, transition: { delay: 0, duration: 50 } },
  preset: 'fade',
  tapped: { scale: 0.9, transition: { delay: 0, duration: 50 } },
});

const motionGroupProps = reactive({
  delay: 0,
  duration: 300,
  enter: { scale: 1 },
  hovered: { scale: 1.1, transition: { delay: 0, duration: 50 } },
  preset: 'fade',
  tapped: { scale: 0.9, transition: { delay: 0, duration: 50 } },
});

watchDebounced(
  motionProps,
  () => {
    showCard2.value = false;
  },
  { debounce: 200, deep: true },
);

watchDebounced(
  motionGroupProps,
  () => {
    showCard3.value = false;
  },
  { debounce: 200, deep: true },
);

function openDocPage() {
  window.open('https://motion.vueuse.org/', '_blank');
}
</script>
<template>
  <Page title="Motion">
    <template #description>
      <span>다른 컴포넌트에 애니메이션 효과를 쉽게 부여할 수 있는 컴포넌트입니다.</span>
      <Button type="link" @click="openDocPage">문서 보기</Button>
    </template>
    <Card title="지시어 사용" :body-style="{ minHeight: '5rem' }">
      <template #extra>
        <Button type="primary" @click="showCard1 = false">다시 로드</Button>
      </template>
      <div>
        <div class="relative flex gap-2 overflow-hidden" v-if="showCard1">
          <Button v-motion-fade-visible>fade</Button>
          <Button v-motion-pop-visible :duration="500">pop</Button>
          <Button v-motion-slide-left>slide-left</Button>
          <Button v-motion-slide-right>slide-right</Button>
          <Button v-motion-slide-bottom>slide-bottom</Button>
          <Button v-motion-slide-top>slide-top</Button>
        </div>
      </div>
    </Card>
    <Card
      class="mt-2"
      title="컴포넌트 사용 (내부를 하나의 전체로 애니메이션 추가)"
      :body-style="{ padding: 0 }"
    >
      <div class="relative flex-center min-h-32 gap-2 overflow-hidden">
        <Motion
          v-bind="motionProps"
          v-if="showCard2"
          class="flex items-center gap-2"
        >
          <Button size="large">이 버튼은 표시될 때 애니메이션 효과가 나타납니다.</Button>
          <span>부속 컴포넌트, 전체적으로 애니메이션이 처리됩니다.</span>
        </Motion>
      </div>
      <div class="relative flex-center min-h-32 gap-2 overflow-hidden">
        <div v-if="showCard2" class="flex items-center gap-2">
          <span>순차 지연</span>
          <Motion
            v-bind="{
              ...motionProps,
              delay: motionProps.delay + 100 * i,
            }"
            v-for="i in 5"
            :key="i"
          >
            <Button size="large">버튼{{ i }}</Button>
          </Motion>
        </div>
      </div>
      <div>
        <Form :model="motionProps" :label-col="{ span: 10 }">
          <Row>
            <Col :span="8">
              <FormItem prop="preset" label="애니메이션 효과">
                <Select v-model:value="motionProps.preset">
                  <Select.Option
                    :value="preset"
                    v-for="preset in presets"
                    :key="preset"
                  >
                    {{ preset }}
                  </Select.Option>
                </Select>
              </FormItem>
            </Col>
            <Col :span="8">
              <FormItem prop="duration" label="지속 시간">
                <InputNumber v-model:value="motionProps.duration" />
              </FormItem>
            </Col>
            <Col :span="8">
              <FormItem prop="delay" label="애니메이션 지연">
                <InputNumber v-model:value="motionProps.delay" />
              </FormItem>
            </Col>
            <Col :span="8">
              <FormItem prop="hovered.scale" label="Hover 축척">
                <InputNumber v-model:value="motionProps.hovered.scale" />
              </FormItem>
            </Col>
            <Col :span="8">
              <FormItem prop="hovered.tapped" label="클릭 시 축척">
                <InputNumber v-model:value="motionProps.tapped.scale" />
              </FormItem>
            </Col>
          </Row>
        </Form>
      </div>
    </Card>
    <Card
      class="mt-2"
      title="그룹 애니메이션 (각 자식 요소에 동일한 독립 애니메이션 적용)"
      :body-style="{ padding: 0 }"
    >
      <div class="relative flex-center min-h-32 gap-2 overflow-hidden">
        <MotionGroup v-bind="motionGroupProps" v-if="showCard3">
          <Button size="large">버튼1</Button>
          <Button size="large">버튼2</Button>
          <Button size="large">버튼3</Button>
          <Button size="large">버튼4</Button>
          <Button size="large">버튼5</Button>
        </MotionGroup>
      </div>
      <div>
        <Form :model="motionGroupProps" :label-col="{ span: 10 }">
          <Row>
            <Col :span="8">
              <FormItem prop="preset" label="애니메이션 효과">
                <Select v-model:value="motionGroupProps.preset">
                  <Select.Option
                    :value="preset"
                    v-for="preset in presets"
                    :key="preset"
                  >
                    {{ preset }}
                  </Select.Option>
                </Select>
              </FormItem>
            </Col>
            <Col :span="8">
              <FormItem prop="duration" label="지속 시간">
                <InputNumber v-model:value="motionGroupProps.duration" />
              </FormItem>
            </Col>
            <Col :span="8">
              <FormItem prop="delay" label="애니메이션 지연">
                <InputNumber v-model:value="motionGroupProps.delay" />
              </FormItem>
            </Col>
            <Col :span="8">
              <FormItem prop="hovered.scale" label="Hover 축척">
                <InputNumber v-model:value="motionGroupProps.hovered.scale" />
              </FormItem>
            </Col>
            <Col :span="8">
              <FormItem prop="hovered.tapped" label="클릭 시 축척">
                <InputNumber v-model:value="motionGroupProps.tapped.scale" />
              </FormItem>
            </Col>
          </Row>
        </Form>
      </div>
    </Card>
  </Page>
</template>
