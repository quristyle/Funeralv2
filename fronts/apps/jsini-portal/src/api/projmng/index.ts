/**
 * 프로젝트관리 API — ProjMngServer(구 ProjMngServer + ProjMngWasm) 를 호출하는 모듈.
 *
 * 모든 호출은 게이트웨이의 `/api/projmng` 라우트를 지난다.
 * 인증은 포털 계정(AuthServer 토큰)으로 단일화되어 있다 —
 * 이식 전에 있던 자체 로그인(`sp_proj_login`)과 자체 메뉴·사용자그룹은 쓰지 않는다.
 */
export * from './proc';
export * from './request';
export * from './types';
