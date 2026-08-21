<script lang="ts" setup>
import type {
  CaptchaVerifyPassingData,
  SliderCaptchaActionType,
} from '@vben/common-ui';

import { ref } from 'vue';

import { Page, SliderCaptcha } from '@vben/common-ui';
import { Bell, Sun } from '@vben/icons';

import { Button, Card, message } from 'ant-design-vue';

function handleSuccess(data: CaptchaVerifyPassingData) {
  const { time } = data;
  message.success(`검증 성공, ${time}초 소요됨`);
}
function handleBtnClick(elRef?: SliderCaptchaActionType) {
  if (!elRef) {
    return;
  }
  elRef.resume();
}

const el1 = ref<SliderCaptchaActionType>();
const el2 = ref<SliderCaptchaActionType>();
const el3 = ref<SliderCaptchaActionType>();
const el4 = ref<SliderCaptchaActionType>();
const el5 = ref<SliderCaptchaActionType>();
const el6 = ref<SliderCaptchaActionType>();
</script>

<template>
  <Page description="프론트엔드의 간단한 드래그 검증 시나리오에 사용됩니다." title="슬라이더 검증">
    <Card class="mb-5" title="기본 예제">
      <div class="flex-center p-4 px-[30%]">
        <SliderCaptcha ref="el1" @success="handleSuccess" />
        <Button class="ml-2" type="primary" @click="handleBtnClick(el1)">
          복원
        </Button>
      </div>
    </Card>
    <Card class="mb-5" title="사용자 정의 둥근 모서리">
      <div class="flex-center p-4 px-[30%]">
        <SliderCaptcha
          ref="el2"
          class="rounded-full"
          @success="handleSuccess"
        />
        <Button class="ml-2" type="primary" @click="handleBtnClick(el2)">
          복원
        </Button>
      </div>
    </Card>
    <Card class="mb-5" title="사용자 정의 배경색">
      <div class="flex-center p-4 px-[30%]">
        <SliderCaptcha
          ref="el3"
          :bar-style="{
            backgroundColor: '#018ffb',
          }"
          success-text="검증 성공"
          text="드래그하여 검증을 진행하세요"
          @success="handleSuccess"
        />
        <Button class="ml-2" type="primary" @click="handleBtnClick(el3)">
          복원
        </Button>
      </div>
    </Card>
    <Card class="mb-5" title="사용자 정의 드래그 아이콘">
      <div class="flex-center p-4 px-[30%]">
        <SliderCaptcha ref="el4" @success="handleSuccess">
          <template #actionIcon="{ isPassing }">
            <Bell v-if="isPassing" />
            <Sun v-else />
          </template>
        </SliderCaptcha>
        <Button class="ml-2" type="primary" @click="handleBtnClick(el4)">
          복원
        </Button>
      </div>
    </Card>
    <Card class="mb-5" title="사용자 정의 텍스트">
      <div class="flex-center p-4 px-[30%]">
        <SliderCaptcha
          ref="el5"
          success-text="성공"
          text="드래그"
          @success="handleSuccess"
        />
        <Button class="ml-2" type="primary" @click="handleBtnClick(el5)">
          복원
        </Button>
      </div>
    </Card>
    <Card class="mb-5" title="사용자 정의 콘텐츠(slot)">
      <div class="flex-center p-4 px-[30%]">
        <SliderCaptcha ref="el6" @success="handleSuccess">
          <template #text="{ isPassing }">
            <template v-if="isPassing">
              <Bell class="mr-2 size-4" />
              성공
            </template>
            <template v-else>
              드래그
              <Sun class="ml-2 size-4" />
            </template>
          </template>
        </SliderCaptcha>
        <Button class="ml-2" type="primary" @click="handleBtnClick(el6)">
          복원
        </Button>
      </div>
    </Card>
  </Page>
</template>
