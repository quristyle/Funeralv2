import { requestClient } from '#/api/request';

/**
 * 요청 시작
 */
async function getBigIntData() {
  return requestClient.get('/funeral/demo/bigint');
}

export { getBigIntData };
