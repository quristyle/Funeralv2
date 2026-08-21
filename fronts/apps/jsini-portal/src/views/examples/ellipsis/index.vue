<script lang="ts" setup>
import { ref } from 'vue';

import { EllipsisText, Page } from '@vben/common-ui';

import { Card } from 'ant-design-vue';

import DocButton from '../doc-button.vue';

const longText = `Vben Admin은 Vue3.0, Vite, TypeScript 기반의 백엔드 솔루션으로, 중대형 프로젝트 개발을 위한 즉시 사용 가능한 솔루션을 제공하는 것을 목표로 합니다. 2차 캡슐화 컴포넌트, utils, hooks, 동적 메뉴, 권한 검증, 다중 테마 설정, 버튼 레벨 권한 제어 등의 기능을 포함합니다. 프로젝트는 최신 프론트엔드 기술 스택을 사용하며, 기업급 중백엔드 제품 프로토타입을 빠르게 구축하는 데 도움이 되는 프로젝트 시작 템플릿으로 사용할 수 있습니다. 또한 vue3, vite, ts 등 주요 기술을 학습하기 위한 예제로도 활용될 수 있습니다. 이 프로젝트는 지속적으로 최신 기술을 추적하고 프로젝트에 적용할 것입니다.`;

const text = ref(longText);
</script>

<template>
  <Page
    description="다행 텍스트 생략을 위한 컴포넌트로, 클릭 시 펼치기와 사용자 정의 콘텐츠를 지원합니다."
    title="텍스트 생략 컴포넌트 예제"
  >
    <template #extra>
      <DocButton class="mb-2" path="/components/common-ui/vben-ellipsis-text" />
    </template>
    <Card class="mb-4" title="기본 사용법">
      <EllipsisText :max-width="500">{{ text }}</EllipsisText>
    </Card>

    <Card class="mb-4" title="다행 생략">
      <EllipsisText :line="2">{{ text }}</EllipsisText>
    </Card>

    <Card class="mb-4" title="클릭 시 펼치기">
      <EllipsisText :line="3" expand>{{ text }}</EllipsisText>
    </Card>
    <Card class="mb-4" title="사용자 정의 콘텐츠">
      <EllipsisText :max-width="240">
        내 마음속에 사는 외로운 외로운 바다 괴물 고통의 왕 깊은 바다의 빛 정체된 파도에 실증이 나기 시작했네
        <template #tooltip>
          <div style="text-align: center">
            《진황도》<br />내 마음속에 사는 외로운<br />외로운 바다 괴물 고통의 왕<br />깊은 바다의 빛 정체된 파도에 실증이 나기 시작했네
          </div>
        </template>
      </EllipsisText>
    </Card>
  </Page>
</template>
