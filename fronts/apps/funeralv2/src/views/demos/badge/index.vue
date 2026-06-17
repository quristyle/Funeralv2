<script lang="ts" setup>
import { reactive } from 'vue';
import { useRoute } from 'vue-router';

import { Page } from '@vben/common-ui';
import { useAccessStore } from '@vben/stores';

import { MenuBadge } from '@vben-core/menu-ui';

import { Button, Card, Radio, RadioGroup } from 'ant-design-vue';

import { useVbenForm } from '#/adapter/form';

const colors = [
  { label: '기본값: 기본', value: 'default' },
  { label: '기본값: 파괴적', value: 'destructive' },
  { label: '기본값: 주요', value: 'primary' },
  { label: '기본값: 성공', value: 'success' },
  { label: '사용자 정의', value: 'bg-gray-200 text-black' },
];

const route = useRoute();
const accessStore = useAccessStore();
const menu = accessStore.getMenuByPath(route.path);
const badgeProps = reactive({
  badge: menu?.badge as string,
  badgeType: menu?.badge ? 'normal' : (menu?.badgeType as 'dot' | 'normal'),
  badgeVariants: menu?.badgeVariants as string,
});

const [Form] = useVbenForm({
  handleValuesChange(values) {
    badgeProps.badge = values.badge;
    badgeProps.badgeType = values.badgeType;
    badgeProps.badgeVariants = values.badgeVariants;
  },
  schema: [
    {
      component: 'RadioGroup',
      componentProps: {
        buttonStyle: 'solid',
        options: [
          { label: '도트 배지', value: 'dot' },
          { label: '텍스트 배지', value: 'normal' },
        ],
        optionType: 'button',
      },
      defaultValue: badgeProps.badgeType,
      fieldName: 'badgeType',
      label: '유형',
    },
    {
      component: 'Input',
      componentProps: {
        maxLength: 4,
        placeholder: '배지 내용을 입력하세요',
        style: { width: '200px' },
      },
      defaultValue: badgeProps.badge,
      fieldName: 'badge',
      label: '배지 내용',
    },
    {
      component: 'RadioGroup',
      defaultValue: badgeProps.badgeVariants,
      fieldName: 'badgeVariants',
      label: '색상',
    },
    {
      component: 'Input',
      fieldName: 'action',
    },
  ],
  showDefaultActions: false,
});

function updateMenuBadge() {
  if (menu) {
    menu.badge = badgeProps.badge;
    menu.badgeType = badgeProps.badgeType;
    menu.badgeVariants = badgeProps.badgeVariants;
  }
}
</script>

<template>
  <Page
    description="메뉴 항목에 배지를 표시할 수 있으며, 이 배지들은 능동적으로 업데이트할 수 있습니다."
    title="메뉴 배지"
  >
    <Card title="배지 업데이트">
      <Form>
        <template #badgeVariants="slotProps">
          <RadioGroup v-bind="slotProps">
            <Radio
              v-for="color in colors"
              :key="color.value"
              :value="color.value"
            >
              <div
                :title="color.label"
                class="flex h-3.5 w-12.5 items-center justify-start"
              >
                <MenuBadge
                  v-bind="{ ...badgeProps, badgeVariants: color.value }"
                />
              </div>
            </Radio>
          </RadioGroup>
        </template>
        <template #action>
          <Button type="primary" @click="updateMenuBadge">배지 업데이트</Button>
        </template>
      </Form>
    </Card>
  </Page>
</template>
