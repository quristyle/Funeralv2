import { execSync } from 'node:child_process';

import { getPackagesSync } from '@vben/node-utils';

const { packages } = getPackagesSync();

const allowedScopes = [
  ...packages.map((pkg) => pkg.packageJson.name),
  'project',
  'style',
  'lint',
  'ci',
  'dev',
  'deploy',
  'other',
];

// precomputed scope
const scopeComplete = execSync('git status --porcelain || true')
  .toString()
  .trim()
  .split('\n')
  .find((r) => ~r.indexOf('M  src'))
  ?.replaceAll(/(\/)/g, '%%')
  ?.match(/src%%((\w|-)*)/)?.[1]
  ?.replace(/s$/, '');

/**
 * @type {import('cz-git').UserConfig}
 */
const userConfig = {
  extends: ['@commitlint/config-conventional'],
  plugins: ['commitlint-plugin-function-rules'],
  prompt: {
    /** @use `pnpm commit :f` */
    alias: {
      b: 'build: bump dependencies',
      c: 'chore: update config',
      f: 'docs: fix typos',
      r: 'docs: update README',
      s: 'style: update code format',
    },
    allowCustomIssuePrefixs: false,
    // scopes: [...scopes, 'mock'],
    allowEmptyIssuePrefixs: false,
    customScopesAlign: scopeComplete ? 'bottom' : 'top',
    defaultScope: scopeComplete,
    // English
    typesAppend: [
      { name: 'workflow: workflow improvements', value: 'workflow' },
      { name: 'types:    type definition file changes', value: 'types' },
    ],

    // 한글-영어 대조 버전
    // messages: {
    //   type: '제출할 유형을 선택하세요 :',
    //   scope: '제출 범위를 선택하세요 (선택 사항):',
    //   customScope: '사용자 지정 제출 범위를 입력하세요 :',
    //   subject: '변경 사항에 대한 간결한 설명을 작성하세요 :\n',
    //   body: '변경 사항에 대한 자세한 설명을 작성하세요 (선택 사항). "|"를 사용하여 줄바꿈 가능 :\n',
    //   breaking: '호환되지 않는 중대한 변경 사항을 나열하세요 (선택 사항). "|"를 사용하여 줄바꿈 가능 :\n',
    //   footerPrefixsSelect: '연관된 issue 접두사를 선택하세요 (선택 사항):',
    //   customFooterPrefixs: '사용자 지정 issue 접두사를 입력하세요 :',
    //   footer: '연관된 issue를 나열하세요 (선택 사항) 예: #31, #I3244 :\n',
    //   confirmCommit: 'commit을 제출하거나 수정하시겠습니까?',
    // },
    // types: [
    //   { value: 'feat', name: 'feat:     새로운 기능 추가' },
    //   { value: 'fix', name: 'fix:      버그 수정' },
    //   { value: 'docs', name: 'docs:     문서 변경' },
    //   { value: 'style', name: 'style:    코드 포맷팅' },
    //   { value: 'refactor', name: 'refactor: 코드 리팩토링' },
    //   { value: 'perf', name: 'perf:     성능 최적화' },
    //   { value: 'test', name: 'test:     테스트 추가 또는 수정' },
    //   { value: 'build', name: 'build:    빌드 프로세스, 외부 종속성 변경 (예: npm 패키지 업그레이드, 빌드 설정 변경 등)' },
    //   { value: 'ci', name: 'ci:       CI 설정, 스크립트 수정' },
    //   { value: 'revert', name: 'revert:   commit 되돌리기' },
    //   { value: 'chore', name: 'chore:    빌드 프로세스 또는 보조 도구 및 라이브러리 변경 (소스 파일, 테스트 케이스에 영향 없음)' },
    //   { value: 'wip', name: 'wip:      개발 중' },
    //   { value: 'workflow', name: 'workflow: 작업 프로세스 개선' },
    //   { value: 'types', name: 'types:    타입 정의 파일 수정' },
    // ],
    // emptyScopesAlias: 'empty:      작성 안 함',
    // customScopesAlias: 'custom:     사용자 지정',
  },
  rules: {
    /**
     * type[scope]: [function] description
     *
     * ^^^^^^^^^^^^^^ empty line.
     * - Something here
     */
    'body-leading-blank': [2, 'always'],
    /**
     * type[scope]: [function] description
     *
     * - something here
     *
     * ^^^^^^^^^^^^^^
     */
    'footer-leading-blank': [1, 'always'],
    /**
     * type[scope]: [function] description
     *      ^^^^^
     */
    'function-rules/scope-enum': [
      2, // level: error
      'always',
      (parsed) => {
        if (!parsed.scope || allowedScopes.includes(parsed.scope)) {
          return [true];
        }

        return [false, `scope must be one of ${allowedScopes.join(', ')}`];
      },
    ],
    /**
     * type[scope]: [function] description [No more than 108 characters]
     *      ^^^^^
     */
    'header-max-length': [2, 'always', 108],

    'scope-enum': [0],
    'subject-case': [0],
    'subject-empty': [2, 'never'],
    'type-empty': [2, 'never'],
    /**
     * type[scope]: [function] description
     * ^^^^
     */
    'type-enum': [
      2,
      'always',
      [
        'feat',
        'fix',
        'perf',
        'style',
        'docs',
        'test',
        'refactor',
        'build',
        'ci',
        'chore',
        'revert',
        'types',
        'release',
      ],
    ],
  },
};

export default userConfig;
