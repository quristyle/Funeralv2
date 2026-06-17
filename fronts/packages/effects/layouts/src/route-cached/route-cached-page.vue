<!-- 이 컴포넌트는 캐시된 route를 가져와 pinia에 저장하는 데 사용됩니다. -->
<script setup lang="ts">
import type { VNode } from 'vue';
import type { RouteLocationNormalizedLoadedGeneric } from 'vue-router';

import { watch } from 'vue';

import { useTabbarStore } from '@vben/stores';

interface Props {
  component?: VNode;
  route: RouteLocationNormalizedLoadedGeneric;
}

/**
 * 페이지 캐시 컴포넌트이며, 실제 렌더링은 수행하지 않습니다.
 */
defineOptions({
  render() {
    return null;
  },
});
const props = defineProps<Props>();

const { addCachedRoute } = useTabbarStore();

watch(
  () => props.route,
  () => {
    if (props.component && props.route.meta.domCached) {
      addCachedRoute(props.component, props.route);
    }
  },
  { immediate: true },
);
</script>
