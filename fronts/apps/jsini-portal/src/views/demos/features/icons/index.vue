<script lang="ts" setup>
import { h, ref } from 'vue';

import { IconPicker, Page } from '@vben/common-ui';
import {
  MdiKeyboardEsc,
  SvgAvatar1Icon,
  SvgAvatar2Icon,
  SvgAvatar3Icon,
  SvgAvatar4Icon,
  SvgBellIcon,
  SvgCakeIcon,
  SvgCardIcon,
  SvgDownloadIcon,
  SvgGithubIcon,
  SvgGoogleIcon,
  SvgKakaotalkIcon,
  SvgNaverIcon,
} from '@vben/icons';

import { Card, Input } from 'ant-design-vue';

const iconValue1 = ref('ant-design:trademark-outlined');
const iconValue2 = ref('svg:avatar-1');
const iconValue3 = ref('mdi:alien-outline');
const iconValue4 = ref('mdi-light:book-multiple');

const inputComponent = h(Input);
</script>

<template>
  <Page title="아이콘">
    <template #description>
      <div class="mt-2 text-foreground/80">
        아이콘은
        <a
          class="text-primary"
          href="https://icon-sets.iconify.design/"
          target="_blank"
        >
          Iconify
        </a>
        에서 찾을 수 있으며, Material Design, Font Awesome, Jam Icons 등 다양한 아이콘 라이브러리를 지원합니다.
      </div>
    </template>

    <Card class="mb-5" title="Iconify">
      <div class="flex items-center gap-5">
        <SvgGithubIcon class="size-8" />
        <SvgGoogleIcon class="size-8" />
        <SvgKakaotalkIcon class="size-8" />
        <SvgNaverIcon class="size-8" />
        <MdiKeyboardEsc class="size-8" />
      </div>
    </Card>

    <Card class="mb-5" title="Svg Icons">
      <div class="flex items-center gap-5">
        <SvgAvatar1Icon class="size-8" />
        <SvgAvatar2Icon class="size-8 text-red-500" />
        <SvgAvatar3Icon class="size-8 text-green-500" />
        <SvgAvatar4Icon class="size-8" />
        <SvgCakeIcon class="size-8" />
        <SvgBellIcon class="size-8" />
        <SvgCardIcon class="size-8" />
        <SvgDownloadIcon class="size-8" />
      </div>
    </Card>

    <Card class="mb-5" title="Tailwind CSS">
      <div class="flex items-center gap-5 text-3xl">
        <span class="icon-[ant-design--alipay-circle-outlined]"></span>
        <span class="icon-[ant-design--account-book-filled]"></span>
        <span class="icon-[ant-design--container-outlined]"></span>
        <span class="icon-[svg-spinners--wind-toy]"></span>
        <span class="icon-[svg-spinners--blocks-wave]"></span>
        <span class="icon-[line-md--compass-filled-loop]"></span>
      </div>
    </Card>

    <Card class="mb-5" title="아이콘 선택기">
      <div class="mb-5 flex items-center gap-5">
        <span>기본 스타일(Iconify):</span>
        <IconPicker v-model="iconValue1" class="w-50" />
      </div>
      <div class="mb-5 flex items-center gap-5">
        <span>기본 스타일(svg):</span>
        <IconPicker v-model="iconValue2" class="w-50" prefix="svg" />
      </div>
      <div class="mb-5 flex items-center gap-5">
        <span>사용자 정의 Input:</span>
        <IconPicker
          :input-component="inputComponent"
          v-model="iconValue3"
          icon-slot="addonAfter"
          model-value-prop="value"
          prefix="mdi"
        />
      </div>
      <div class="flex items-center gap-5">
        <span>아이콘으로 표시:</span>
        <Input
          v-model:value="iconValue4"
          allow-clear
          placeholder="아이콘을 선택하려면 여기를 클릭하세요"
          class="w-75"
        >
          <template #addonAfter>
            <IconPicker v-model="iconValue4" prefix="mdi-light" type="icon" />
          </template>
        </Input>
      </div>
    </Card>
  </Page>
</template>
