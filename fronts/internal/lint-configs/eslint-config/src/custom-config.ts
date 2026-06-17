import type { Linter } from 'eslint';

const restrictedImportIgnores = ['**/vite.config.mts'];

const customConfig: Linter.Config[] = [
  // shadcn-ui 내부 컴포넌트는 자동 생성되므로 제한을 많이 두지 않음
  {
    files: ['packages/@core/ui-kit/shadcn-ui/**/**'],
    rules: {
      'vue/require-default-prop': 'off',
    },
  },
  {
    files: [
      'apps/**/**',
      'packages/effects/**/**',
      'packages/utils/**/**',
      'packages/types/**/**',
      'packages/locales/**/**',
    ],
    ignores: restrictedImportIgnores,
    rules: {
      'perfectionist/sort-interfaces': 'off',
    },
  },
  {
    // apps 내부의 일부 기본 규칙
    files: ['apps/**/**'],
    ignores: restrictedImportIgnores,
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['#/api/*'],
              message:
                'The #/api package cannot be imported, please use the @core package itself',
            },
            {
              group: ['#/layouts/*'],
              message:
                'The #/layouts package cannot be imported, please use the @core package itself',
            },
            {
              group: ['#/locales/*'],
              message:
                'The #/locales package cannot be imported, please use the @core package itself',
            },
            {
              group: ['#/stores/*'],
              message:
                'The #/stores package cannot be imported, please use the @core package itself',
            },
          ],
        },
      ],
    },
  },
  {
    // @core 내부 컴포넌트, @vben/* 패키지를 가져올 수 없음
    files: ['packages/@core/**/**'],
    ignores: restrictedImportIgnores,
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@vben/*'],
              message:
                'The @core package cannot import the @vben package, please use the @core package itself',
            },
          ],
        },
      ],
    },
  },
  {
    // @core/shared 내부 컴포넌트, @vben/* 또는 @vben-core/* 패키지를 가져올 수 없음
    files: ['packages/@core/base/**/**'],
    ignores: restrictedImportIgnores,
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@vben/*', '@vben-core/*'],
              message:
                'The @vben-core/shared package cannot import the @vben package, please use the @core/shared package itself',
            },
          ],
        },
      ],
    },
  },

  {
    // @vben/* 패키지를 가져올 수 없음
    files: [
      'packages/types/**/**',
      'packages/utils/**/**',
      'packages/icons/**/**',
      'packages/constants/**/**',
      'packages/styles/**/**',
      'packages/stores/**/**',
      'packages/preferences/**/**',
      'packages/locales/**/**',
    ],
    ignores: restrictedImportIgnores,
    rules: {
      'no-restricted-imports': [
        'error',
        {
          patterns: [
            {
              group: ['@vben/*'],
              message:
                'The @vben package cannot be imported, please use the @core package itself',
            },
          ],
        },
      ],
    },
  },
  // 백엔드 모의(mock) 코드, 많은 규칙이 필요하지 않음
  {
    files: ['apps/backend-mock/**/**', 'docs/**/**'],
    rules: {
      'no-console': 'off',
    },
  },
  {
    files: ['**/**/playwright.config.ts'],
    rules: {
      'no-console': 'off',
    },
  },
  {
    files: ['internal/**/**', 'scripts/**/**'],
    rules: {
      'no-console': 'off',
    },
  },
  {
    files: ['packages/@core/base/shared/src/utils/inference.ts'],
    rules: {
      'vue/prefer-import-from-vue': 'off',
    },
  },
];

export { customConfig };
