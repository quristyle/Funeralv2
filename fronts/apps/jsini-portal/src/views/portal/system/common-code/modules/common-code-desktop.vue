<script lang="ts" setup>
import { ColPage } from '@vben/common-ui';
import { Card, Button, Space, Popconfirm } from 'ant-design-vue';
import { IconifyIcon } from '@vben/icons';
import GridIconButton from '#/components/GridIconButton.vue';
import type { UseCommonCodeReturn } from '../composables/use-common-code';
import GroupForm from './group-form.vue';
import CodeForm from './code-form.vue';

defineProps<{
  context: UseCommonCodeReturn;
}>();
</script>

<template>
  <ColPage
    title="공통코드 관리"
    description="시스템에서 사용하는 공통코드를 관리합니다."
    auto-content-height
    :left-width="30"
    :left-min-width="20"
    :left-max-width="50"
    :left-collapsible="true"
    :resizable="true"
    :split-line="true"
    :split-handle="true"
  >
    <template #left>
      <Card title="코드 그룹" :bordered="false" class="mr-2 h-full flex flex-col">
        <template #extra>
          <GridIconButton
            icon="vxe-icon-add"
            title="코드 그룹 추가"
            @click="context.groupFormRef.value?.openModal()"
          />
        </template>
        <component :is="context.GroupGrid">
          <template #action="{ row }">
            <Space>
              <Button type="link" size="small" @click="context.groupFormRef.value?.openModal(row)">
                <IconifyIcon icon="lucide:edit" class="size-4" />
              </Button>
              <Popconfirm title="그룹을 삭제하시겠습니까?" @confirm="context.handleGroupDelete(row.id)">
                <Button type="link" size="small" danger>
                  <IconifyIcon icon="lucide:trash-2" class="size-4" />
                </Button>
              </Popconfirm>
            </Space>
          </template>
        </component>
      </Card>
    </template>

    <Card :title="context.currentGroup.value ? `[${context.currentGroup.value.groupName}] 코드 목록` : '코드 목록'" :bordered="false" class="ml-2 h-full flex flex-col">
      <template #extra v-if="context.currentGroup.value">
        <GridIconButton
          icon="vxe-icon-add"
          title="코드 추가"
          @click="context.codeFormRef.value?.openModal(context.currentGroup.value.id)"
        />
      </template>
      
      <component :is="context.CodeGrid" v-if="context.currentGroup.value">
        <template #action="{ row }">
          <Space>
            <Button 
              v-if="context.currentGroup.value.isHierarchical"
              type="link" 
              size="small" 
              @click="context.codeFormRef.value?.openModal(context.currentGroup.value.id, null, row.id)"
            >
              하위추가
            </Button>
            <Button type="link" size="small" @click="context.codeFormRef.value?.openModal(context.currentGroup.value.id, row)">
              <IconifyIcon icon="lucide:edit" class="size-4" />
            </Button>
            <Popconfirm title="정말 삭제하시겠습니까?" @confirm="context.handleDelete(row.id)">
              <Button type="link" size="small" danger>
                <IconifyIcon icon="lucide:trash-2" class="size-4" />
              </Button>
            </Popconfirm>
          </Space>
        </template>
      </component>
      <div v-else class="flex h-64 items-center justify-center text-muted-foreground">
        왼쪽에서 그룹을 선택해주세요.
      </div>
    </Card>

    <!-- 모달 컴포넌트 -->
    <GroupForm :ref="(el) => context.groupFormRef.value = el" @success="context.loadGroups" />
    <CodeForm :ref="(el) => context.codeFormRef.value = el" @success="context.loadCodes" />
  </ColPage>
</template>

<style scoped>
:deep(.vxe-grid) {
  height: 600px;
}

@media (min-width: 769px) {
  :deep(.ant-card) {
    display: flex;
    flex-direction: column;
    height: 100%;
  }

  :deep(.ant-card-body) {
    flex: 1;
    display: flex;
    flex-direction: column;
    min-height: 0;
    padding: 12px;
  }

  :deep(.vxe-grid) {
    height: 100% !important;
  }
}
</style>
