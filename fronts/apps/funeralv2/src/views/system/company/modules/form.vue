<script lang="ts" setup>
import { nextTick } from 'vue';
import { useVbenForm } from '#/adapter/form';
import { useVbenDrawer } from '@vben/common-ui';
import { $t } from '@vben/locales';
import { message } from 'ant-design-vue';
import { createCompany, updateCompany } from '#/api/system/company';
import { formSchema } from '../data';

const emit = defineEmits(['success']);

// formSchema가 함수형() => [] 이거나 객체형 { schema: [] } 인 경우를 모두 안전하게 배열로 변환합니다.
const resolvedSchema = typeof formSchema === 'function' 
  ? formSchema() 
  : (Array.isArray(formSchema) ? formSchema : (formSchema as any).schema || []);

const [Form, formApi] = useVbenForm({
  schema: resolvedSchema,
  // Drawer 내부에서 사용되므로 Form 자체의 기본 액션 버튼(Submit 등)은 숨깁니다.
  showDefaultActions: false, 
  handleSubmit: async (values) => {
    try {
      drawerApi.setState({ confirmLoading: true });
      // 2. formApi가 아닌 drawerApi에서 원본 데이터를 가져와야 합니다.
      const data = drawerApi.getData(); 
      if (data?.id) {
        await updateCompany(data.id, values);
        message.success($t('ui.actionMessage.updateSuccess', [values.name]));
      } else {
        await createCompany(values);
        message.success($t('ui.actionMessage.createSuccess', [values.name]));
      }
      drawerApi.close();
      emit('success');
    } catch (error) {
      console.error(error);
    } finally {
      drawerApi.setState({ confirmLoading: false });
    }
  },
});

const [Drawer, drawerApi] = useVbenDrawer({
  onCancel() {
    drawerApi.close();
  },
  onConfirm: async () => {
    await formApi.validateAndSubmitForm();
  },
  onOpenChange: async (isOpen) => {
    if (isOpen) {
      const data = drawerApi.getData();
      drawerApi.setState({ confirmLoading: false });

      // Form 컴포넌트가 완전히 렌더링될 때까지 대기
      await nextTick();

      if (data?.id) {
        drawerApi.setState({ title: $t('ui.actionTitle.edit', [$t('system.company.name')]) });
        formApi.setValues(data);
      } else {
        drawerApi.setState({ title: $t('ui.actionTitle.create', [$t('system.company.name')]) });
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
