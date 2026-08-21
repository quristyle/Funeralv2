<script setup lang="ts">
import { Page } from '@vben/common-ui';

import { VbenContextMenu } from '@vben-core/shadcn-ui';

import { Button, Card, message } from 'ant-design-vue';

const needHidden = (role: string) => {
  return role === 'user';
};

const contextMenus = () => {
  return [
    {
      text: '새로고침',
      key: 'refresh',
      handler: (data: any) => {
        message.success('새로고침 성공', data);
      },
      hidden: needHidden('admin'),
    },
    {
      text: '현재 탭 닫기',
      key: 'close-current',
      handler: (data: any) => {
        message.success('현재 탭 닫기', data);
      },
      hidden: needHidden('user'),
    },
    {
      text: '다른 탭 닫기',
      key: 'close-other',
      handler: (data: any) => {
        message.success('다른 탭 닫기', data);
      },
    },
    {
      text: '모든 탭 닫기',
      key: 'close-all',
      handler: (data: any) => {
        message.success('모든 탭 닫기', data);
      },
    },
  ];
};
</script>

<template>
  <Page title="Context Menu 컨텍스트 메뉴">
    <Card title="기본 사용법">
      <div>총 4개의 메뉴 (새로고침, 현재 탭 닫기, 다른 탭 닫기, 모든 탭 닫기)</div>
      <br />
      <br />
      <VbenContextMenu :menus="contextMenus" :modal="true" item-class="pr-6">
        <Button> 마우스 오른쪽 버튼을 클릭하여 컨텍스트 메뉴를 엽니다(숨겨진 항목 있음) </Button>
      </VbenContextMenu>
    </Card>
  </Page>
</template>
