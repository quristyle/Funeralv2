import { promises as fs } from 'node:fs';
import { join, normalize } from 'node:path';

const rootDir = process.cwd();

// 동시성 제한, 과도한 병렬 작업 생성을 방지합니다.
const CONCURRENCY_LIMIT = 10;

// 건너뛸 디렉토리, 정리 대상에서 제외합니다.
const SKIP_DIRS = new Set(['.DS_Store', '.git', '.idea', '.vscode']);

/**
 * 단일 파일/디렉토리 항목 처리
 * @param {string} currentDir - 현재 디렉토리 경로
 * @param {string} item - 파일/디렉토리 이름
 * @param {string[]} targets - 삭제할 대상 목록
 * @param {number} _depth - 현재 재귀 깊이
 * @returns {Promise<boolean>} - 추가 재귀 처리가 필요한지 여부
 */
async function processItem(currentDir, item, targets, _depth) {
  // 특수 디렉토리 건너뛰기
  if (SKIP_DIRS.has(item)) {
    return false;
  }

  try {
    const itemPath = normalize(join(currentDir, item));

    if (targets.includes(item)) {
      // 대상 디렉토리나 파일이 일치하면 즉시 삭제
      await fs.rm(itemPath, { force: true, recursive: true });
      console.log(`✅ Deleted: ${itemPath}`);
      return false; // 삭제되었으므로 재귀 불필요
    }

    // readdir의 withFileTypes 옵션을 사용하여 추가적인 lstat 호출을 피함
    return true; // 재귀가 필요할 수 있음, 호출자가 결정
  } catch (error) {
    // 상세한 에러 정보
    if (error.code === 'ENOENT') {
      // 파일이 존재하지 않음, 이미 삭제되었을 수 있으며 정상적인 상황임
      return false;
    } else if (error.code === 'EPERM' || error.code === 'EACCES') {
      console.error(`❌ Permission denied: ${item} in ${currentDir}`);
    } else {
      console.error(
        `❌ Error handling item ${item} in ${currentDir}: ${error.message}`,
      );
    }
    return false;
  }
}

/**
 * 대상 디렉토리를 재귀적으로 찾아 삭제 (동시성 최적화 버전)
 * @param {string} currentDir - 현재 순회 중인 디렉토리 경로
 * @param {string[]} targets - 삭제할 대상 목록
 * @param {number} depth - 현재 재귀 깊이, 과도한 재귀 방지
 */
async function cleanTargetsRecursively(currentDir, targets, depth = 0) {
  // 재귀 깊이 제한, 무한 재귀 방지
  if (depth > 10) {
    console.warn(`Max recursion depth reached at: ${currentDir}`);
    return;
  }

  let dirents;
  try {
    // withFileTypes 옵션을 사용하여 파일 유형 정보를 한 번에 가져옴으로써 이후 lstat 호출을 피함
    dirents = await fs.readdir(currentDir, { withFileTypes: true });
  } catch (error) {
    // 디렉토리를 읽을 수 없는 경우, 이미 삭제되었거나 권한이 부족할 수 있음
    console.warn(`Cannot read directory ${currentDir}: ${error.message}`);
    return;
  }

  // 일정한 수의 동시 작업으로 나누어 처리
  for (let i = 0; i < dirents.length; i += CONCURRENCY_LIMIT) {
    const batch = dirents.slice(i, i + CONCURRENCY_LIMIT);

    const tasks = batch.map(async (dirent) => {
      const item = dirent.name;
      const shouldRecurse = await processItem(currentDir, item, targets, depth);

      // 디렉토리이고 삭제되지 않았다면 재귀적으로 처리
      if (shouldRecurse && dirent.isDirectory()) {
        const itemPath = normalize(join(currentDir, item));
        return cleanTargetsRecursively(itemPath, targets, depth + 1);
      }

      return null;
    });

    // 현재 배치의 작업을 병렬로 실행
    const results = await Promise.allSettled(tasks);

    // 실패한 작업이 있는지 확인 (디버깅용 선택 사항)
    const failedTasks = results.filter(
      (result) => result.status === 'rejected',
    );
    if (failedTasks.length > 0) {
      console.warn(
        `${failedTasks.length} tasks failed in batch starting at index ${i} in directory: ${currentDir}`,
      );
    }
  }
}

(async function startCleanup() {
  // 삭제할 디렉토리 및 파일 이름
  const targets = ['node_modules', 'dist', '.turbo', 'dist.zip'];
  const deleteLockFile = process.argv.includes('--del-lock');
  const cleanupTargets = [...targets];

  if (deleteLockFile) {
    cleanupTargets.push('pnpm-lock.yaml');
  }

  console.log(
    `🚀 Starting cleanup of targets: ${cleanupTargets.join(', ')} from root: ${rootDir}`,
  );

  const startTime = Date.now();

  try {
    // 삭제할 대상 검색 시작
    console.log('📊 Scanning for cleanup targets...');

    await cleanTargetsRecursively(rootDir, cleanupTargets);

    const endTime = Date.now();
    const duration = (endTime - startTime) / 1000;

    console.log(
      `✨ Cleanup process completed successfully in ${duration.toFixed(2)}s`,
    );
  } catch (error) {
    console.error(`💥 Unexpected error during cleanup: ${error.message}`);
    process.exit(1);
  }
})();
