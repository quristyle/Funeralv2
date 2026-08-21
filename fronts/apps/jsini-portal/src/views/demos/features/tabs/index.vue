<script lang="ts" setup>
import { ref } from 'vue';
import { useRouter } from 'vue-router';

import { Page } from '@vben/common-ui';
import { useTabs } from '@vben/hooks';

import { Button, Card, Input } from 'ant-design-vue';

const router = useRouter();
const newTabTitle = ref('');

const {
  closeAllTabs,
  closeCurrentTab,
  closeLeftTabs,
  closeOtherTabs,
  closeRightTabs,
  closeTabByKey,
  refreshTab,
  resetTabTitle,
  setTabTitle,
} = useTabs();

function openTab() {
  // 라우터 이동이며, path를 사용할 수도 있습니다.
  router.push({ name: 'VbenAbout' });
}

function openTabWithParams(id: number) {
  // 라우터 이동이며, path를 사용할 수도 있습니다.
  router.push({ name: 'FeatureTabDetailDemo', params: { id } });
}

function reset() {
  newTabTitle.value = '';
  resetTabTitle();
}
</script>

<template>
  <Page description="탭 작업이 필요한 시나리오에 사용됩니다." title="탭">
    <Card class="mb-5" title="탭 열기/닫기">
      <div class="mb-3 text-foreground/80">
        탭이 존재하면 바로 해당 탭으로 전환됩니다. 탭이 존재하지 않으면 새 탭을 엽니다.
      </div>
      <div class="flex flex-wrap gap-3">
        <Button type="primary" @click="openTab"> "정보" 탭 열기 </Button>
        <Button type="primary" @click="closeTabByKey('/vben-admin/about')">
          "정보" 탭 닫기
        </Button>
      </div>
    </Card>

    <Card class="mb-5" title="탭 작업">
      <div class="mb-3 text-foreground/80">탭의 다양한 작업을 동적으로 제어하는 데 사용됩니다.</div>
      <div class="flex flex-wrap gap-3">
        <Button type="primary" @click="closeCurrentTab()">
          현재 탭 닫기
        </Button>
        <Button type="primary" @click="closeLeftTabs()">
          왼쪽 탭 닫기
        </Button>
        <Button type="primary" @click="closeRightTabs()">
          오른쪽 탭 닫기
        </Button>
        <Button type="primary" @click="closeAllTabs()"> 모든 탭 닫기 </Button>
        <Button type="primary" @click="closeOtherTabs()">
          기타 탭 닫기
        </Button>
        <Button type="primary" @click="refreshTab()"> 현재 탭 새로고침 </Button>
      </div>
    </Card>

    <Card class="mb-5" title="동적 제목">
      <div class="mb-3 text-foreground/80">
        이 작업은 페이지 제목에 영향을 주지 않고 탭 제목만 수정합니다.
      </div>
      <div class="flex flex-wrap items-center gap-3">
        <Input
          v-model:value="newTabTitle"
          class="w-40"
          placeholder="새 제목을 입력하세요"
        />
        <Button type="primary" @click="() => setTabTitle(newTabTitle)">
          수정
        </Button>
        <Button @click="reset"> 초기화 </Button>
      </div>
    </Card>

    <Card class="mb-5" title="최대 열기 수">
      <div class="mb-3 text-foreground/80">
        매개변수가 있는 탭의 최대 열기 수를 제한하며, `route.meta.maxNumOfOpenTab`으로 제어됩니다.
      </div>
      <div class="flex flex-wrap items-center gap-3">
        <template v-for="item in 5" :key="item">
          <Button type="primary" @click="openTabWithParams(item)">
            {{ item }} 상세 페이지 열기
          </Button>
        </template>
      </div>
    </Card>
  </Page>
</template>
