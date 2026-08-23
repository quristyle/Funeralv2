<script lang="ts" setup>
import { computed, onMounted, ref } from 'vue';

import { Page } from '@vben/common-ui';

import {
  Alert,
  Avatar,
  Button,
  Card,
  Col,
  Descriptions,
  DescriptionsItem,
  Row,
  Spin,
  Tag,
} from 'ant-design-vue';

import { getMyInfo } from '#/api/helpdesk';
import { useJsiniUser } from '#/composables/use-jsini-user';
import { useHelpdeskStore } from '#/store/helpdesk';

import HelpdeskAccountNotice from '../shared/account-notice.vue';

/**
 * [내 프로필]
 *
 * 원본(JinReception pages/Profile.vue, `/profile`).
 * 원본은 헬프데스크 계정의 이름·이메일·사진을 고치고, 헬프데스크 자체 로그인
 * 비밀번호를 바꾸는 화면이었다.
 *
 * 계정을 JSini 포털로 단일화하면서 그 두 가지가 모두 여기 있을 이유가 없어졌다.
 *   - 이름·이메일·사진은 포털 계정의 것이 정본이다 → 개인 설정 화면에서 고친다.
 *   - 비밀번호는 포털(AuthServer)이 관리한다. 헬프데스크 자체 로그인은 꺼져 있다.
 *
 * 그래서 이 화면은 이제 **보여 주는 일만** 한다.
 *   1) 로그인한 JSini 계정
 *   2) 그 계정이 어떤 헬프데스크 사용자로 연결되어 있는지
 *
 * 헬프데스크 사용자 레코드 자체를 고쳐야 하면 조직 관리 › 담당자/고객 화면에서 한다.
 */

const helpdesk = useHelpdeskStore();
const {
  avatar,
  companyName,
  deptName,
  email,
  loginId,
  phone,
  roles,
  userName,
} = useJsiniUser();

const loading = ref(false);
/** 연결된 헬프데스크 계정 정보. 연결이 없으면 null 로 남는다. */
const linked = ref<any>(null);

/** 이름 첫 글자. 사진이 없을 때 아바타에 쓴다. */
const avatarInitial = computed(() =>
  (userName.value || loginId.value || '?').charAt(0).toUpperCase(),
);

async function load() {
  loading.value = true;
  try {
    linked.value = await getMyInfo();
  } catch {
    linked.value = null;
  } finally {
    loading.value = false;
  }
}

onMounted(async () => {
  await helpdesk.loadIdentity();
  if (helpdesk.helpdeskUserId) await load();
});
</script>

<template>
  <Page auto-content-height>
    <HelpdeskAccountNotice />

    <Spin :spinning="loading">
      <Row :gutter="[12, 12]">
        <!-- JSini 계정 -->
        <Col :lg="14" :xs="24">
          <Card size="small" title="JSini 계정">
            <div class="flex flex-col gap-4 md:flex-row">
              <div class="flex flex-col items-center gap-2">
                <Avatar v-if="avatar" :size="96" :src="avatar" />
                <Avatar v-else :size="96">{{ avatarInitial }}</Avatar>
              </div>

              <Descriptions
                :column="1"
                class="min-w-0 flex-1"
                size="small"
              >
                <DescriptionsItem label="로그인 아이디">
                  {{ loginId || '-' }}
                </DescriptionsItem>
                <DescriptionsItem label="이름">
                  {{ userName || '-' }}
                </DescriptionsItem>
                <DescriptionsItem label="이메일">
                  {{ email || '-' }}
                </DescriptionsItem>
                <DescriptionsItem label="연락처">
                  {{ phone || '-' }}
                </DescriptionsItem>
                <DescriptionsItem label="소속">
                  {{
                    [companyName, deptName]
                      .filter(Boolean)
                      .join(' · ') || '-'
                  }}
                </DescriptionsItem>
                <DescriptionsItem label="역할">
                  <template v-if="roles.length > 0">
                    <Tag v-for="role in roles" :key="role">
                      {{ role }}
                    </Tag>
                  </template>
                  <span v-else class="text-muted-foreground">배정된 역할 없음</span>
                </DescriptionsItem>
              </Descriptions>
            </div>

            <template #extra>
              <Button size="small" @click="$router.push('/profile')">
                개인 설정에서 수정
              </Button>
            </template>
          </Card>
        </Col>

        <!-- 헬프데스크 연결 -->
        <Col :lg="10" :xs="24">
          <Card size="small" title="헬프데스크 연결">
            <Alert
              class="mb-3"
              description="이름·이메일·비밀번호는 JSini 포털 계정이 정본입니다. 헬프데스크 계정은 기존 요청·댓글 데이터를 가리키기 위한 내부 연결로만 씁니다."
              message="무엇을 보여 주나요?"
              show-icon
              type="info"
            />

            <Descriptions :column="1" size="small">
              <DescriptionsItem label="헬프데스크 계정">
                {{ linked?.loginId ?? '-' }}
              </DescriptionsItem>
              <DescriptionsItem label="구분">
                <Tag v-if="helpdesk.identity" :color="helpdesk.isAdmin ? 'blue' : 'green'">
                  {{ helpdesk.isAdmin ? '담당자' : '고객' }}
                </Tag>
                <span v-else class="text-muted-foreground">연결 없음</span>
              </DescriptionsItem>
              <DescriptionsItem label="내부 ID">
                {{ helpdesk.helpdeskUserId ?? '-' }}
              </DescriptionsItem>
              <DescriptionsItem label="헬프데스크 이름">
                {{ linked?.helpdeskUserName ?? '-' }}
              </DescriptionsItem>
              <DescriptionsItem label="소속 회사">
                {{ linked?.companyName ?? linked?.teamName ?? '-' }}
              </DescriptionsItem>
            </Descriptions>
          </Card>
        </Col>
      </Row>
    </Spin>
  </Page>
</template>
