<script lang="ts" setup>
import { nextTick } from 'vue';
import { useVbenForm } from '#/adapter/form';
import { useVbenDrawer } from '@vben/common-ui';
import { message } from 'ant-design-vue';
import { createBizSelectConfig, updateBizSelectConfig } from '#/api/system/biz-select-config';
import { useBizSelectStore } from '#/store/biz-select-config';
import { formSchema } from '../data';

/**
 * [BizSelect 설정 관리 - 등록/수정 폼 Drawer]
 */

const emit = defineEmits(['success']);
const bizSelectStore = useBizSelectStore();

const resolvedSchema = typeof formSchema === 'function' 
  ? formSchema() 
  : (Array.isArray(formSchema) ? formSchema : (formSchema as any).schema || []);

const [Form, formApi] = useVbenForm({
  schema: resolvedSchema,
  showDefaultActions: false, 
  handleSubmit: async (values) => {
    try {
      drawerApi.setState({ confirmLoading: true });
      
      const data = drawerApi.getData(); 
      if (data?.id) {
        await updateBizSelectConfig(data.id, values);
        message.success(`"${values.bizType}" 설정을 수정하였습니다.`);
      } else {
        await createBizSelectConfig(values);
        message.success(`"${values.bizType}" 설정을 새로 등록하였습니다.`);
      }
      
      // 메타데이터가 변경되었으므로 Pinia 전역 스토어 캐시를 갱신합니다.
      await bizSelectStore.loadConfigs(true);
      
      drawerApi.close();
      emit('success');
    } catch (error) {
      console.error('[Form Submit Error]', error);
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

      await nextTick();

      if (data?.id) {
        drawerApi.setState({ title: 'BizSelect 설정 수정' });
        formApi.setValues(data);
      } else {
        drawerApi.setState({ title: 'BizSelect 설정 등록' });
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
