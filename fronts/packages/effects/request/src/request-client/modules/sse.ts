import type { AxiosRequestHeaders, InternalAxiosRequestConfig } from 'axios';

import type { RequestClient } from '../request-client';
import type { SseRequestOptions } from '../types';

/**
 * SSE 모듈
 */
class SSE {
  private client: RequestClient;

  constructor(client: RequestClient) {
    this.client = client;
  }

  public async postSSE(
    url: string,
    data?: any,
    requestOptions?: SseRequestOptions,
  ) {
    return this.requestSSE(url, data, {
      ...requestOptions,
      method: 'POST',
    });
  }

  /**
   * SSE 요청 메서드
   * @param url - 요청 URL
   * @param data - 요청 데이터
   * @param requestOptions - SSE 요청 옵션
   */
  public async requestSSE(
    url: string,
    data?: any,
    requestOptions?: SseRequestOptions,
  ) {
    const baseUrl = this.client.getBaseUrl() || '';

    let axiosConfig: InternalAxiosRequestConfig<any> = {
      url,
      method: (requestOptions?.method as any) ?? 'GET',
      headers: {} as AxiosRequestHeaders,
    };
    const requestInterceptors = this.client.instance.interceptors
      .request as any;
    if (
      requestInterceptors.handlers &&
      requestInterceptors.handlers.length > 0
    ) {
      for (const handler of requestInterceptors.handlers) {
        if (typeof handler?.fulfilled === 'function') {
          const next = await handler.fulfilled(axiosConfig as any);
          if (next) axiosConfig = next as InternalAxiosRequestConfig<any>;
        }
      }
    }

    const merged = new Headers();
    Object.entries(
      (axiosConfig.headers ?? {}) as Record<string, string>,
    ).forEach(([k, v]) => merged.set(k, String(v)));
    if (requestOptions?.headers) {
      new Headers(requestOptions.headers).forEach((v, k) => merged.set(k, v));
    }
    if (!merged.has('accept')) {
      merged.set('accept', 'text/event-stream');
    }

    let bodyInit = requestOptions?.body ?? data;
    const ct = (merged.get('content-type') || '').toLowerCase();
    if (
      bodyInit &&
      typeof bodyInit === 'object' &&
      !ArrayBuffer.isView(bodyInit as any) &&
      !(bodyInit instanceof ArrayBuffer) &&
      !(bodyInit instanceof Blob) &&
      !(bodyInit instanceof FormData) &&
      ct.includes('application/json')
    ) {
      bodyInit = JSON.stringify(bodyInit);
    }
    const requestInit: RequestInit = {
      ...requestOptions,
      method: axiosConfig.method,
      headers: merged,
      body: bodyInit,
    };

    const response = await fetch(safeJoinUrl(baseUrl, url), requestInit);
    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    const reader = response.body?.getReader();
    const decoder = new TextDecoder();

    if (!reader) {
      throw new Error('No reader');
    }
    while (true) {
      const { done, value } = await reader.read();
      if (done) {
        decoder.decode(new Uint8Array(0), { stream: false });
        requestOptions?.onEnd?.();
        reader.releaseLock?.();
        break;
      }
      const content = decoder.decode(value, { stream: true });
      requestOptions?.onMessage?.(content);
    }
  }
}

function safeJoinUrl(baseUrl: string | undefined, url: string): string {
  if (!baseUrl) {
    return url; // baseUrl이 없으면 직접 url 반환
  }

  // url 자체가 절대 주소인 경우 직접 반환
  if (/^https?:\/\//i.test(url)) {
    return url;
  }

  // baseUrl이 전체 URL인 경우 new URL 사용
  if (/^https?:\/\//i.test(baseUrl)) {
    return new URL(url, baseUrl).toString();
  }

  // 그렇지 않으면 경로 결합으로 처리
  return `${baseUrl.replace(/\/+$/, '')}/${url.replace(/^\/+/, '')}`;
}

export { SSE };
