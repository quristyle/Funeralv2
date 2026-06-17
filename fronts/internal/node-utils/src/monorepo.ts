import type { Package } from '@manypkg/get-packages';

import { existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';

import * as manypkg from '@manypkg/get-packages';
const { getPackages: getPackagesFunc, getPackagesSync: getPackagesSyncFunc } =
  manypkg;

/**
 * 모노레포 루트 디렉토리 찾기
 * @param cwd
 */
function findMonorepoRoot(cwd: string = process.cwd()) {
  let currentDir = resolve(cwd);

  while (true) {
    if (existsSync(join(currentDir, 'pnpm-lock.yaml'))) {
      return currentDir;
    }

    const parentDir = dirname(currentDir);
    if (parentDir === currentDir) {
      return '';
    }

    currentDir = parentDir;
  }
}

/**
 * 모노레포의 모든 패키지 가져오기
 */
function getPackagesSync() {
  const root = findMonorepoRoot();
  return getPackagesSyncFunc(root);
}

/**
 * 모노레포의 모든 패키지 가져오기
 */
async function getPackages() {
  const root = findMonorepoRoot();

  return await getPackagesFunc(root);
}

/**
 * 모노레포의 특정 패키지 가져오기
 */
async function getPackage(pkgName: string) {
  const { packages } = await getPackages();
  return packages.find((pkg: Package) => pkg.packageJson.name === pkgName);
}

export { findMonorepoRoot, getPackage, getPackages, getPackagesSync };
