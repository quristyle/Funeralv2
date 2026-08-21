/**
 * 헬프데스크 API — JinReception 백엔드(HelpDeskServer)를 호출하는 모듈 모음.
 *
 * 모든 호출은 게이트웨이의 `/api/helpdesk` 라우트를 지난다.
 * 인증은 funeralv2 계정으로 단일화되어 있어 AuthServer 토큰을 그대로 사용한다.
 */
export * from './admin';
export * from './improvement';
export * from './oadr';
export * from './org';
export * from './request';
export * from './types';
export * from './util';
export * from './work';
