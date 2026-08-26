<script lang="ts" setup>
import { Settings } from '@vben/icons';
import { $t } from '@vben/locales';

import { useVbenDrawer } from '@vben-core/popup-ui';
import { VbenButton } from '@vben-core/shadcn-ui';

import PreferencesDrawer from './preferences-drawer.vue';
import { usePreferencesBinding } from './use-preferences-binding';

interface Props {
  /** 是否显示按钮 */
  showButton?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  showButton: true,
});

const emit = defineEmits<{ clearPreferencesAndLogout: [] }>();

const [Drawer, drawerApi] = useVbenDrawer({
  connectedComponent: PreferencesDrawer,
});

// 暴露打开抽屉的方法
defineExpose({
  open: () => drawerApi.open(),
});

// 스토어 ↔ props·listener 바인딩.
// `/setting/environment` 페이지(`preferences-view.vue`)와 같은 것을 쓴다 —
// 한쪽에만 새 설정이 붙는 일을 막으려고 구현을 하나로 뺐다.
const { attrs, listen } = usePreferencesBinding();
</script>
<template>
  <div>
    <Drawer
      v-bind="{ ...$attrs, ...attrs }"
      v-on="listen"
      @clear-preferences-and-logout="emit('clearPreferencesAndLogout')"
    />

    <!-- 触发打开抽屉的按钮(可覆盖) -->
    <slot>
      <VbenButton
        v-if="props.showButton"
        :title="$t('preferences.title')"
        class="flex-col-center size-10 cursor-pointer rounded-l-lg rounded-r-none border-none bg-primary"
        @click="() => drawerApi.open()"
      >
        <Settings class="size-5" />
      </VbenButton>
    </slot>
  </div>
</template>
