<script setup lang="ts">
/**
 * [부품 모음]
 *
 * 원본: ProjMngWasm `Pages/Proj/Component.razor`.
 *
 * 원본 파일은 **내용이 비어 있다**(0 바이트). 화면 자리만 잡아 둔 것으로 보인다.
 * 자리를 없애면 이식 목록과 메뉴 수가 어긋나므로 화면은 남겨 두고,
 * 이 화면이 쓸 수 있는 공용 부품을 여기 모아 두었다.
 * 원본에 무엇을 넣으려 했는지 확인되면 이 파일에 채우면 된다.
 */
import { ref } from 'vue';

import { Page } from '@vben/common-ui';

import { Alert, Card } from 'ant-design-vue';

import { CodeEditor, CodeSelect, SearchBar } from '../shared';

const sample = ref('-- 공용 편집기 부품\nselect 1;');
const code = ref('');
</script>

<template>
  <Page auto-content-height>
    <Alert
      class="mb-2"
      message="원본 화면(Component.razor)은 내용이 비어 있었습니다. 이식 목록을 맞추기 위해 자리를 남겨 두고, 공용 부품 확인용으로 채웠습니다."
      show-icon
      type="warning"
    />

    <Card class="mb-2" size="small" title="검색 줄 · 공통코드 드롭다운">
      <SearchBar>
        <CodeSelect v-model="code" code-id="projlist" show-all />
      </SearchBar>
      <div class="text-muted-foreground mt-2 text-xs">
        고른 값: {{ code || '(없음)' }}
      </div>
    </Card>

    <Card :body-style="{ padding: '8px' }" size="small" title="코드 편집기">
      <CodeEditor v-model="sample" height="240" language="pgsql" />
    </Card>
  </Page>
</template>
