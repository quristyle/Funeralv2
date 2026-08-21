<script lang="ts" setup>
import type { DataNode } from 'ant-design-vue/es/tree';

import type { Recordable } from '@vben/types';

import type { SystemRoleApi } from '#/api/portal/system/role';

import { computed, nextTick, ref } from 'vue';

import { Tree, useVbenDrawer } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';

import { Spin } from 'ant-design-vue';

import { useVbenForm, z } from '#/adapter/form';
import { getMenuList } from '#/api/portal/system/menu';
import { createRole, updateRole, isRoleIdExists } from '#/api/portal/system/role';
import { $t } from '#/locales';
import AiCodeSuggester from '#/components/ai-code-suggester/ai-code-suggester.vue';

import { useFormSchema } from '../data';

const emits = defineEmits(['success']);

const formData = ref<SystemRoleApi.SystemRole>();
const roleNameVal = ref('');

const schema = useFormSchema().map((item) => {
  if (item.fieldName === 'name') {
    return {
      ...item,
      componentProps: {
        ...item.componentProps,
        onChange: (e: any) => {
          roleNameVal.value = e?.target?.value || e;
        },
        onInput: (e: any) => {
          roleNameVal.value = e?.target?.value || e;
        },
      },
    };
  }
  return item;
});

const [Form, formApi] = useVbenForm({
  schema: schema,
  showDefaultActions: false,
});

const permissions = ref<DataNode[]>([]);
const loadingPermissions = ref(false);

const id = ref();
const [Drawer, drawerApi] = useVbenDrawer({
  async onConfirm() {
    const { valid } = await formApi.validate();
    if (!valid) return;
    const values = await formApi.getValues();
    drawerApi.lock();
    (id.value ? updateRole(id.value, values as any) : createRole(values as any))
      .then(() => {
        emits('success');
        drawerApi.close();
      })
      .catch(() => {
        drawerApi.unlock();
      });
  },

  async onOpenChange(isOpen) {
    if (isOpen) {
      const data = drawerApi.getData<SystemRoleApi.SystemRole>();
      formApi.resetForm();

      if (data && data.id) {
        formData.value = data;
        id.value = data.id;
        roleNameVal.value = data.name || '';
        formApi.updateSchema([
          {
            fieldName: 'id',
            componentProps: {
              disabled: true,
            },
            rules: z.string(),
          },
        ]);
      } else {
        id.value = undefined;
        roleNameVal.value = '';
        formApi.updateSchema([
          {
            fieldName: 'id',
            componentProps: {
              disabled: false,
            },
            rules: z.string()
              .min(1, '역할 ID를 입력해주세요')
              .refine(
                async (val) => {
                  return !(await isRoleIdExists(val));
                },
                (val) => ({
                  message: `이미 존재하는 역할 ID입니다: ${val}`
                })
              ),
          },
        ]);
      }

      if (permissions.value.length === 0) {
        await loadPermissions();
      }
      // Wait for Vue to flush DOM updates (form fields mounted)
      await nextTick();
      if (data) {
        formApi.setValues(data);
      }
    }
  },
});

async function loadPermissions() {
  loadingPermissions.value = true;
  try {
    const res = await getMenuList();
    permissions.value = res as unknown as DataNode[];
  } finally {
    loadingPermissions.value = false;
  }
}

const getDrawerTitle = computed(() => {
  return formData.value?.id
    ? $t('common.edit', $t('system.role.name'))
    : $t('common.create', $t('system.role.name'));
});

function getNodeClass(node: Recordable<any>) {
  const classes: string[] = [];
  if (node.value?.type === 'button') {
    classes.push('inline-flex');
  }

  return classes.join(' ');
}
</script>
<template>
  <Drawer :title="getDrawerTitle">
    <Form>
      <template #permissions="slotProps">
        <Spin :spinning="loadingPermissions" wrapper-class-name="w-full">
          <Tree
            :tree-data="permissions"
            multiple
            bordered
            :default-expanded-level="2"
            :get-node-class="getNodeClass"
            v-bind="slotProps"
            value-field="id"
            label-field="meta.title"
            icon-field="meta.icon"
          >
            <template #node="{ value }">
              <IconifyIcon v-if="value.meta.icon" :icon="value.meta.icon" />
              {{ $t(value.meta.title) }}
            </template>
          </Tree>
        </Spin>
      </template>
    </Form>
    <AiCodeSuggester 
      v-if="!id && roleNameVal"
      :input-text="roleNameVal" 
      @select="(code) => formApi.setFieldValue('id', code)" 
    />
  </Drawer>
</template>
<style lang="css" scoped>
:deep(.ant-tree-title) {
  .tree-actions {
    @apply ml-5 hidden;
  }
}

:deep(.ant-tree-title:hover) {
  .tree-actions {
    @apply ml-5 flex flex-auto justify-end;
  }
}
</style>
