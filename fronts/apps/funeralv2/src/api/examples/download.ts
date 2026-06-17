import type { RequestResponse } from '@vben/request';

import { requestClient } from '../request';

/**
 * 파일 다운로드, Blob 가져오기
 * @returns Blob
 */
async function downloadFile1() {
  return requestClient.download<Blob>(
    'https://unpkg.com/@vbenjs/static-source@0.1.7/source/logo-v1.webp',
  );
}

/**
 * 파일 다운로드, 전체 Response 가져오기
 * @returns RequestResponse<Blob>
 */
async function downloadFile2() {
  return requestClient.download<RequestResponse<Blob>>(
    'https://unpkg.com/@vbenjs/static-source@0.1.7/source/logo-v1.webp',
    {
      responseReturn: 'raw',
    },
  );
}

export { downloadFile1, downloadFile2 };
