import { requestClient } from '#/api/request';

/**
 * 임의 상태 코드 시뮬레이션
 */
async function getMockStatusApi(status: string) {
  return requestClient.get('/funeral/status', { params: { status } });
}

export { getMockStatusApi };
