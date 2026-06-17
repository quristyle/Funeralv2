import { posix } from 'node:path';

/**
 * 주어진 파일 경로를 POSIX 스타일로 변환합니다.
 * @param {string} pathname - 원본 파일 경로.
 */
function toPosixPath(pathname: string) {
  return pathname.split(`\\`).join(posix.sep);
}

export { toPosixPath };
