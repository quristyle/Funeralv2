import type { RequestClient } from '../request-client';
import type { RequestClientConfig } from '../types';

type DownloadRequestConfig = {
  /**
   * 기대하는 데이터 타입을 정의합니다.
   * raw: headers, status 등을 포함한 원본 AxiosResponse.
   * body: 응답 데이터의 BODY 부분(Blob)만 반환.
   */
  responseReturn?: 'body' | 'raw';
} & Omit<RequestClientConfig, 'responseReturn'>;

class FileDownloader {
  private client: RequestClient;

  constructor(client: RequestClient) {
    this.client = client;
  }
  /**
   * 파일 다운로드
   * @param url 파일의 전체 링크
   * @param config 설정 정보(선택 사항).
   * @returns config.responseReturn이 'body'인 경우 Blob(기본값)을 반환하고, 그렇지 않으면 RequestResponse<Blob>을 반환합니다.
   */
  public async download<T = Blob>(
    url: string,
    config?: DownloadRequestConfig,
  ): Promise<T> {
    const finalConfig: DownloadRequestConfig = {
      responseReturn: 'body',
      method: 'GET',
      ...config,
      responseType: 'blob',
    };

    // Prefer a generic request if available; otherwise, dispatch to method-specific calls.
    const method = (finalConfig.method || 'GET').toUpperCase();
    const clientAny = this.client as any;

    if (typeof clientAny.request === 'function') {
      return await clientAny.request(url, finalConfig);
    }
    const lower = method.toLowerCase();

    if (typeof clientAny[lower] === 'function') {
      if (['POST', 'PUT'].includes(method)) {
        const { data, ...rest } = finalConfig as Record<string, any>;
        return await clientAny[lower](url, data, rest);
      }

      return await clientAny[lower](url, finalConfig);
    }

    throw new Error(
      `RequestClient does not support method "${method}". Please ensure the method is properly implemented in your RequestClient instance.`,
    );
  }
}

export { FileDownloader };
