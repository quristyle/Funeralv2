<script lang="ts" setup>
import { nextTick } from 'vue';
import { useVbenForm } from '#/adapter/form';
import { useVbenDrawer } from '@vben/common-ui';
import { $t } from '@vben/locales';
import { message } from 'ant-design-vue';
import { createCompany, updateCompany } from '#/api/system/company';
import { formSchema } from '../data';

/**
 * [회사 관리 - 등록/수정 폼 Drawer]
 * 신규 회사 등록 및 기존 회사 정보 수정을 처리합니다.
 */

/** 데이터 변경 완료 시 목록 갱신을 위한 emit 정의 */
const emit = defineEmits(['success']);

/**
 * data.ts에 정의된 formSchema를 안전하게 처리합니다.
 * (Vben Admin의 유연한 스키마 정의 방식을 모두 지원하도록 구성)
 */
const resolvedSchema = typeof formSchema === 'function' 
  ? formSchema() 
  : (Array.isArray(formSchema) ? formSchema : (formSchema as any).schema || []);

// 우편번호 찾기 컴포넌트 이벤트 맵핑 바인딩
const zipCodeField = resolvedSchema.find((item: any) => item.fieldName === 'zipCode');
if (zipCodeField) {
  zipCodeField.componentProps = {
    onSelected: (result: { zipCode: string; address: string }) => {
      formApi.setValues({
        zipCode: result.zipCode,
        address: result.address,
      });
    }
  };
}

/** VbenForm 설정 */
const [Form, formApi] = useVbenForm({
  schema: resolvedSchema,
  /** Drawer의 확인 버튼을 사용하므로 Form 자체 하단의 버튼은 숨깁니다. */
  showDefaultActions: false, 
  /** 폼 제출(Submit) 시 실행되는 비즈니스 로직 */
  handleSubmit: async (values) => {
    try {
      drawerApi.setState({ confirmLoading: true });
      
      /** 
       * Drawer가 열릴 때 주입된 원본 데이터를 가져옵니다.
       * ID가 존재하면 수정(Update), 없으면 등록(Create)으로 판단합니다.
       */
      const data = drawerApi.getData(); 
      if (data?.id) {
        await updateCompany(data.id, values);
        message.success($t('ui.actionMessage.updateSuccess', [values.name]));
      } else {
        await createCompany(values);
        message.success($t('ui.actionMessage.createSuccess', [values.name]));
      }
      
      /** 성공 시 Drawer를 닫고 목록을 새로고침합니다. */
      drawerApi.close();
      emit('success');
    } catch (error) {
      console.error('[Form Submit Error]', error);
    } finally {
      drawerApi.setState({ confirmLoading: false });
    }
  },
});

/** VbenDrawer 설정 (팝업 레이어 관리) */
const [Drawer, drawerApi] = useVbenDrawer({
  /** 취소 버튼 클릭 시 팝업 닫기 */
  onCancel() {
    drawerApi.close();
  },
  /** 확인 버튼 클릭 시 폼 검증 및 제출 실행 */
  onConfirm: async () => {
    await formApi.validateAndSubmitForm();
  },
  /** Drawer가 열리거나 닫힐 때의 상태 변화 감지 */
  onOpenChange: async (isOpen) => {
    if (isOpen) {
      const data = drawerApi.getData();
      drawerApi.setState({ confirmLoading: false });

      /** 폼 컴포넌트가 DOM에 완전히 렌더링될 때까지 대기 후 데이터 바인딩 */
      await nextTick();

      if (data?.id) {
        /** 수정 모드: 제목 변경 및 데이터 채우기 */
        drawerApi.setState({ title: $t('ui.actionTitle.edit', [$t('system.company.name')]) });
        formApi.setValues(data);
      } else {
        /** 등록 모드: 제목 변경 및 폼 초기화 */
        drawerApi.setState({ title: $t('ui.actionTitle.create', [$t('system.company.name')]) });
        formApi.resetForm();
      }
    }
  },
});
</script>

<template>
  <Drawer>
    <!-- VbenForm 컴포넌트 렌더링 -->
    <Form />
  </Drawer>
</template>
