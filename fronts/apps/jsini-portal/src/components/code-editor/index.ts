/**
 * Monaco 기반 코드 편집기.
 *
 * 이식 전 두 시스템이 모두 Monaco 를 쓰고 있었다.
 *   - 프로젝트관리(Blazor WASM) `QuriCodeEditor` — SQL·코드 편집
 *   - 헬프데스크(JinReception) 바이너리 파서 — 전문 붙여넣기
 *
 * 두 화면군이 같은 부품을 쓰도록 여기에 둔다.
 */
export { default as CodeEditor } from './code-editor.vue';
export { monaco, setupMonaco, toMonacoLanguage } from './monaco-setup';
