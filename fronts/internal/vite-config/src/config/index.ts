import type { DefineConfig, VbenViteConfig } from '../typing';

import { existsSync } from 'node:fs';
import { join } from 'node:path';

import { defineApplicationConfig } from './application';
import { defineLibraryConfig } from './library';

export * from './application';
export * from './library';

function defineConfig(
  userConfigPromise?: DefineConfig,
  type: 'application' | 'auto' | 'library' = 'auto',
): VbenViteConfig {
  let projectType = type;

  // 패키지에 index.html이 존재하는지 여부에 따라 타입을 자동으로 판단합니다.
  if (projectType === 'auto') {
    const htmlPath = join(process.cwd(), 'index.html');
    projectType = existsSync(htmlPath) ? 'application' : 'library';
  }

  switch (projectType) {
    case 'application': {
      return defineApplicationConfig(userConfigPromise);
    }
    case 'library': {
      return defineLibraryConfig(userConfigPromise);
    }
    default: {
      throw new Error(`Unsupported project type: ${projectType}`);
    }
  }
}

export { defineConfig };
