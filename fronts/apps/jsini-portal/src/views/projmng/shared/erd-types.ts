/**
 * ERD 저장 형식 — 이식 전 `ProjModel/ErdInfo.cs` 와 1:1.
 *
 * DB(`sp_dev_db_prop_exec` 의 `db_pvalue`, `db_pkey='erd'`)에 이 형태의 JSON 이
 * 이미 쌓여 있다. 필드 이름을 바꾸면 기존 다이어그램을 읽지 못한다.
 */
export interface ErdEntity {
  id: string;
  name: string;
  desc?: string;
  fields?: string[];
  x?: number;
  y?: number;
  w?: number;
  h?: number;
}

export interface ErdRelation {
  from: string;
  to: string;
  label?: string;
}

export interface ErdModel {
  entities: ErdEntity[];
  relations: ErdRelation[];
}

/** 문자열로 저장된 ERD JSON 을 안전하게 읽는다. 깨져 있으면 빈 모델을 준다. */
export function parseErdModel(raw?: null | string): ErdModel {
  if (!raw) return { entities: [], relations: [] };
  try {
    const parsed = JSON.parse(raw) as Partial<ErdModel>;
    return {
      entities: parsed.entities ?? [],
      relations: parsed.relations ?? [],
    };
  } catch {
    return { entities: [], relations: [] };
  }
}
