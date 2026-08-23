/**
 * 프로시저 하나에 붙는 그리드의 조회·저장·삭제를 묶어 둔 컴포저블.
 *
 * 프로젝트관리 화면은 대부분 같은 모양이다.
 *   ① 조건을 골라 프로시저를 조회한다
 *   ② 그리드에서 행을 편집한다
 *   ③ 변경된 행만 같은 프로시저에 `save` 로 보낸다
 *   ④ 삭제는 같은 프로시저에 `delete` 로 보낸다
 *
 * 이식 전에는 화면마다 이 네 가지를 손으로 적었다. 여기 한 번만 두면
 * 화면은 프로시저 이름과 조건만 넘기면 된다.
 */
import type { ProjMngResult, ProjMngRow } from '#/api/projmng';

import { ref, shallowRef } from 'vue';

import { dbCont, dbDelete, dbSave } from '#/api/projmng';

export interface UseProcGridOptions {
  /** `/Proj/sys` 로 보낸다 — 서버 캐시를 타지 않아야 하는 조회 */
  isServerFix?: boolean;
  /** 프로젝트에 등록된 외부 DB 로 붙는다 */
  isProjDb?: boolean;
  /** 저장·삭제 후 다시 조회한다. 서버가 채운 키·순번을 보려면 켜 둔다 */
  reloadAfterWrite?: boolean;
  /** 저장 시 조회 조건과 별도로 넘길 파라미터 */
  saveParams?: Record<string, unknown>;
}

export function useProcGrid(procName: string, options?: UseProcGridOptions) {
  /** 마지막 조회 결과. 그리드에 그대로 넘긴다(`cols` + `data`). */
  const result = shallowRef<null | ProjMngResult>(null);
  const loading = ref(false);

  /** 마지막 조회 조건. 저장 후 같은 조건으로 다시 읽기 위해 기억한다. */
  let lastParams: Record<string, unknown> = {};

  async function load(params?: Record<string, unknown>) {
    if (params) lastParams = params;
    loading.value = true;
    try {
      result.value = await dbCont(procName, lastParams, 'srch', {
        isServerFix: options?.isServerFix,
        isProjDb: options?.isProjDb,
      });
    } finally {
      loading.value = false;
    }
  }

  async function reload() {
    await load();
  }

  /** 그리드에서 편집 표시가 붙은 행만 저장한다. */
  async function save(params?: Record<string, unknown>) {
    const saved = await dbSave(
      procName,
      { ...lastParams, ...options?.saveParams, ...params },
      result.value?.data ?? [],
    );

    if (saved.code >= 0 && (options?.reloadAfterWrite ?? true)) {
      await reload();
    }
    return saved;
  }

  /** 행 하나를 삭제한다. 그리드는 이미 화면에서 그 행을 지운 상태로 부른다. */
  async function remove(row: ProjMngRow) {
    const deleted = await dbDelete(procName, { ...lastParams, ...row });

    if (deleted.code >= 0 && (options?.reloadAfterWrite ?? true)) {
      await reload();
    }
    return deleted;
  }

  return { result, loading, load, reload, save, remove };
}
