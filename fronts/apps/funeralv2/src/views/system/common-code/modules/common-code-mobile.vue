<script lang="ts" setup>
import { Page } from '@vben/common-ui';
import { Button, Card, Space, Popconfirm } from 'ant-design-vue';
import { Plus, IconifyIcon } from '@vben/icons';
import type { UseCommonCodeReturn } from '../composables/use-common-code';
import GroupForm from './group-form.vue';
import CodeForm from './code-form.vue';

defineProps<{
  context: UseCommonCodeReturn;
}>();
</script>

<template>
  <Page 
    title="공통코드 관리" 
    description="시스템에서 사용하는 공통코드를 관리합니다."
  >
    <!-- 1. 그룹 목록 뷰 (선택된 그룹이 없을 때) -->
    <Card 
      v-if="!context.currentGroup.value" 
      title="코드 그룹 목록" 
      :bordered="false"
      class="w-full bg-card p-2 rounded-(--radius) border border-border"
    >
      <template #extra>
        <Button type="primary" @click="context.groupFormRef.value?.openModal()">
          <Plus class="mr-1 size-4" />
          추가
        </Button>
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

    <!-- 2. 세부 코드 목록 뷰 (특정 그룹이 선택되었을 때) -->
    <Card 
      v-else 
      :bordered="false"
      class="w-full bg-card p-2 rounded-(--radius) border border-border"
    >
      <template #title>
        <div class="flex items-center gap-2">
          <Button type="text" size="small" class="flex items-center" @click="context.currentGroup.value = null">
            <IconifyIcon icon="lucide:arrow-left" class="size-4 mr-1" />
            이전
          </Button>
          <span class="text-lg font-bold text-foreground">
            {{ `[${context.currentGroup.value.groupName}] 코드 목록` }}
          </span>
        </div>
      </template>
      <template #extra>
        <Button type="primary" @click="context.codeFormRef.value?.openModal(context.currentGroup.value.id)">
          <Plus class="mr-1 size-4" />
          추가
        </Button>
      </template>
      
      <component :is="context.CodeGrid">
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
    </Card>

    <!-- 모달 컴포넌트 -->
    <GroupForm :ref="(el) => context.groupFormRef.value = el" @success="context.loadGroups" />
    <CodeForm :ref="(el) => context.codeFormRef.value = el" @success="context.loadCodes" />
  </Page>
</template>

<style scoped>
:deep(.vxe-grid) {
  height: 450px;
}
</style>
