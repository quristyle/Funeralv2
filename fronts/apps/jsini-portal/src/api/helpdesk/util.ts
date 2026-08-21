/**
 * 유틸리티 API — ASCII/바이너리 파서, MC 모델(프로토콜 규격) 정의, 바이너리 샘플.
 *
 * 필드명은 HelpDeskServer 의 실제 응답에 맞춘 것이다
 * (`Utilities/MC_Models.cs` + `Endpoints/UtilEndpoints.cs` 의 DTO).
 */
import { helpdeskClient } from './request';

/** 태그 값의 해석 방식. 서버 DataTypeEnum 과 같다. */
export const TAG_DATA_TYPES = [
  'NUMBER',
  'DATE',
  'DATETIME',
  'LENGTH',
  'DESTINATION',
  'SOURCE',
  'CONTROL',
  'SUB_APP',
  'REQUEST_CODE',
  'RESPONSE_CODE',
  'APP_CODE',
  'DATA',
  'DATA_SINGLE',
  'ENERGY_LIMIT',
] as const;

/** 태그 데이터 타입 */
export type TagDataType = (typeof TAG_DATA_TYPES)[number];

/** 전문에서 뽑아낼 태그(필드) 정의 */
export interface TagItem {
  dataType?: TagDataType | string;
  desc: string;
  id: number;
  parseItemId?: number;
  sortNo?: number;
  /** 블록 내 시작 바이트 위치(0부터) */
  tagIdx?: number;
  /** 읽을 바이트 수 */
  tagLength?: number;
}

/** 전문 종류를 가려내는 규칙(키 바이트 + 블록 분해 방식) */
export interface ParseItem {
  /** 블록 분해 길이. '8' 또는 '4,2,1,1' 처럼 콤마로 구분한다. */
  blocParseLength?: string;
  /** 블록 해석 방식 — number 또는 date */
  blocParseType?: string;
  desc: string;
  id: number;
  /** 키가 되는 바이트 값 목록 */
  keys?: number[];
  /** 키 바이트의 인덱스 위치(0부터) */
  keyIdx?: number;
  mC_ModelsId?: number;
  /** 수신/송신 구분 */
  pTYPE?: string;
  tagItems?: TagItem[];
}

/** ACK 프레임 판정 규칙 */
export interface AckFind {
  endCalcArrow?: string;
  endCalcEquals?: string;
  endCalcIdx?: string;
  endCalcTarget?: string;
  endCalcValue?: string;
  id: number;
  mC_ModelsId?: number;
  startCalcArrow?: string;
  startCalcEquals?: string;
  startCalcIdx?: string;
  startCalcTarget?: string;
  startCalcValue?: string;
}

/** 보관된 전문 샘플. 목록 조회에는 content 가 빠져 있다. */
export interface BinarySample {
  content?: string;
  createdAt?: string;
  id: number;
  mC_ModelsId?: number;
  title: string;
}

/** 프로토콜 규격(모델) */
export interface McModel {
  ackFinds?: AckFind[];
  id: number;
  mcName: string;
  parseItems?: ParseItem[];
  samples?: BinarySample[];
  /** 전문 시작을 알리는 키 바이트 */
  startKey?: string;
}

/** 파서 호출 옵션 */
export interface ParseOptions {
  byteGroup?: number;
  content: string;
  /** 걸러낼 헤더 — RX / TX / Count / CRC */
  heads?: string[];
  interpretationType?: string;
  isLittleEndian?: boolean;
  /** 모델 규격을 적용해 해석할지 */
  isProtocolMode?: boolean;
  isRxLengthFirst?: boolean;
  /** 적용할 모델 이름(mcName) */
  model?: string;
}

// ============================================================
// 파서
// ============================================================

/** ASCII 전문 파싱 */
export async function parseAscii(payload: ParseOptions) {
  return helpdeskClient.post<any>('/utils/parse-ascii', {
    heads: [],
    interpretationType: 'HEX',
    ...payload,
  });
}

/** 바이너리 전문 파싱 */
export async function parseBinary(payload: ParseOptions) {
  return helpdeskClient.post<any>('/utils/parse-binary', {
    byteGroup: 4,
    heads: [],
    interpretationType: 'HEX',
    isLittleEndian: true,
    isProtocolMode: false,
    isRxLengthFirst: false,
    ...payload,
  });
}

// ============================================================
// MC 모델
// ============================================================

/** 모델 목록 (하위 항목 없음) */
export async function getMcModels() {
  return helpdeskClient.get<McModel[]>('/utils/mc-models');
}

/** 파싱 항목·태그·샘플까지 포함한 모델 목록 */
export async function getMcModelsFull() {
  return helpdeskClient.get<McModel[]>('/utils/mc-models-full');
}

/** 모델 생성 */
export async function createMcModel(payload: {
  mcName: string;
  startKey?: string;
}) {
  return helpdeskClient.post<McModel>('/utils/mc-models', payload);
}

/** 모델 수정 */
export async function updateMcModel(
  id: number,
  payload: { mcName: string; startKey?: string },
) {
  return helpdeskClient.put<McModel>(`/utils/mc-models/${id}`, payload);
}

/** 모델 삭제 */
export async function deleteMcModel(id: number) {
  return helpdeskClient.delete(`/utils/mc-models/${id}`);
}

// ============================================================
// 파싱 항목 · 태그
// ============================================================

/** 파싱 항목 페이로드. keys 는 '80,81' 처럼 콤마로 이어 보낸다. */
export interface ParseItemPayload {
  blocParseLength?: string;
  blocParseType?: string;
  desc: string;
  keyIdx?: number;
  keys?: string;
  ptype?: string;
}

/** 파싱 항목 추가 */
export async function createParseItem(
  mcModelId: number,
  payload: ParseItemPayload,
) {
  return helpdeskClient.post<ParseItem>(
    `/utils/mc-models/${mcModelId}/parse-items`,
    payload,
  );
}

/** 파싱 항목 수정 */
export async function updateParseItem(id: number, payload: ParseItemPayload) {
  return helpdeskClient.put<ParseItem>(`/utils/parse-items/${id}`, payload);
}

/** 파싱 항목 삭제 */
export async function deleteParseItem(id: number) {
  return helpdeskClient.delete(`/utils/parse-items/${id}`);
}

/** 태그 항목 페이로드 */
export interface TagItemPayload {
  dataType?: string;
  desc: string;
  sortNo?: number;
  tagIdx?: number;
  tagLength?: number;
}

/** 태그 항목 추가 */
export async function createTagItem(
  parseItemId: number,
  payload: TagItemPayload,
) {
  return helpdeskClient.post<TagItem>(
    `/utils/parse-items/${parseItemId}/tag-items`,
    payload,
  );
}

/** 태그 항목 수정 */
export async function updateTagItem(id: number, payload: TagItemPayload) {
  return helpdeskClient.put<TagItem>(`/utils/tag-items/${id}`, payload);
}

/** 태그 항목 삭제 */
export async function deleteTagItem(id: number) {
  return helpdeskClient.delete(`/utils/tag-items/${id}`);
}

/** 태그 항목 순서 일괄 변경 */
export async function reorderTagItems(items: { id: number; sortNo: number }[]) {
  return helpdeskClient.put('/utils/tag-items/reorder', items);
}

// ============================================================
// ACK 매칭 규칙
// ============================================================

/** ACK 판정 규칙 페이로드 */
export interface AckFindPayload {
  endCalcArrow: string;
  endCalcEquals: string;
  endCalcIdx: string;
  endCalcTarget: string;
  endCalcValue: string;
  startCalcArrow: string;
  startCalcEquals: string;
  startCalcIdx: string;
  startCalcTarget: string;
  startCalcValue: string;
}

/** ACK 규칙 추가 */
export async function createAckFind(
  mcModelId: number,
  payload: AckFindPayload,
) {
  return helpdeskClient.post<AckFind>(
    `/utils/mc-models/${mcModelId}/ack-finds`,
    payload,
  );
}

/** ACK 규칙 수정 */
export async function updateAckFind(id: number, payload: AckFindPayload) {
  return helpdeskClient.put<AckFind>(`/utils/ack-finds/${id}`, payload);
}

/** ACK 규칙 삭제 */
export async function deleteAckFind(id: number) {
  return helpdeskClient.delete(`/utils/ack-finds/${id}`);
}

// ============================================================
// 바이너리 샘플
// ============================================================

/** 모델별 샘플 목록 (본문 제외) */
export async function getSamples(mcModelId: number) {
  return helpdeskClient.get<BinarySample[]>(
    `/utils/mc-models/${mcModelId}/samples`,
  );
}

/** 샘플 단건 조회 (본문 포함) */
export async function getSample(id: number) {
  return helpdeskClient.get<BinarySample>(`/utils/samples/${id}`);
}

/** 샘플 저장 */
export async function createSample(
  mcModelId: number,
  payload: { content: string; title: string },
) {
  return helpdeskClient.post<BinarySample>(
    `/utils/mc-models/${mcModelId}/samples`,
    payload,
  );
}

/** 샘플 수정 */
export async function updateSample(
  id: number,
  payload: { content: string; title: string },
) {
  return helpdeskClient.put<BinarySample>(`/utils/samples/${id}`, payload);
}

/** 샘플 삭제 */
export async function deleteSample(id: number) {
  return helpdeskClient.delete(`/utils/samples/${id}`);
}
