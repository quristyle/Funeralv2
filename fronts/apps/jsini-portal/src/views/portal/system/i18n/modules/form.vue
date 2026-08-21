<script lang="ts" setup>
import { ref, nextTick } from 'vue';
import { useVbenForm, type VbenFormProps } from '#/adapter/form';
import { useVbenDrawer } from '@vben/common-ui';
import { $t } from '@vben/locales';
import { message } from 'ant-design-vue';
import { createI18nResource, updateI18nResource, type SystemI18nApi, suggestI18nTranslation } from '#/api/system/i18n';
import AiCodeSuggester from '#/components/ai-code-suggester/ai-code-suggester.vue';

const emit = defineEmits(['success']);

const localeVal = ref('');
const keyVal = ref('');

const schema = [
  {
    component: 'Select',
    componentProps: {
      options: [
        { label: $t('ui.i18n.koKR'), value: 'ko-KR' },
        { label: $t('ui.i18n.enUS'), value: 'en-US' },
      ],
      onChange: (val: any) => {
        localeVal.value = val;
      },
    },
    fieldName: 'locale',
    label: $t('ui.i18n.locale'),
    rules: 'required',
  },
  {
    component: 'Input',
    fieldName: 'category',
    label: $t('ui.i18n.category'),
    rules: 'required',
  },
  {
    component: 'Input',
    componentProps: {
      onChange: (e: any) => {
        keyVal.value = e?.target?.value || e;
      },
      onInput: (e: any) => {
        keyVal.value = e?.target?.value || e;
      },
    },
    fieldName: 'key',
    label: $t('ui.i18n.key'),
    rules: 'required',
  },
  {
    component: 'Textarea',
    fieldName: 'value',
    label: $t('ui.i18n.value'),
    rules: 'required',
    componentProps: {
      rows: 4,
    },
  },
];

const [Form, formApi] = useVbenForm({
  commonConfig: {
    componentProps: {
      class: 'w-full',
    },
    labelWidth: 100,
  },
  schema: schema,
  showDefaultActions: false,
} as VbenFormProps);

const [Drawer, drawerApi] = useVbenDrawer({
  onCancel: () => drawerApi.close(),
  onConfirm: async () => {
    try {
      drawerApi.setState({ confirmLoading: true });
      
      // 1. 유효성 검사 통과 여부 확인
      const { valid } = await formApi.validate();
      if (!valid) return;
      
      // 2. 실제 폼 입력 데이터 추출
      const formValues = await formApi.getValues();
      const data = drawerApi.getData<SystemI18nApi.I18nResource>();
     
      if (data?.id) {
        await updateI18nResource(data.id, formValues as any);
        message.success($t('ui.actionMessage.operationSuccess'));
      } else {
        await createI18nResource(formValues as any);
        message.success($t('ui.actionMessage.operationSuccess'));
      }
      
      emit('success');
      drawerApi.close();
    } catch (error) {
      console.error(error);
    } finally {
      drawerApi.setState({ confirmLoading: false });
    }
  },
  onOpenChange: async (isOpen) => {
    if (isOpen) {
      const data = drawerApi.getData<SystemI18nApi.I18nResource>();
      
      // Form 컴포넌트가 완전히 렌더링될 때까지 대기
      await nextTick();

      if (data?.id) {
        drawerApi.setState({ title: $t('ui.actionTitle.edit', ['I18n']) });
        formApi.setValues(data);
        localeVal.value = data.locale || '';
        keyVal.value = data.key || '';
      } else {
        drawerApi.setState({ title: $t('ui.actionTitle.create', ['I18n']) });
        formApi.resetForm();
        localeVal.value = '';
        keyVal.value = '';
      }
    } else {
      localeVal.value = '';
      keyVal.value = '';
    }
  },
});
</script>

<template>
  <Drawer>
    <Form />
    <AiCodeSuggester
      v-if="keyVal && localeVal"
      :key="localeVal + '_' + keyVal"
      :input-text="keyVal"
      :suggest-api="(text) => suggestI18nTranslation(text, localeVal)"
      :label="localeVal === 'ko-KR' ? 'AI 추천 한글' : 'AI 추천 영문'"
      @select="(val) => formApi.setFieldValue('value', val)"
    />
  </Drawer>
</template>
