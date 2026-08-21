<script lang="ts" setup>
import type { UploadFile } from 'ant-design-vue';

import { h, ref, toRaw } from 'vue';

import { Page } from '@vben/common-ui';

import { useDebounceFn } from '@vueuse/core';
import { Button, Card, message, Spin, Tag } from 'ant-design-vue';
import dayjs from 'dayjs';

import { useVbenForm, z } from '#/adapter/form';
import { getAllMenusApi } from '#/api';
import { upload_file } from '#/api/examples/upload';
import { $t } from '#/locales';

import DocButton from '../doc-button.vue';

const keyword = ref('');
const fetching = ref(false);
// 원격 데이터 가져오기 시뮬레이션
function fetchRemoteOptions({ keyword = '옵션' }: Record<string, any>) {
  fetching.value = true;
  return new Promise((resolve) => {
    setTimeout(() => {
      const options = Array.from({ length: 10 }).map((_, index) => ({
        label: `${keyword}-${index}`,
        value: `${keyword}-${index}`,
      }));
      resolve(options);
      fetching.value = false;
    }, 1000);
  });
}

const [BaseForm, baseFormApi] = useVbenForm({
  // 모든 폼 항목에서 공유되며, 개별 항목에서 재정의 가능
  commonConfig: {
    // label 뒤에 콜론 표시
    colon: true,
    // 모든 폼 항목
    componentProps: {
      class: 'w-full',
    },
  },
  fieldMappingTime: [['rangePicker', ['startTime', 'endTime'], 'YYYY-MM-DD']],
  // 제출 함수
  handleSubmit: onSubmit,
  handleValuesChange(_values, fieldsChanged) {
    message.info(`폼의 다음 필드가 변경되었습니다: ${fieldsChanged.join(', ')}`);
  },

  // 수직 레이아웃, label과 input이 다른 줄에 위치, 값은 vertical
  // 수평 레이아웃, label과 input이 같은 줄에 위치
  layout: 'horizontal',
  schema: [
    {
      // 컴포넌트는 #/adapter.ts 내에 등록되어야 하며 타입을 포함해야 함
      component: 'Input',
      // 컴포넌트 파라미터
      componentProps: {
        placeholder: '사용자 이름을 입력하세요',
      },
      // 필드명
      fieldName: 'username',
      // 화면에 표시될 label
      label: '문자열',
      rules: 'required',
    },
    {
      component: 'Input',
      fieldName: 'desc',
      // 화면에 표시될 description
      description: '폼 설명입니다',
      label: '문자열(설명 포함)',
    },
    {
      // 컴포넌트는 #/adapter.ts 내에 등록되어야 하며 타입을 포함해야 함
      component: 'ApiSelect',
      // 컴포넌트 파라미터
      componentProps: {
        // 메뉴 인터페이스를 options 형식으로 변환
        afterFetch: (data: { name: string; path: string }[]) => {
          return data.map((item: any) => ({
            label: item.name,
            value: item.path,
          }));
        },
        // 메뉴 인터페이스
        api: getAllMenusApi,
        autoSelect: 'first',
      },
      // 필드명
      fieldName: 'api',
      // 화면에 표시될 label
      label: 'ApiSelect',
    },
    {
      component: 'ApiSelect',
      // 컴포넌트 파라미터
      componentProps: () => {
        return {
          api: fetchRemoteOptions,
          // 로컬 필터링 비활성화
          filterOption: false,
          // 데이터를 가져오는 중이면 슬롯을 사용하여 로딩 표시
          notFoundContent: fetching.value ? undefined : null,
          // 검색어 변경 시 기록, useDebounceFn으로 디바운스 처리
          onSearch: useDebounceFn((value: string) => {
            keyword.value = value;
          }, 300),
          // 원격 검색 파라미터. 검색어 변경 시 params도 업데이트됨
          params: {
            keyword: keyword.value || undefined,
          },
          // 원격 검색 판단. true일 때만 API 호출 허용
          shouldFetch: (params: any) => {
            return !!params?.keyword;
          },
          showSearch: true,
        };
      },
      // 필드명
      fieldName: 'remoteSearch',
      // 화면에 표시될 label
      label: '원격 검색',
      help: '원격 조회, 입력 시에만 조회를 수행합니다',
      renderComponentContent: () => {
        return {
          notFoundContent: fetching.value ? h(Spin) : undefined,
        };
      },
      rules: 'selectRequired',
    },
    {
      component: 'ApiTreeSelect',
      // 컴포넌트 파라미터
      componentProps: {
        // 메뉴 인터페이스
        api: getAllMenusApi,
        // 메뉴 인터페이스를 options 형식으로 변환
        labelField: 'name',
        valueField: 'path',
        childrenField: 'children',
      },
      // 필드명
      fieldName: 'apiTree',
      // 화면에 표시될 label
      label: 'ApiTreeSelect',
    },
    {
      component: 'InputPassword',
      componentProps: {
        placeholder: '비밀번호를 입력하세요',
      },
      fieldName: 'password',
      label: '비밀번호',
    },
    {
      component: 'InputNumber',
      componentProps: {
        placeholder: '입력하세요',
      },
      fieldName: 'number',
      label: '숫자(접미사 포함)',
      suffix: () => '¥',
    },
    {
      component: 'IconPicker',
      fieldName: 'icon',
      label: '아이콘',
    },
    {
      colon: false,
      component: 'Select',
      componentProps: {
        allowClear: true,
        filterOption: true,
        options: [
          {
            label: '옵션1',
            value: '1',
          },
          {
            label: '옵션2',
            value: '2',
          },
        ],
        placeholder: '선택하세요',
        showSearch: true,
      },
      fieldName: 'options',
      label: () => h(Tag, { color: 'warning' }, () => '😎사용자 정의:'),
    },
    {
      component: 'RadioGroup',
      componentProps: {
        options: [
          {
            label: '옵션1',
            value: '1',
          },
          {
            label: '옵션2',
            value: '2',
          },
        ],
      },
      fieldName: 'radioGroup',
      label: '라디오 그룹',
    },
    {
      component: 'Radio',
      fieldName: 'radio',
      label: '',
      renderComponentContent: () => {
        return {
          default: () => ['Radio'],
        };
      },
    },
    {
      component: 'CheckboxGroup',
      componentProps: {
        name: 'cname',
        options: [
          {
            label: '옵션1',
            value: '1',
          },
          {
            label: '옵션2',
            value: '2',
          },
        ],
      },
      fieldName: 'checkboxGroup',
      label: '체크박스 그룹',
    },
    {
      component: 'Checkbox',
      fieldName: 'checkbox',
      label: '',
      renderComponentContent: () => {
        return {
          default: () => ['읽었으며 동의합니다'],
        };
      },
      rules: z
        .boolean()
        .refine((v) => v, { message: '왜 동의하지 않으시나요? 체크해 주세요!' }),
    },
    {
      component: 'Mentions',
      componentProps: {
        options: [
          {
            label: 'afc163',
            value: 'afc163',
          },
          {
            label: 'zombieJ',
            value: 'zombieJ',
          },
        ],
        placeholder: '입력하세요',
      },
      fieldName: 'mentions',
      label: '멘션',
    },
    {
      component: 'Rate',
      fieldName: 'rate',
      label: '평점',
    },
    {
      component: 'Switch',
      componentProps: {
        class: 'w-auto',
      },
      fieldName: 'switch',
      help: () =>
        ['이것은 여러 줄 도움말 정보입니다', '두 번째 줄', '세 번째 줄'].map((v) => h('p', v)),
      label: '스위치',
    },
    {
      component: 'DatePicker',
      fieldName: 'datePicker',
      help: (values) =>
        [`다른 필드 값을 출력할 수 있는 도움말 정보입니다: ${values?.rate}`].map((v) =>
          h('p', v),
        ),
      label: '날짜 선택',
    },
    {
      component: 'RangePicker',
      fieldName: 'rangePicker',
      label: '범위 선택',
    },
    {
      component: 'TimePicker',
      fieldName: 'timePicker',
      label: '시간 선택',
    },
    {
      component: 'TreeSelect',
      componentProps: {
        allowClear: true,
        placeholder: '선택하세요',
        showSearch: true,
        treeData: [
          {
            label: 'root 1',
            value: 'root 1',
            children: [
              {
                label: 'parent 1',
                value: 'parent 1',
                children: [
                  {
                    label: 'parent 1-0',
                    value: 'parent 1-0',
                    children: [
                      {
                        label: 'my leaf',
                        value: 'leaf1',
                      },
                      {
                        label: 'your leaf',
                        value: 'leaf2',
                      },
                    ],
                  },
                  {
                    label: 'parent 1-1',
                    value: 'parent 1-1',
                  },
                ],
              },
              {
                label: 'parent 2',
                value: 'parent 2',
              },
            ],
          },
        ],
        treeNodeFilterProp: 'label',
      },
      fieldName: 'treeSelect',
      label: '트리 선택',
    },
    {
      component: 'Upload',
      componentProps: {
        // 더 많은 속성은 https://ant.design/components/upload-cn 참고
        accept: '.png,.jpg,.jpeg',
        // 인증 정보 자동 포함
        customRequest: upload_file,
        disabled: false,
        maxCount: 3,
        // 단위: MB
        maxSize: 2,
        multiple: false,
        showUploadList: true,
        // 업로드 목록 스타일, 4가지 기본 스타일 지원: text, picture, picture-card, picture-circle
        listType: 'picture-card',
        draggable: true, // 드래그 앤 드롭 정렬 활성화
        // onChange 이벤트가 재정의되었습니다. 사용자 정의가 필요한 경우 이를 기반으로 확장하십시오.
        handleChange: ({ file }: { file: UploadFile }) => {
          const { name, status } = file;
          if (status === 'done') {
            message.success(`${name} ${$t('examples.form.upload-success')}`);
          } else if (status === 'error') {
            message.error(`${name} ${$t('examples.form.upload-fail')}`);
          }
        },
        onDragSort: (oldIndex: number, newIndex: number) => {
          console.warn(`이미지가 ${oldIndex}에서 ${newIndex}로 이동되었습니다`);
        },
      },
      fieldName: 'files',
      label: $t('examples.form.file'),
      renderComponentContent: () => {
        return {
          default: () => $t('examples.form.upload-image'),
        };
      },
      rules: 'selectRequired',
    },
    {
      component: 'Upload',
      componentProps: {
        accept: '.png,.jpg,.jpeg',
        customRequest: upload_file,
        maxCount: 1,
        maxSize: 2,
        listType: 'picture-card',
        // 이미지 자르기 활성화 여부(다중 선택 또는 이미지가 아닌 경우 자르기 상자가 나타나지 않음)
        crop: true,
        // 자르기 비율
        aspectRatio: '1:1',
      },
      fieldName: 'cropImage',
      label: $t('examples.form.crop-image'),
      renderComponentContent: () => {
        return {
          default: () => $t('examples.form.upload-image'),
        };
      },
      rules: 'selectRequired',
    },
  ],
  // 대화면 한 줄에 3개, 중화면 2개, 소화면 1개 표시
  wrapperClass: 'grid-cols-1 md:grid-cols-2 lg:grid-cols-3',
});

function onSubmit(values: Record<string, any>) {
  const files = toRaw(values.files) as UploadFile[];
  const cropImage = (toRaw(values.cropImage) ?? []) as UploadFile[];
  const doneFiles = files.filter((file) => file.status === 'done');
  const failedFiles = files.filter((file) => file.status !== 'done');
  const doneCrop = cropImage.filter((file) => file.status === 'done');
  const failedCrop = cropImage.filter((file) => file.status !== 'done');

  const msg = [
    ...doneFiles.map((file) => file.response?.url || file.url),
    ...failedFiles.map((file) => file.name),
  ].join(', ');
  const msgCrop = [
    ...doneCrop.map((file) => file.response?.url || file.url),
    ...failedCrop.map((file) => file.name),
  ].join(', ');

  if (failedFiles.length === 0) {
    message.success({
      content: `${$t('examples.form.upload-urls')}: ${msg}`,
    });
  } else {
    message.error({
      content: `${$t('examples.form.upload-error')}: ${msg}`,
    });
    return;
  }
  if (doneCrop.length > 0 && failedCrop.length === 0) {
    message.success({
      content: `${$t('examples.form.upload-urls')}: ${msgCrop}`,
    });
  } else if (failedCrop.length > 0) {
    message.error({
      content: `${$t('examples.form.upload-error')}: ${msgCrop}`,
    });
    return;
  }
  // 필요한 경우 제출 전 URL로 교체 가능
  values.files = doneFiles.map((file) => file.response?.url || file.url);
  values.cropImage = doneCrop.map((file) => file.response?.url || file.url);
  message.success({
    content: `form values: ${JSON.stringify(values)}`,
  });
}

function handleSetFormValue() {
  /**
   * 폼 값 설정(다중)
   */
  baseFormApi.setValues({
    checkboxGroup: ['1'],
    datePicker: dayjs('2022-01-01'),
    files: [
      {
        name: 'example.png',
        status: 'done',
        uid: '-1',
        url: 'https://unpkg.com/@vbenjs/static-source@0.1.7/source/logo-v1.webp',
      },
    ],
    mentions: '@afc163',
    number: 3,
    options: '1',
    password: '2',
    radioGroup: '1',
    rangePicker: [dayjs('2022-01-01'), dayjs('2022-01-02')],
    rate: 3,
    switch: true,
    timePicker: dayjs('2022-01-01 12:00:00'),
    treeSelect: 'leaf1',
    username: '1',
  });

  // 개별 폼 값 설정
  baseFormApi.setFieldValue('checkbox', true);
}
</script>

<template>
  <Page
    content-class="flex flex-col gap-4"
    description="폼 컴포넌트 기본 예제입니다. 이 페이지에서 사용된 파라미터 코드에는 이해를 돕기 위한 간단한 주석이 추가되어 있으니 자세히 확인해 주시기 바랍니다."
    title="폼 컴포넌트"
  >
    <template #description>
      <div class="text-muted-foreground">
        <p>
          폼 컴포넌트 기본 예제입니다. 이 페이지에서 사용된 파라미터 코드에는 이해를 돕기 위한 간단한 주석이 추가되어 있으니 자세히 확인해 주시기 바랍니다.
        </p>
      </div>
    </template>
    <template #extra>
      <DocButton class="mb-2" path="/components/common-ui/vben-form" />
    </template>
    <Card title="기본 예제">
      <template #extra>
        <Button type="primary" @click="handleSetFormValue">폼 값 설정</Button>
      </template>
      <BaseForm />
    </Card>
  </Page>
</template>

