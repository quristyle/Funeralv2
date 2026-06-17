<script setup lang="ts">
import { ref } from 'vue';
import { useRouter } from 'vue-router';

import { $t } from '@vben/locales';

import { VbenButton } from '@vben-core/shadcn-ui';

import { useQRCode } from '@vueuse/integrations/useQRCode';

import Title from './auth-title.vue';

interface Props {
  /**
   * @ko_KR 로딩 처리 상태 여부
   */
  loading?: boolean;
  /**
   * @ko_KR 로그인 경로
   */
  loginPath?: string;
  /**
   * @ko_KR 제목
   */
  title?: string;
  /**
   * @ko_KR 부제목
   */
  subTitle?: string;
  /**
   * @ko_KR 버튼 텍스트
   */
  submitButtonText?: string;
  /**
   * @ko_KR 설명
   */
  description?: string;
  /**
   * @ko_KR 뒤로 가기 버튼 표시 여부
   */
  showBack?: boolean;
}

defineOptions({
  name: 'AuthenticationQrCodeLogin',
});

const props = withDefaults(defineProps<Props>(), {
  description: '',
  loading: false,
  showBack: true,
  loginPath: '/auth/login',
  submitButtonText: '',
  subTitle: '',
  title: '',
});

const router = useRouter();

const text = ref('https://vben.vvbin.cn');

const qrcode = useQRCode(text, {
  errorCorrectionLevel: 'H',
  margin: 4,
});

function goToLogin() {
  router.push(props.loginPath);
}
</script>

<template>
  <div>
    <Title>
      <slot name="title">
        {{ title || $t('authentication.welcomeBack') }} 📱
      </slot>
      <template #desc>
        <span class="text-muted-foreground">
          <slot name="subTitle">
            {{ subTitle || $t('authentication.qrcodeSubtitle') }}
          </slot>
        </span>
      </template>
    </Title>

    <div class="mt-6 flex-col-center">
      <img :src="qrcode" alt="qrcode" class="w-1/2" />
      <p class="mt-4 text-sm text-muted-foreground">
        <slot name="description">
          {{ description || $t('authentication.qrcodePrompt') }}
        </slot>
      </p>
    </div>

    <VbenButton
      v-if="showBack"
      class="mt-4 w-full"
      variant="outline"
      @click="goToLogin()"
    >
      {{ $t('common.back') }}
    </VbenButton>
  </div>
</template>
