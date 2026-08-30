/**
 * 프로젝트관리 화면 공용 부품.
 *
 * 이식 전 Blazor 의 `Compnents/` 폴더에 있던 것들을 옮긴 것이다.
 * 이 넷이 서면 나머지 화면은 "어떤 프로시저를 어떤 파라미터로 부르는가" 만 적으면 된다.
 */
// CodeEditor 를 여기서 재수출하지 않는다 (40번 문서 3단계).
// 재수출하면 이 배럴을 스치는 projmng 화면 27개 전부가 모나코(수 MB)를
// 자기 청크에 끌고 갔다 — 편집기 없는 일정·할일 화면까지.
// 편집기를 쓰는 화면은 `#/components/code-editor` 에서 직접 가져온다.
export { default as CodeSelect } from './code-select.vue';
export { default as DynamicGrid } from './dynamic-grid.vue';
export { default as ErdDiagram } from './erd-diagram.vue';
export * from './erd-types';
// menu-tree 는 [프로젝트 화면 메뉴] 화면 하나만 쓰던 것이라 그 화면과 함께 걷어냈다.
// 메뉴는 포털이 단독으로 맡는다 (36-projmng-tobe-feature-cleanup.md 4단계).
export { default as SearchBar } from './search-bar.vue';
export { default as SplitPane } from './split-pane.vue';
export * from './use-proc-grid';
