import { createHash } from 'node:crypto';

/**
 * 콘텐츠 기반 해시 생성, 길이 사용자 정의 가능
 * @param content
 * @param hashLSize
 */
function generatorContentHash(content: string, hashLSize?: number) {
  const hash = createHash('md5').update(content, 'utf8').digest('hex');

  if (hashLSize) {
    return hash.slice(0, hashLSize);
  }

  return hash;
}

export { generatorContentHash };
