<script lang="ts" setup>
import { ref, watch } from 'vue';
import { Page, useVbenDrawer, ImageGroupManager } from '@vben/common-ui';
import { IconifyIcon } from '@vben/icons';
import { Button, message, Popconfirm, Form, Input, Tooltip } from 'ant-design-vue';
import { useVbenVxeGrid } from '#/adapter/vxe-table';
import GridIconButton from '#/components/GridIconButton.vue';
import { getBuildings, createBuilding, updateBuilding, deleteBuilding } from '#/api/funeral/building';
import BizSelect from '#/components/BizSelect.vue';
import ImagePreview from '#/components/ImagePreview.vue';

const filterCompanyId = ref<string>('');

const [BuildingDrawer, buildingDrawerApi] = useVbenDrawer({
  title: '건물 정보 설정',
  destroyOnClose: true,
  onConfirm: async () => {
    await handleSave();
  }
});

const formModel = ref({
  id: '',
  companyId: '',
  name: '',
  shortName: '',
  abbreviation: '',
  address: '',
  zipCode: '',
  addressDetail: '',
  remark: '',
  buildingPhotoGroupId: '',
  parkingPhotoGroupId: ''
});

const [Grid, gridApi] = useVbenVxeGrid({
  gridOptions: {
    columns: [
      { field: 'name', title: '건물명', minWidth: 150 },
      { field: 'shortName', title: '짧은명칭', minWidth: 120 },
      { field: 'abbreviation', title: '약어', minWidth: 100 },
      { field: 'address', title: '주소', minWidth: 250 },
      /**
       * 사진 칸 둘. 한 칸에 여러 장이 가로로 늘어서고 넘치면 그 안에서 스크롤한다.
       *
       * `jsini-fillcell` 로 칸 여백을 걷어 사진이 줄 높이를 꽉 채우게 한다
       * (`styles/index.css`). 다른 리소스 화면들과 같은 크기·모양이다.
       */
      { field: 'buildingPhotos', title: '건물전경사진', minWidth: 160, className: 'jsini-fillcell', slots: { default: 'buildingPhotos' } },
      { field: 'parkingPhotos', title: '주차장이미지', minWidth: 160, className: 'jsini-fillcell', slots: { default: 'parkingPhotos' } },
      {
        field: 'action',
        title: '작업',
        width: 150,
        fixed: 'right',
        slots: { default: 'action' }
      }
    ],
    // 아래 도구줄의 [추가] — 위쪽 아이콘과 같은 함수를 부른다.
    // (`gridFeatures` 는 vxe 타입에 없다. 공통 레이어가 읽고 떼어 낸다.)
    gridFeatures: { onCreate: () => onCreate() },
    height: 'auto',
    /**
     * 사진을 칸에 꽉 채우므로 줄 높이가 곧 사진 높이다.
     *
     * 전역이 `showOverflow: true` 라 vxe 가 줄 높이를 고정한다 — 여기서 정하지
     * 않으면 기본 높이(작은 크기)에 맞춰 사진이 납작해진다.
     * 다른 리소스 화면들과 같은 값으로 맞췄다.
     */
    rowConfig: { height: 64 },
    proxyConfig: {
      ajax: {
        query: async () => {
          return await getBuildings(filterCompanyId.value);
        },
      },
    },
  } as any,
});

watch(filterCompanyId, () => {
  gridApi.query();
});

function onCreate() {
  formModel.value = {
    id: '',
    companyId: filterCompanyId.value,
    name: '',
    shortName: '',
    abbreviation: '',
    address: '',
    zipCode: '',
    addressDetail: '',
    remark: '',
    buildingPhotoGroupId: '',
    parkingPhotoGroupId: ''
  };
  buildingDrawerApi.open();
}

function onEdit(row: any) {
  formModel.value = {
    ...row,
    buildingPhotoGroupId: row.buildingPhotoGroupId || '',
    parkingPhotoGroupId: row.parkingPhotoGroupId || ''
  };
  buildingDrawerApi.open();
}

async function onDelete(row: any) {
  try {
    await deleteBuilding(row.id);
    message.success('건물 정보가 삭제되었습니다.');
    gridApi.query();
  } catch (error) {
    message.error('삭제 실패');
  }
}

async function handleSave() {
  try {
    if (formModel.value.id) {
      await updateBuilding(formModel.value.id, formModel.value);
      message.success('건물 정보가 수정되었습니다.');
    } else {
      await createBuilding(formModel.value);
      message.success('건물 정보가 등록되었습니다.');
    }
    buildingDrawerApi.close();
    gridApi.query();
  } catch (error) {
    message.error('저장 실패');
  }
}
</script>

<template>
  <Page auto-content-height content-class="page-fill-last">
    <!-- `jsini-searchbar` — 조회부 라벨이 세로로 눕지 않게 한다 (styles/index.css) -->
    <div class="jsini-searchbar mb-4 flex items-center justify-between bg-card p-4 rounded-lg shadow-sm border border-border">
      <div class="flex items-center gap-4">
        <div class="flex items-center gap-2">
          <span class="font-semibold text-sm">회사</span>
          <BizSelect
            v-model:value="filterCompanyId"
            type="funeralCompany"
            auto-select-first
            placeholder="회사 선택"
            class="w-64"
            show-search
            option-filter-prop="label"
          />
        </div>
      </div>

    </div>

    <Grid >
      <!--
        사진은 여러 장이라 가로로 늘어놓고 넘치면 그 안에서 스크롤한다.
        각 장은 줄 높이를 꽉 채우고(`height="100%"`), `fit="cover"` 라
        비율은 그대로면서 넘치는 가장자리만 잘린다.
        `leading-normal` — 칸의 `line-height: 0` 을 '미등록' 글자에는 되돌린다.
      -->
      <template #buildingPhotos="{ row }">
        <div class="flex h-full items-stretch gap-px overflow-x-auto">
          <template v-if="row.buildingPhotos && row.buildingPhotos.length">
            <ImagePreview
              v-for="(url, idx) in row.buildingPhotos"
              :key="idx"
              :src="url"
              :preview-src="url.replace('/thumbnail/', '/download/')"
              :width="86"
              height="100%"
              fit="cover"
              frameless
              class="shrink-0"
            />
          </template>
          <span v-else class="text-xs text-muted-foreground leading-normal self-center px-2">
            미등록
          </span>
        </div>
      </template>

      <template #parkingPhotos="{ row }">
        <div class="flex h-full items-stretch gap-px overflow-x-auto">
          <template v-if="row.parkingPhotos && row.parkingPhotos.length">
            <ImagePreview
              v-for="(url, idx) in row.parkingPhotos"
              :key="idx"
              :src="url"
              :preview-src="url.replace('/thumbnail/', '/download/')"
              :width="86"
              height="100%"
              fit="cover"
              frameless
              class="shrink-0"
            />
          </template>
          <span v-else class="text-xs text-muted-foreground leading-normal self-center px-2">
            미등록
          </span>
        </div>
      </template>

      <template #action="{ row }">
        <div class="flex gap-2 justify-center">
          <Tooltip v-perm:update title="수정">
            <Button type="link" size="small" @click="onEdit(row)">
              <IconifyIcon icon="lucide:edit" class="size-4" />
            </Button>
          </Tooltip>
          <Popconfirm title="해당 건물을 삭제하시겠습니까?" @confirm="onDelete(row)" placement="topLeft">
            <Tooltip v-perm:delete title="삭제">
              <Button type="link" size="small" danger>
                <IconifyIcon icon="lucide:trash-2" class="size-4" />
              </Button>
            </Tooltip>
          </Popconfirm>
        </div>
      </template>
    </Grid>

    <BuildingDrawer>
      <div class="p-2">
        <Form layout="vertical">
          <Form.Item label="소속 회사" required>
            <BizSelect
              type="funeralCompany"
              v-model:value="formModel.companyId"
              placeholder="회사를 선택해주세요"
            />
          </Form.Item>
          <Form.Item label="건물명" required>
            <Input v-model:value="formModel.name" placeholder="예: 본관, 신관, 장례식장 A동" />
          </Form.Item>
          <div class="grid grid-cols-2 gap-x-4">
            <Form.Item label="짧은 명칭">
              <Input v-model:value="formModel.shortName" placeholder="예: 본관" />
            </Form.Item>
            <Form.Item label="약어 (3자리 영문)">
              <Input v-model:value="formModel.abbreviation" placeholder="예: MAN" :maxlength="3" />
            </Form.Item>
          </div>
          <Form.Item label="주소">
            <Input v-model:value="formModel.address" placeholder="주소 입력" />
          </Form.Item>
          <Form.Item label="비고/설명">
            <Input.TextArea v-model:value="formModel.remark" placeholder="특이 사항 입력" />
          </Form.Item>

          <div class="grid grid-cols-2 gap-4 mt-6">
            <div class="p-4 rounded border border-border bg-card/10">
              <h3 class="text-sm font-semibold mb-1 text-foreground">
                건물 전경 사진 등록 (다중)
              </h3>
              <p class="text-xs text-muted-foreground mb-4">
                * 종합 현판, DID에 노출되는 전경 이미지 그룹입니다.
              </p>
              <ImageGroupManager
                v-model="formModel.buildingPhotoGroupId"
                :limit="10"
                biz-type="funeralv2/building"
              />
            </div>
            
            <div class="p-4 rounded border border-border bg-card/10">
              <h3 class="text-sm font-semibold mb-1 text-foreground">
                주차장 안내 이미지 등록 (다중)
              </h3>
              <p class="text-xs text-muted-foreground mb-4">
                * 종합 안내 키오스크의 주차안내 화면에 슬라이드로 표시됩니다.
              </p>
              <ImageGroupManager
                v-model="formModel.parkingPhotoGroupId"
                :limit="10"
                biz-type="funeralv2/building"
              />
            </div>
          </div>
        </Form>
      </div>
    </BuildingDrawer>
  </Page>
</template>