import type { Recordable } from '@vben/types';

import { requestClient } from '#/api/request';

/**
 * 배열 요청 시작
 */
async function getParamsData(
  params: Recordable<any>,
  type: 'brackets' | 'comma' | 'indices' | 'repeat',
) {
  return requestClient.get('/funeral/status', {
    params,
    paramsSerializer: type,
    responseReturn: 'raw',
  });
}

export { getParamsData };
