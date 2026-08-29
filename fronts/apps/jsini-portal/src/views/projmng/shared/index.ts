/**
 * 프로젝트관리 화면 공용 부품.
 *
 * 이식 전 Blazor 의 `Compnents/` 폴더에 있던 것들을 옮긴 것이다.
 * 이 넷이 서면 나머지 화면은 "어떤 프로시저를 어떤 파라미터로 부르는가" 만 적으면 된다.
 */
// 코드 편집기는 헬프데스크 화면도 쓰므로 공용 컴포넌트로 옮겼다.
// 프로젝트관리 화면들이 `../shared` 에서 그대로 가져다 쓰도록 여기서 다시 내보낸다.
export { CodeEditor } from '#/components/code-editor';
export { default as CodeSelect } from './code-select.vue';
export { default as DynamicGrid } from './dynamic-grid.vue';
export { default as ErdDiagram } from './erd-diagram.vue';
export * from './erd-types';
// menu-tree 는 [프로젝트 화면 메뉴] 화면 하나만 쓰던 것이라 그 화면과 함께 걷어냈다.
// 메뉴는 포털이 단독으로 맡는다 (36-projmng-tobe-feature-cleanup.md 4단계).
export { default as SearchBar } from './search-bar.vue';
export { default as SplitPane } from './split-pane.vue';
export * from './use-proc-grid';
