import { requestClient } from '#/api/request';

interface UploadFileParams {
  file: File;
  bizType?: string;
  onError?: (error: Error) => void;
  onProgress?: (progress: { percent: number }) => void;
  onSuccess?: (data: any, file: File) => void;
}
export async function upload_file({
  file,
  bizType,
  onError,
  onProgress,
  onSuccess,
}: UploadFileParams) {
  try {
    onProgress?.({ percent: 0 });

    const url = bizType ? `/file/upload?bizType=${encodeURIComponent(bizType)}` : '/file/upload';
    const data = await requestClient.upload(url, { file });

    onProgress?.({ percent: 100 });
    onSuccess?.(data, file);
  } catch (error) {
    onError?.(error instanceof Error ? error : new Error(String(error)));
  }
}
