import type { CAC } from 'cac';

import { access, mkdtemp, readFile, rm } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { tmpdir } from 'node:os';
import { extname, join } from 'node:path';

import { execa, getStagedFiles } from '@vben/node-utils';

const require = createRequire(import.meta.url);
const circularScannerCli =
  require.resolve('circular-dependency-scanner/dist/cli.js');

// 기본 설정
const DEFAULT_CONFIG = {
  allowedExtensions: ['.cjs', '.js', '.jsx', '.mjs', '.ts', '.tsx', '.vue'],
  ignoreDirs: [
    'dist',
    '.turbo',
    'output',
    '.cache',
    'scripts',
    'internal',
    'packages/effects/request/src/',
    'packages/@core/ui-kit/menu-ui/src/',
    'packages/@core/ui-kit/popup-ui/src/',
  ],
  threshold: 0, // 순환 의존성 임계값
} as const;

// 타입 정의
type CircularDependencyResult = string[];

interface CheckCircularConfig {
  allowedExtensions?: string[];
  ignoreDirs?: string[];
  threshold?: number;
}

interface CommandOptions {
  config?: CheckCircularConfig;
  staged: boolean;
  verbose: boolean;
}

// 캐시 메커니즘
const cache = new Map<string, CircularDependencyResult[]>();

async function detectCircularDependencies({
  cwd,
  ignorePattern,
  staged,
}: {
  cwd: string;
  ignorePattern: string;
  staged: boolean;
}): Promise<CircularDependencyResult[]> {
  const tempDir = await mkdtemp(join(tmpdir(), 'vsh-check-circular-'));
  const outputFile = join(tempDir, 'circles.json');

  try {
    const args = [circularScannerCli, cwd, '--output', outputFile];

    if (staged) {
      args.push('--absolute');
    }

    args.push('--ignore', ignorePattern);

    await execa(process.execPath, args, {
      cwd,
    });

    await access(outputFile);
    const output = await readFile(outputFile, 'utf8');
    return JSON.parse(output) as CircularDependencyResult[];
  } catch (error) {
    if ((error as NodeJS.ErrnoException)?.code === 'ENOENT') {
      return [];
    }
    throw error;
  } finally {
    await rm(tempDir, { force: true, recursive: true });
  }
}

/**
 * 순환 의존성 출력 포맷팅
 * @param circles - 순환 의존성 결과
 */
function formatCircles(circles: CircularDependencyResult[]): void {
  if (circles.length === 0) {
    console.log('✅ No circular dependencies found');
    return;
  }

  console.log('⚠️ Circular dependencies found:');
  circles.forEach((circle, index) => {
    console.log(`\nCircular dependency #${index + 1}:`);
    circle.forEach((file) => console.log(`  → ${file}`));
  });
}

/**
 * 프로젝트 내 순환 의존성 검사
 * @param options - 검사 옵션
 * @param options.staged - 스테이징된 파일만 검사할지 여부
 * @param options.verbose - 상세 정보 표시 여부
 * @param options.config - 사용자 정의 설정
 * @returns Promise<void>
 */
async function checkCircular({
  config = {},
  staged,
  verbose,
}: CommandOptions): Promise<void> {
  try {
    // 설정 병합
    const finalConfig = {
      ...DEFAULT_CONFIG,
      ...config,
    };

    // 무시 패턴 생성
    const ignorePattern = `**/{${finalConfig.ignoreDirs.join(',')}}/**`;

    // 캐시 확인
    const cacheKey = `${staged}-${process.cwd()}-${ignorePattern}`;
    if (cache.has(cacheKey)) {
      const cachedResults = cache.get(cacheKey);
      if (cachedResults && verbose) {
        formatCircles(cachedResults);
      }
      return;
    }

    // 순환 의존성 감지
    const results = await detectCircularDependencies({
      cwd: process.cwd(),
      ignorePattern,
      staged,
    });

    if (staged) {
      let files = await getStagedFiles();
      const allowedExtensions = new Set(finalConfig.allowedExtensions);

      // 파일 목록 필터링
      files = files.filter((file) => allowedExtensions.has(extname(file)));

      const circularFiles: CircularDependencyResult[] = [];

      for (const file of files) {
        for (const result of results) {
          const resultFiles = result.flat();
          if (resultFiles.includes(file)) {
            circularFiles.push(result);
          }
        }
      }

      // 캐시 업데이트
      cache.set(cacheKey, circularFiles);
      if (verbose) {
        formatCircles(circularFiles);
      }
    } else {
      // 캐시 업데이트
      cache.set(cacheKey, results);
      if (verbose) {
        formatCircles(results);
      }
    }

    // 순환 의존성이 발견되면 경고 메시지 출력
    if (results.length > 0) {
      console.log(
        '\n⚠️ Warning: Circular dependencies found, please check and fix',
      );
    }
  } catch (error) {
    console.error(
      '❌ Error checking circular dependencies:',
      error instanceof Error ? error.message : error,
    );
  }
}

/**
 * 순환 의존성 검사 명령어 정의
 * @param cac - CAC 인스턴스
 */
function defineCheckCircularCommand(cac: CAC): void {
  cac
    .command('check-circular')
    .option('--staged', 'Only check staged files')
    .option('--verbose', 'Show detailed information')
    .option('--threshold <number>', 'Threshold for circular dependencies', {
      default: 0,
    })
    .option('--ignore-dirs <dirs>', 'Directories to ignore, comma separated')
    .usage('Analyze project circular dependencies')
    .action(async ({ ignoreDirs, staged, threshold, verbose }) => {
      const config: CheckCircularConfig = {
        threshold: Number(threshold),
        ...(ignoreDirs && { ignoreDirs: ignoreDirs.split(',') }),
      };

      await checkCircular({
        config,
        staged,
        verbose: verbose ?? true,
      });
    });
}

export { type CheckCircularConfig, defineCheckCircularCommand };
