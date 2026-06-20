import { ref } from 'vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import { message } from 'ant-design-vue';
import { useIsMobile } from '@vben/hooks';
import { groupGridOptions, codeGridOptions } from '../data';
import { 
  getCommonCodeGroups, 
  getCommonCodes, 
  deleteCommonCode,
  deleteCommonCodeGroup
} from '#/api/system/common-code';

export function useCommonCode() {
  const currentGroup = ref<any>(null);
  const groupFormRef = ref();
  const codeFormRef = ref();

  const { isMobile } = useIsMobile();
  const activeKey = ref('group');

  /**
   * 그룹 그리드 설정
   */
  const [GroupGrid, groupGridApi] = useVbenVxeGrid({
    gridOptions: groupGridOptions,
    gridEvents: {
      cellClick: ({ row }: { row: any }) => {
        currentGroup.value = row;
        loadCodes();
        if (isMobile.value) {
          activeKey.value = 'code';
        }
      },
    },
  });

  /**
   * 코드 그리드 설정
   */
  const [CodeGrid, codeGridApi] = useVbenVxeGrid({
    gridOptions: codeGridOptions,
  });

  /**
   * 그룹 데이터 로드
   */
  async function loadGroups() {
    groupGridApi.setLoading(true);
    try {
      const data = await getCommonCodeGroups();
      groupGridApi.setGridOptions({ data });
      if (data.length > 0 && !currentGroup.value) {
        currentGroup.value = data[0];
        loadCodes();
      } else if (data.length === 0) {
        currentGroup.value = null;
      }
    } finally {
      groupGridApi.setLoading(false);
    }
  }

  /**
   * 그룹 삭제 처리
   */
  async function handleGroupDelete(id: string) {
    await deleteCommonCodeGroup(id);
    message.success('그룹이 삭제되었습니다.');
    currentGroup.value = null; // 선택 해제
    loadGroups();
  }

  /**
   * 코드 데이터 로드
   */
  async function loadCodes() {
    if (!currentGroup.value) return;
    codeGridApi.setLoading(true);
    try {
      const data = await getCommonCodes(
        currentGroup.value.groupCode, 
        currentGroup.value.isHierarchical
      );
      codeGridApi.setGridOptions({ data });
    } finally {
      codeGridApi.setLoading(false);
    }
  }

  /**
   * 삭제 처리
   */
  async function handleDelete(id: string) {
    await deleteCommonCode(id);
    message.success('코드가 삭제되었습니다.');
    loadCodes();
  }

  return {
    currentGroup,
    groupFormRef,
    codeFormRef,
    activeKey,
    GroupGrid,
    groupGridApi,
    CodeGrid,
    codeGridApi,
    loadGroups,
    handleGroupDelete,
    loadCodes,
    handleDelete,
  };
}

export type UseCommonCodeReturn = ReturnType<typeof useCommonCode>;
