interface TreeConfigOptions {
  // 자식 속성의 이름, 기본값은 'children'
  childProps: string;
}

/**
 * @zh_CN 트리 구조를 순회하여 모든 노드에서 지정된 값을 반환합니다.
 * @param tree 트리 구조 배열
 * @param getValue 노드 값을 가져오는 함수
 * @param options 자식 노드 배열로 사용할 선택적 속성 이름.
 * @returns 모든 노드에서 지정된 값의 배열
 */
function traverseTreeValues<T, V>(
  tree: T[],
  getValue: (node: T) => V,
  options?: TreeConfigOptions,
): V[] {
  const result: V[] = [];
  const { childProps } = options || {
    childProps: 'children',
  };

  const dfs = (treeNode: T) => {
    const value = getValue(treeNode);
    result.push(value);
    const children = (treeNode as Record<string, any>)?.[childProps];
    if (!children) {
      return;
    }
    if (children.length > 0) {
      for (const child of children) {
        dfs(child);
      }
    }
  };

  for (const treeNode of tree) {
    dfs(treeNode);
  }
  return result.filter(Boolean);
}

/**
 * 조건에 따라 주어진 트리 구조의 노드를 필터링하고, 원래 순서대로 모든 일치하는 노드 배열을 반환합니다.
 * @param tree 필터링할 트리 구조의 루트 노드 배열.
 * @param filter 각 노드를 매칭하기 위한 조건.
 * @param options 자식 노드 배열로 사용할 선택적 속성 이름.
 * @returns 모든 일치하는 노드를 포함하는 배열.
 */
function filterTree<T extends Record<string, any>>(
  tree: T[],
  filter: (node: T) => boolean,
  options?: TreeConfigOptions,
): T[] {
  const { childProps } = options || {
    childProps: 'children',
  };

  const _filterTree = (nodes: T[]): T[] => {
    return nodes.filter((node: Record<string, any>) => {
      if (filter(node as T)) {
        if (node[childProps]) {
          node[childProps] = _filterTree(node[childProps]);
        }
        return true;
      }
      return false;
    });
  };

  return _filterTree(tree);
}

/**
 * 조건에 따라 주어진 트리 구조의 노드를 다시 매핑합니다.
 * @param tree 필터링할 트리 구조의 루트 노드 배열.
 * @param mapper 각 노드를 매칭하기 위한 조건.
 * @param options 자식 노드 배열로 사용할 선택적 속성 이름.
 */
function mapTree<T, V extends Record<string, any>>(
  tree: T[],
  mapper: (node: T, parent: null | V) => V,
  options?: TreeConfigOptions,
  parent: null | V = null,
): V[] {
  const { childProps } = options || {
    childProps: 'children',
  };
  return tree.map((node) => {
    const mapperNode: Record<string, any> = mapper(node, parent as null | V);
    if (mapperNode[childProps]) {
      mapperNode[childProps] = mapTree(
        mapperNode[childProps],
        mapper,
        options,
        mapperNode as V,
      );
    }
    return mapperNode as V;
  });
}

/**
 * 트리 구조 데이터를 재귀적으로 정렬
 * @param treeData - 트리 데이터 배열
 * @param sortFunction - 정렬 규칙을 정의하는 정렬 함수
 * @param options - 자식 노드 속성명을 포함한 설정 옵션
 * @returns 정렬된 트리 데이터
 */
function sortTree<T extends Record<string, any>>(
  treeData: T[],
  sortFunction: (a: T, b: T) => number,
  options?: TreeConfigOptions,
): T[] {
  const { childProps } = options || {
    childProps: 'children',
  };

  return treeData.toSorted(sortFunction).map((item) => {
    const children = item[childProps];
    if (children && Array.isArray(children) && children.length > 0) {
      return {
        ...item,
        [childProps]: sortTree(children, sortFunction, options),
      };
    }
    return item;
  });
}

export { filterTree, mapTree, sortTree, traverseTreeValues };
