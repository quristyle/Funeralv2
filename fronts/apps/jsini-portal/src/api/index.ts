/**
 * API 배럴.
 *
 * JSini 포털은 여러 MSA 를 한 화면에 담는다. 어느 API 가 어느 시스템 것인지
 * 폴더로 구분한다.
 *   core     — 프레임워크 코어(인증·사용자·메뉴·타임존)
 *   portal   — JSini 포털 공통 업무(시스템 관리, 게이트웨이 상태, AI)
 *   funeral  — 장례식장 MSA
 *   helpdesk — 헬프데스크 MSA
 *   projmng  — 프로젝트관리 MSA
 *
 * 배럴에는 이름 충돌이 없는 것만 올린다. helpdesk / projmng 는 화면에서
 * `#/api/helpdesk`, `#/api/projmng` 로 직접 가져다 쓴다.
 */
export * from './core';
export * from './examples';
export * from './portal/system';
