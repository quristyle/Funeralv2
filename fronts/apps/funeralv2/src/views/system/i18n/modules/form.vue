<script lang="ts" setup>
import { nextTick } from 'vue';
import { useVbenForm, type VbenFormProps } from '#/adapter/form';
import { useVbenDrawer } from '@vben/common-ui';
import { $t } from '@vben/locales';
import { message } from 'ant-design-vue';
import { createI18nResource, updateI18nResource, type SystemI18nApi } from '#/api/system/i18n';

const emit = defineEmits(['success']);

const [Form, formApi] = useVbenForm({
  commonConfig: {
    componentProps: {
      class: 'w-full',
    },
    labelWidth: 100,
  },
  schema: [
    {
      component: 'Select',
      componentProps: {
        options: [
          { label: $t('ui.i18n.koKR'), value: 'ko-KR' },
          { label: $t('ui.i18n.enUS'), value: 'en-US' },
        ],
      },
      fieldName: 'locale', label: $t('ui.i18n.locale'), rules: 'required', },
    { component: 'Input', fieldName: 'category', label: $t('ui.i18n.category'), rules: 'required', },
    { component: 'Input', fieldName: 'key', label: $t('ui.i18n.key'), rules: 'required', },
    { component: 'Textarea', fieldName: 'value', label: $t('ui.i18n.value'), rules: 'required',
      componentProps: { rows: 4, },
    },
  ],
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
      } else {
        drawerApi.setState({ title: $t('ui.actionTitle.create', ['I18n']) });
        formApi.resetForm();
      }
    }
  },
});
</script>

<template>
  <Drawer>
    <Form />
  </Drawer>
</template>
