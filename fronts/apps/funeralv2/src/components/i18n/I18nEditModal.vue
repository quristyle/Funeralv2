<script lang="ts" setup>
import { ref } from 'vue';
import { Modal, Form, Input, message } from 'ant-design-vue';
import { getI18nPaged, createI18nResource, updateI18nResource } from '#/api/system/i18n';
import { updateLocalI18n } from '#/locales';

interface OpenParams {
  id: string;
  key: string;
  category?: string;
  onSuccess?: (key: string) => Promise<void> | void;
}

const emit = defineEmits<{
  success: [key: string];
}>();

const visible = ref(false);
const isLoading = ref(false);
const i18nKey = ref('');
const categoryName = ref('menu');
const i18nValues = ref({
  'ko-KR': { id: null as number | null, value: '' },
  'en-US': { id: null as number | null, value: '' }
});

let successCallback: ((key: string) => Promise<void> | void) | undefined = undefined;

/**
 * 다국어 편집 모달을 엽니다.
 */
async function open(params: OpenParams) {
  i18nKey.value = params.key;
  categoryName.value = params.category || 'menu';
  successCallback = params.onSuccess;

  i18nValues.value['ko-KR'] = { id: null, value: '' };
  i18nValues.value['en-US'] = { id: null, value: '' };

  visible.value = true;
  isLoading.value = true;

  try {
    // 성능 최적화: 특정 key에 연관된 리소스만 조회
    const result = await getI18nPaged({ page: 1, pageSize: 50, key: i18nKey.value });
    const matches = (result?.items || []).filter((item: any) => item.key === i18nKey.value);

    matches.forEach((item: any) => {
      if (item.locale === 'ko-KR' || item.locale === 'en-US') {
        i18nValues.value[item.locale as 'ko-KR' | 'en-US'] = { id: item.id, value: item.value };
      }
    });
  } catch (error) {
    console.error(error);
    message.error('다국어 데이터를 로드하는 중 오류가 발생했습니다.');
  } finally {
    isLoading.value = false;
  }
}

/**
 * 다국어 번역 정보를 저장합니다.
 */
async function onSave() {
  if (!i18nKey.value.trim()) {
    message.warning('다국어 번역 키를 입력해주세요.');
    return;
  }

  isLoading.value = true;

  try {
    const key = i18nKey.value.trim();
    const category = categoryName.value;

    // 한국어와 영어 각각 번역 정보 생성/수정
    for (const locale of ['ko-KR', 'en-US'] as const) {
      const data = i18nValues.value[locale];

      if (data.id !== null) {
        // 이미 번역이 존재하는 경우 업데이트
        await updateI18nResource(data.id, {
          key,
          locale,
          value: data.value,
          category
        });
      } else if (data.value.trim()) {
        // 새로 추가하는 경우 생성
        await createI18nResource({
          key,
          locale,
          value: data.value,
          category
        });
      }

      // 로컬 다국어 정보 저장소(vue-i18n 캐시) 즉시 갱신 반영
      updateLocalI18n(locale, key, data.value);
    }

    // 도메인 엔티티 업데이트 등 추가 콜백 처리
    if (successCallback) {
      await successCallback(key);
    }

    message.success('다국어 번역 정보가 저장되었습니다.');
    visible.value = false;
    emit('success', key);
  } catch (error) {
    console.error(error);
    message.error('다국어 번역 정보 저장에 실패했습니다.');
  } finally {
    isLoading.value = false;
  }
}

defineExpose({
  open
});
</script>

<template>
  <Modal
    v-model:open="visible"
    title="다국어 편집"
    :confirm-loading="isLoading"
    @ok="onSave"
    :destroy-on-close="true"
  >
    <Form layout="vertical" class="mt-4">
      <Form.Item label="다국어 번역 키 (Key)">
        <Input v-model:value="i18nKey" :disabled="true" placeholder="예: menu.system.management" />
      </Form.Item>
      <Form.Item label="한국어 (ko-KR)">
        <Input v-model:value="i18nValues['ko-KR'].value" placeholder="한국어 명칭을 입력하세요" />
      </Form.Item>
      <Form.Item label="영어 (en-US)">
        <Input v-model:value="i18nValues['en-US'].value" placeholder="영어 명칭을 입력하세요 (English)" />
      </Form.Item>
    </Form>
  </Modal>
</template>
