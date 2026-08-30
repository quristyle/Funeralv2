import { requestClient } from '#/api/request';

export namespace MenuFavoriteApi {
  /**
   * 즐겨찾기 메뉴 한 건.
   *
   * `title` 은 다국어 키일 수도 있고 그대로 쓸 글자일 수도 있다.
   * 화면에서는 `$tIfKey` 로 감싸 쓴다(메뉴 제목과 같은 규칙).
   */
  export interface MenuFavorite {
    icon?: null | string;
    menuId: string;
    name: string;
    /** 메뉴 경로. 즐겨찾기 여부 판단과 이동에 쓰는 값이다 */
    path: string;
    sortOrder: number;
    title?: null | string;
  }
}

/**
 * 응답에서 목록을 꺼낸다.
 *
 * 서버는 `{ data: { result: [...], page: {...} } }` 로 감싸 보내고 `requestClient` 는
 * `data` 까지만 벗겨 준다. 그래서 여기서 `result` 를 한 겹 더 벗긴다
 * (메뉴 관리 화면의 `getMenuItems` 와 같은 처리다).
 *
 * `requestListClient`(dataField `data.result`)를 쓰면 될 것 같지만 **동작하지 않는다** —
 * 인터셉터가 `responseData[dataField]` 로 한 단계만 찾으므로 점이 든 경로는 undefined 가 된다.
 */
function unwrap(response: any): MenuFavoriteApi.MenuFavorite[] {
  if (Array.isArray(response)) return response;
  if (Array.isArray(response?.result)) return response.result;
  if (Array.isArray(response?.data?.result)) return response.data.result;
  return [];
}

/**
 * 즐겨찾기 목록·추가·해제.
 *
 * 세 함수 모두 **갱신된 목록 전체**를 돌려준다(서버가 그렇게 응답한다).
 * 그래서 추가·해제 뒤에 목록을 다시 받으러 한 번 더 부를 필요가 없다.
 *
 * 대상은 **경로**로 지정한다. 탭이 아는 것이 경로뿐이기 때문이다 —
 * 메뉴 조회 API 응답에는 메뉴 식별자가 없다. 서버가 경로로 메뉴를 찾아
 * 식별자로 저장한다(AuthServer 의 MenuFavorite 주석 참고).
 */
export async function getMenuFavorites() {
  return unwrap(await requestClient.get<any>('/auth/menu/favorites'));
}

/** 즐겨찾기에 담는다. 이미 담겨 있으면 서버가 그대로 두고 현재 목록을 준다. */
export async function addMenuFavorite(path: string) {
  return unwrap(
    await requestClient.post<any>('/auth/menu/favorites', { path }),
  );
}

/** 즐겨찾기에서 뺀다. 담겨 있지 않아도 오류가 아니다. */
export async function removeMenuFavorite(path: string) {
  return unwrap(
    await requestClient.delete<any>('/auth/menu/favorites', {
      params: { path },
    }),
  );
}

/**
 * 즐겨찾기 순서를 경로 목록의 순서대로 저장한다.
 * 고정탭 관리 화면의 드래그 정렬 결과를 통째로 보낸다.
 */
export async function reorderMenuFavorites(paths: string[]) {
  return unwrap(
    await requestClient.put<any>('/auth/menu/favorites/order', { paths }),
  );
}
