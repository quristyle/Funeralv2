/**
 * 유틸리티 API — ASCII/바이너리 파서, MC 모델 정의, 외부 고객사(oadr) 리포트 프록시.
 */
import { helpdeskClient } from './request';

/** MC 모델 (파싱 규칙 묶음) */
export interface McModel {
  createdAt?: string;
  description?: string;
  id: number;
  modelName: string;
}

/** 파싱 항목 */
export interface ParseItem {
  dataType?: string;
  id: number;
  itemName: string;
  length?: number;
  mcModelId: number;
  offset?: number;
  sortOrder?: number;
}

/** 태그 항목 */
export interface TagItem {
  id: number;
  parseItemId: number;
  sortOrder?: number;
  tagName: string;
  tagValue?: string;
}

/** ACK 매칭 규칙 */
export interface AckFind {
  id: number;
  mcModelId: number;
  pattern?: string;
}

/** 바이너리 샘플 */
export interface BinarySample {
  createdAt?: string;
  id: number;
  mcModelId: number;
  payload?: string;
  sampleName: string;
}

// ============================================================
// 파서
// ============================================================

/** ASCII 전문 파싱 */
export async function parseAscii(payload: Record<string, any>) {
  return helpdeskClient.post<any>('/utils/parse-ascii', payload);
}

/** 바이너리 전문 파싱 */
export async function parseBinary(payload: Record<string, any>) {
  return helpdeskClient.post<any>('/utils/parse-binary', payload);
}

// ============================================================
// MC 모델
// ============================================================

/** MC 모델 목록 */
export async function getMcModels() {
  return helpdeskClient.get<McModel[]>('/utils/mc-models');
}

/** 하위 항목까지 포함한 MC 모델 목록 */
export async function getMcModelsFull() {
  return helpdeskClient.get<any[]>('/utils/mc-models-full');
}

/** MC 모델 생성 */
export async function createMcModel(payload: Partial<McModel>) {
  return helpdeskClient.post<McModel>('/utils/mc-models', payload);
}

/** MC 모델 수정 */
export async function updateMcModel(id: number, payload: Partial<McModel>) {
  return helpdeskClient.put<McModel>(`/utils/mc-models/${id}`, payload);
}

/** MC 모델 삭제 */
export async function deleteMcModel(id: number) {
  return helpdeskClient.delete(`/utils/mc-models/${id}`);
}

// ============================================================
// 파싱 항목 · 태그
// ============================================================

/** 파싱 항목 추가 */
export async function createParseItem(
  mcModelId: number,
  payload: Partial<ParseItem>,
) {
  return helpdeskClient.post<ParseItem>(
    `/utils/mc-models/${mcModelId}/parse-items`,
    payload,
  );
}

/** 파싱 항목 수정 */
export async function updateParseItem(id: number, payload: Partial<ParseItem>) {
  return helpdeskClient.put<ParseItem>(`/utils/parse-items/${id}`, payload);
}

/** 파싱 항목 삭제 */
export async function deleteParseItem(id: number) {
  return helpdeskClient.delete(`/utils/parse-items/${id}`);
}

/** 태그 항목 추가 */
export async function createTagItem(
  parseItemId: number,
  payload: Partial<TagItem>,
) {
  return helpdeskClient.post<TagItem>(
    `/utils/parse-items/${parseItemId}/tag-items`,
    payload,
  );
}

/** 태그 항목 수정 */
export async function updateTagItem(id: number, payload: Partial<TagItem>) {
  return helpdeskClient.put<TagItem>(`/utils/tag-items/${id}`, payload);
}

/** 태그 항목 삭제 */
export async function deleteTagItem(id: number) {
  return helpdeskClient.delete(`/utils/tag-items/${id}`);
}

/** 태그 항목 순서 변경 */
export async function reorderTagItems(items: { id: number; sortOrder: number }[]) {
  return helpdeskClient.put('/utils/tag-items/reorder', items);
}

// ============================================================
// ACK 매칭
// ============================================================

/** ACK 규칙 추가 */
export async function createAckFind(
  mcModelId: number,
  payload: Partial<AckFind>,
) {
  return helpdeskClient.post<AckFind>(
    `/utils/mc-models/${mcModelId}/ack-finds`,
    payload,
  );
}

/** ACK 규칙 수정 */
export async function updateAckFind(id: number, payload: Partial<AckFind>) {
  return helpdeskClient.put<AckFind>(`/utils/ack-finds/${id}`, payload);
}

/** ACK 규칙 삭제 */
export async function deleteAckFind(id: number) {
  return helpdeskClient.delete(`/utils/ack-finds/${id}`);
}

// ============================================================
// 바이너리 샘플
// ============================================================

/** 모델별 샘플 목록 */
export async function getSamples(mcModelId: number) {
  return helpdeskClient.get<BinarySample[]>(
    `/utils/mc-models/${mcModelId}/samples`,
  );
}

/** 샘플 단건 조회 */
export async function getSample(id: number) {
  return helpdeskClient.get<BinarySample>(`/utils/samples/${id}`);
}

/** 샘플 저장 */
export async function createSample(
  mcModelId: number,
  payload: Partial<BinarySample>,
) {
  return helpdeskClient.post<BinarySample>(
    `/utils/mc-models/${mcModelId}/samples`,
    payload,
  );
}

/** 샘플 수정 */
export async function updateSample(
  id: number,
  payload: Partial<BinarySample>,
) {
  return helpdeskClient.put<BinarySample>(`/utils/samples/${id}`, payload);
}

/** 샘플 삭제 */
export async function deleteSample(id: number) {
  return helpdeskClient.delete(`/utils/samples/${id}`);
}
