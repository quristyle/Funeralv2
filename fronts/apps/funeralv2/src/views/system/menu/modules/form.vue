<script lang="ts" setup>


import type { Recordable } from '@vben/types';

import type { VbenFormSchema } from '#/adapter/form';

import { computed, h, ref, defineComponent, markRaw, nextTick, watch } from 'vue';

import { useVbenDrawer } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { $te } from '@vben/locales';
import { getPopupContainer } from '@vben/utils';

import { breakpointsTailwind, useBreakpoints } from '@vueuse/core';

import { useVbenForm, z } from '#/adapter/form';
import {
  createMenu,
  getMenuList,
  isMenuNameExists,
  isMenuPathExists,
  SystemMenuApi,
  updateMenu,
} from '#/api/system/menu';
import { $t } from '#/locales';
import { componentKeys } from '#/router/routes';

import { getMenuTypeOptions } from '../data';

const emit = defineEmits<{
  success: [];
}>();
const formData = ref<SystemMenuApi.SystemMenu>();
const titleSuffix = ref<string>();
const isBinding = ref(false);

const CustomTitleInput = defineComponent({
  name: 'CustomTitleInput',
  props: {
    value: {
      type: String,
      default: '',
    },
    modelValue: {
      type: String,
      default: '',
    },
  },
  emits: ['update:value', 'update:modelValue', 'change'],
  setup(props, { emit, attrs }) {
    const computedValue = computed(() => {
      return props.value !== undefined && props.value !== '' ? props.value : props.modelValue;
    });

    watch(computedValue, (val) => {
      titleSuffix.value = val && $te(val) ? $t(val) : undefined;
    }, { immediate: true });

    return () => {
      const { size, ...restAttrs } = attrs as any;
      const inputNode = h(Input, {
        ...restAttrs,
        value: computedValue.value,
        'onUpdate:value': (val: string) => {
          emit('update:value', val);
          emit('update:modelValue', val);
        },
        onChange: (e: any) => {
          const val = e.target.value;
          titleSuffix.value = val && $te(val) ? $t(val) : undefined;
          emit('update:value', val);
          emit('update:modelValue', val);
          emit('change', e);
        },
      }, {
        addonAfter: () => {
          if (formData.value?.id) {
            return h(
              Button,
              {
                size: 'small',
                type: 'link',
                class: 'p-0 flex items-center justify-center',
                onClick: (e: MouseEvent) => {
                  e.preventDefault();
                  e.stopPropagation();
                  openI18nModal();
                }
              },
              {
                icon: () => h(IconifyIcon, { icon: 'lucide:globe', class: 'size-4 text-primary' })
              }
            );
          }
          return null;
        }
      });

      const textNode = titleSuffix.value
        ? h('div', { class: 'text-xs text-gray-400 mt-1 pl-1 text-left' }, titleSuffix.value)
        : null;

      return h('div', { class: 'w-full' }, [inputNode, textNode]);
    };
  }
});

const CustomComponentInput = defineComponent({
  name: 'CustomComponentInput',
  props: {
    value: {
      type: String,
      default: '',
    },
    modelValue: {
      type: String,
      default: '',
    },
  },
  emits: ['update:value', 'update:modelValue', 'change'],
  setup(props, { emit, attrs }) {
    const computedValue = computed(() => {
      return props.value !== undefined && props.value !== '' ? props.value : props.modelValue;
    });

    return () => {
      const { size, ...restAttrs } = attrs as any;
      return h('div', { class: 'ant-input-group-wrapper w-full' }, [
        h('div', { class: 'ant-input-wrapper ant-input-group flex w-full' }, [
          h('span', { class: 'ant-input-group-addon flex items-center justify-center bg-gray-50 border border-r-0 rounded-l px-3 text-gray-500 text-sm whitespace-nowrap' }, '#/views'),
          h(AutoComplete, {
            ...restAttrs,
            value: computedValue.value,
            'onUpdate:value': (val: string) => {
              emit('update:value', val);
              emit('update:modelValue', val);
            },
            onChange: (val: any) => {
              emit('update:value', val);
              emit('update:modelValue', val);
              emit('change', val);
            },
            class: 'w-full',
            style: {
              borderTopLeftRadius: '0px',
              borderBottomLeftRadius: '0px',
              borderTopRightRadius: '0px',
              borderBottomRightRadius: '0px',
            }
          }),
          h('span', { class: 'ant-input-group-addon flex items-center justify-center bg-gray-50 border border-l-0 rounded-r px-3 text-gray-500 text-sm whitespace-nowrap' }, '.vue'),
        ])
      ]);
    };
  }
});

const schema: VbenFormSchema[] = [
  {
    component: 'RadioGroup',
    componentProps: {
      buttonStyle: 'solid',
      options: getMenuTypeOptions(),
      optionType: 'button',
    },
    defaultValue: 'MENU',
    fieldName: 'type',
    formItemClass: 'col-span-2 md:col-span-2',
    label: $t('system.menu.type'),
  },
  {
    component: 'Input',
    fieldName: 'name',
    label: $t('system.menu.menuName'),
    rules: z
      .string()
      .min(2, $t('ui.formRules.minLength', [$t('system.menu.menuName'), 2]))
      .max(30, $t('ui.formRules.maxLength', [$t('system.menu.menuName'), 30]))
      .refine(
        async (value: string) => {
          if (isBinding.value) {
            return true;
          }
          if (!value || value.trim().length < 2) {
            return true;
          }
          if (formData.value?.id && value === formData.value.name) {
            return true;
          }
          return !(await isMenuNameExists(value, formData.value?.id));
        },
        (value) => ({
          message: $t('ui.formRules.alreadyExists', [
            $t('system.menu.menuName'),
            value,
          ]),
        }),
      ),
  },
  {
    component: 'ApiTreeSelect',
    componentProps: {
      api: getMenuList,
      class: 'w-full',
      filterTreeNode(input: string, node: Recordable<any>) {
        if (!input || input.length === 0) {
          return true;
        }
        const title: string = node.meta?.title ?? '';
        if (!title) return false;
        return title.includes(input) || $t(title).includes(input);
      },
      getPopupContainer,
      labelField: 'meta.title',
      showSearch: true,
      treeDefaultExpandAll: true,
      valueField: 'id',
      childrenField: 'children',
    },
    fieldName: 'pid',
    label: $t('system.menu.parent'),
    renderComponentContent() {
      return {
        title({ label, meta }: { label: string; meta: Recordable<any> }) {
          const coms = [];
          if (!label) return '';
          if (meta?.icon) {
            coms.push(h(IconifyIcon, { class: 'size-4', icon: meta.icon }));
          }
          coms.push(h('span', { class: '' }, $t(label || '')));
          return h('div', { class: 'flex items-center gap-1' }, coms);
        },
      };
    },
  },
  {
    component: markRaw(CustomTitleInput),
    fieldName: 'meta.title',
    formItemClass: 'col-span-2 md:col-span-2',
    label: $t('system.menu.menuTitle'),
    rules: 'required',
  },
  {
    component: 'Input',
    dependencies: {
      show: (values) => {
        return ['CATALOG', 'EMBEDDED', 'MENU'].includes(values.type);
      },
      triggerFields: ['type'],
    },
    fieldName: 'path',
    label: $t('system.menu.path'),
    rules: z
      .string()
      .min(2, $t('ui.formRules.minLength', [$t('system.menu.path'), 2]))
      .max(100, $t('ui.formRules.maxLength', [$t('system.menu.path'), 100]))
      .refine(
        (value: string) => {
          return value.startsWith('/');
        },
        $t('ui.formRules.startWith', [$t('system.menu.path'), '/']),
      )
      .refine(
        async (value: string) => {
          if (isBinding.value) {
            return true;
          }
          if (!value || value.trim().length < 2 || !value.startsWith('/')) {
            return true;
          }
          if (formData.value?.id && value === formData.value.path) {
            return true;
          }
          return !(await isMenuPathExists(value, formData.value?.id));
        },
        (value) => ({
          message: $t('ui.formRules.alreadyExists', [
            $t('system.menu.path'),
            value,
          ]),
        }),
      ),
  },
  {
    component: 'Input',
    dependencies: {
      show: (values) => {
        return ['EMBEDDED', 'MENU'].includes(values.type);
      },
      triggerFields: ['type'],
    },
    fieldName: 'activePath',
    help: $t('system.menu.activePathHelp'),
    label: $t('system.menu.activePath'),
    rules: z
      .string()
      .min(2, $t('ui.formRules.minLength', [$t('system.menu.path'), 2]))
      .max(100, $t('ui.formRules.maxLength', [$t('system.menu.path'), 100]))
      .refine(
        (value: string) => {
          return value.startsWith('/');
        },
        $t('ui.formRules.startWith', [$t('system.menu.path'), '/']),
      )
      .refine(async (value: string) => {
        return await isMenuPathExists(value);
      }, $t('system.menu.activePathMustExist'))
      .optional(),
  },
  {
    component: 'IconPicker',
    componentProps: {
      prefix: 'carbon',
    },
    dependencies: {
      show: (values) => {
        return ['CATALOG', 'EMBEDDED', 'LINK', 'MENU'].includes(values.type);
      },
      triggerFields: ['type'],
    },
    fieldName: 'meta.icon',
    label: $t('system.menu.icon'),
  },
  {
    component: 'IconPicker',
    componentProps: {
      prefix: 'carbon',
    },
    dependencies: {
      show: (values) => {
        return ['CATALOG', 'EMBEDDED', 'MENU'].includes(values.type);
      },
      triggerFields: ['type'],
    },
    fieldName: 'meta.activeIcon',
    label: $t('system.menu.activeIcon'),
  },
  {
    component: markRaw(CustomComponentInput),
    componentProps: {
      allowClear: true,
      class: 'w-full',
      filterOption(input: string, option: { value: string }) {
        return option.value.toLowerCase().includes(input.toLowerCase());
      },
      options: componentKeys.map((v) => ({ value: v })),
    },
    dependencies: {
      rules: (values) => {
        return values.type === 'MENU' ? 'required' : null;
      },
      show: (values) => {
        return values.type === 'MENU';
      },
      triggerFields: ['type'],
    },
    fieldName: 'component',
    formItemClass: 'col-span-2 md:col-span-2',
    label: $t('system.menu.component'),
  },
  {
    component: 'Input',
    dependencies: {
      show: (values) => {
        return ['EMBEDDED', 'LINK'].includes(values.type);
      },
      triggerFields: ['type'],
    },
    fieldName: 'linkSrc',
    label: $t('system.menu.linkSrc'),
    rules: z.string().url($t('ui.formRules.invalidURL')),
  },
  {
    component: 'Input',
    dependencies: {
      rules: (values) => {
        return values.type === 'BUTTON' ? 'required' : null;
      },
      show: (values) => {
        return ['BUTTON', 'CATALOG', 'EMBEDDED', 'MENU'].includes(values.type);
      },
      triggerFields: ['type'],
    },
    fieldName: 'authCode',
    label: $t('system.menu.authCode'),
  },
  {
    component: 'Switch',
    componentProps: {
      checkedChildren: $t('common.enabled'),
      unCheckedChildren: $t('common.disabled'),
      checkedValue: 1,
      unCheckedValue: 0,
    },
    defaultValue: 1,
    fieldName: 'status',
    label: $t('system.menu.status'),
  },
  {
    component: 'InputNumber',
    componentProps: {
      class: 'w-full',
    },
    fieldName: 'meta.order',
    label: $t('system.menu.order'),
    defaultValue: 0,
    rules: 'required',
  },
  {
    component: 'Select',
    componentProps: {
      allowClear: true,
      class: 'w-full',
      options: [
        { label: $t('system.menu.badgeType.dot'), value: 'dot' },
        { label: $t('system.menu.badgeType.normal'), value: 'normal' },
      ],
    },
    dependencies: {
      show: (values) => {
        return values.type !== 'BUTTON';
      },
      triggerFields: ['type'],
    },
    fieldName: 'meta.badgeType',
    label: $t('system.menu.badgeType.title'),
  },
  {
    component: 'Input',
    componentProps: (values) => {
      return {
        allowClear: true,
        class: 'w-full',
        disabled: values.meta?.badgeType !== 'normal',
      };
    },
    dependencies: {
      show: (values) => {
        return values.type !== 'BUTTON';
      },
      triggerFields: ['type'],
    },
    fieldName: 'meta.badge',
    label: $t('system.menu.badge'),
  },
  {
    component: 'Select',
    componentProps: {
      allowClear: true,
      class: 'w-full',
      options: SystemMenuApi.BadgeVariants.map((v) => ({
        label: v,
        value: v,
      })),
    },
    dependencies: {
      show: (values) => {
        return values.type !== 'BUTTON';
      },
      triggerFields: ['type'],
    },
    fieldName: 'meta.badgeVariants',
    label: $t('system.menu.badgeVariants'),
  },
  {
    component: 'Divider',
    dependencies: {
      show: (values) => {
        return !['BUTTON', 'LINK'].includes(values.type);
      },
      triggerFields: ['type'],
    },
    fieldName: 'divider1',
    formItemClass: 'col-span-2 md:col-span-2 pb-0',
    hideLabel: true,
    renderComponentContent() {
      return {
        default: () => $t('system.menu.advancedSettings'),
      };
    },
  },
  {
    component: 'Checkbox',
    dependencies: {
      show: (values) => {
        return ['MENU'].includes(values.type);
      },
      triggerFields: ['type'],
    },
    fieldName: 'meta.keepAlive',
    renderComponentContent() {
      return {
        default: () => $t('system.menu.keepAlive'),
      };
    },
  },
  {
    component: 'Checkbox',
    dependencies: {
      show: (values) => {
        return ['EMBEDDED', 'MENU'].includes(values.type);
      },
      triggerFields: ['type'],
    },
    fieldName: 'meta.affixTab',
    renderComponentContent() {
      return {
        default: () => $t('system.menu.affixTab'),
      };
    },
  },
  {
    component: 'Checkbox',
    dependencies: {
      show: (values) => {
        return !['BUTTON'].includes(values.type);
      },
      triggerFields: ['type'],
    },
    fieldName: 'meta.hideInMenu',
    renderComponentContent() {
      return {
        default: () => $t('system.menu.hideInMenu'),
      };
    },
  },
  {
    component: 'Checkbox',
    dependencies: {
      show: (values) => {
        return ['CATALOG', 'MENU'].includes(values.type);
      },
      triggerFields: ['type'],
    },
    fieldName: 'meta.hideChildrenInMenu',
    renderComponentContent() {
      return {
        default: () => $t('system.menu.hideChildrenInMenu'),
      };
    },
  },
  {
    component: 'Checkbox',
    dependencies: {
      show: (values) => {
        return !['BUTTON', 'LINK'].includes(values.type);
      },
      triggerFields: ['type'],
    },
    fieldName: 'meta.hideInBreadcrumb',
    renderComponentContent() {
      return {
        default: () => $t('system.menu.hideInBreadcrumb'),
      };
    },
  },
  {
    component: 'Checkbox',
    dependencies: {
      show: (values) => {
        return !['BUTTON', 'LINK'].includes(values.type);
      },
      triggerFields: ['type'],
    },
    fieldName: 'meta.hideInTab',
    renderComponentContent() {
      return {
        default: () => $t('system.menu.hideInTab'),
      };
    },
  },
];

import { Button, Input, AutoComplete } from 'ant-design-vue';
import I18nEditModal from '#/components/i18n/I18nEditModal.vue';

const i18nEditModalRef = ref<any>(null);

async function openI18nModal() {
  if (!formData.value?.id) return;
  
  const values = await formApi.getValues();
  const key = values?.meta?.title || `menu.title.${formData.value.id}`;
  
  i18nEditModalRef.value?.open({
    id: formData.value.id,
    key,
    category: 'menu',
    onSuccess: (updatedKey: string) => {
      formApi.setFieldValue('meta.title', updatedKey);
      titleSuffix.value = $t(updatedKey);
    }
  });
}

const breakpoints = useBreakpoints(breakpointsTailwind);
const isHorizontal = computed(() => breakpoints.greaterOrEqual('md').value);

const [Form, formApi] = useVbenForm({
  commonConfig: {
    colon: true,
    formItemClass: 'col-span-2 md:col-span-1',
  },
  schema,
  showDefaultActions: false,
  wrapperClass: 'grid-cols-2 gap-x-4',
});
const [Drawer, drawerApi] = useVbenDrawer({
  onConfirm: onSubmit,
  onOpenChange(isOpen) {
    if (isOpen) {
      isBinding.value = true;
      const data = drawerApi.getData<SystemMenuApi.SystemMenu>();
      if (data?.type === 'LINK') {
        data.linkSrc = data.meta?.link;
      } else if (data?.type === 'EMBEDDED') {
        data.linkSrc = data.meta?.iframeSrc;
      }
      if (data) {
        formData.value = data;
        
        const rawComponent = formData.value.component || '';
        let parsedComponent = rawComponent;
        if (rawComponent.startsWith('#/views') && rawComponent.endsWith('.vue')) {
          parsedComponent = rawComponent.substring('#/views'.length, rawComponent.length - '.vue'.length);
        }
        
        const formValues = {
          ...formData.value,
          component: parsedComponent
        };
        
        nextTick(async () => {
          await formApi.setValues(formValues);
          await formApi.resetValidate();
          isBinding.value = false;
        });
        titleSuffix.value = formData.value.meta?.title
          ? $t(formData.value.meta.title)
          : '';
      } else {
        formApi.resetForm();
        titleSuffix.value = '';
        nextTick(async () => {
          await formApi.resetValidate();
          isBinding.value = false;
        });
      }
    }
  },
});

async function onSubmit() {
  const { valid } = await formApi.validate();
  if (valid) {
    drawerApi.lock();
    const data =
      await formApi.getValues<
        Omit<SystemMenuApi.SystemMenu, 'children' | 'id'>
      >();
    if (data.type === 'LINK') {
      data.meta = { ...data.meta, link: data.linkSrc };
    } else if (data.type === 'EMBEDDED') {
      data.meta = { ...data.meta, iframeSrc: data.linkSrc };
    }
    delete data.linkSrc;
    
    if (data.component) {
      let comp = data.component.trim();
      if (comp) {
        if (!comp.startsWith('#/views')) {
          comp = `#/views${comp}`;
        }
        if (!comp.endsWith('.vue')) {
          comp = `${comp}.vue`;
        }
        data.component = comp;
      }
    }
    
    try {
      await (formData.value?.id
        ? updateMenu(formData.value.id, data)
        : createMenu(data));
      drawerApi.close();
      emit('success');
    } finally {
      drawerApi.unlock();
    }
  }
}
const getDrawerTitle = computed(() =>
  formData.value?.id
    ? $t('ui.actionTitle.edit', [$t('system.menu.name')])
    : $t('ui.actionTitle.create', [$t('system.menu.name')]),
);
</script>
<template>
  <Drawer class="w-full max-w-200" :title="getDrawerTitle">
    <Form class="mx-4" :layout="isHorizontal ? 'horizontal' : 'vertical'" />
  </Drawer>
  <I18nEditModal ref="i18nEditModalRef" />
</template>
