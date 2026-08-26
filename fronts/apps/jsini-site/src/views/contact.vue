<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';

import { siteApi, type InquiryRequest, type Section } from '#/api/site';
import RichText from '#/components/rich-text.vue';
import { useSite } from '#/composables/use-site';

const { locale, t } = useSite();

/**
 * 동의 문구는 DB(`site.sections` 의 `contact.consent`)에서 온다.
 * 법률 문구라 코드 배포 없이 고칠 수 있어야 하기 때문이다.
 * 블록이 없으면 아래 fallback 을 쓴다 — 동의 문구가 아예 안 보이는 것이 더 나쁘다.
 */
const consent = ref<Section | null>(null);

const consentBody = computed(
  () =>
    consent.value?.body ??
    (locale.value === 'en'
      ? 'We collect your name, email, and message to answer your enquiry, and keep them for three years.'
      : '문의 답변을 위해 이름 · 이메일 · 문의 내용을 수집하며, 접수일로부터 3년간 보관합니다.'),
);

const form = reactive({
  name: '',
  company: '',
  email: '',
  phone: '',
  category: '',
  subject: '',
  message: '',
  consent: false,
  // 허니팟. 사람은 이 칸을 보지 못한다. 채워져 있으면 서버가 조용히 버린다.
  website: '',
});

type State = 'done' | 'idle' | 'sending';
const state = ref<State>('idle');
const error = ref('');

const canSubmit = computed(
  () => !!form.name.trim() && !!form.email.trim() && !!form.message.trim() && form.consent,
);

async function submit() {
  error.value = '';

  if (!canSubmit.value) {
    error.value = t.value.contact.form.required;
    return;
  }

  state.value = 'sending';

  const body: InquiryRequest = {
    name: form.name.trim(),
    company: form.company.trim() || undefined,
    email: form.email.trim(),
    phone: form.phone.trim() || undefined,
    category: form.category.trim() || undefined,
    subject: form.subject.trim() || form.message.trim().slice(0, 40),
    message: form.message.trim(),
    locale: locale.value,
    consent: form.consent,
    website: form.website,
  };

  const res = await siteApi.submitInquiry(body);

  if (res.ok) {
    state.value = 'done';
    return;
  }

  state.value = 'idle';
  error.value = res.rateLimited ? t.value.contact.form.rateLimited : t.value.contact.form.failed;
}

onMounted(async () => {
  const rows = await siteApi.sections(locale.value, 'contact.');
  consent.value = rows.find((r) => r.sectionKey === 'contact.consent') ?? null;
  siteApi.recordVisit(`/${locale.value}/contact`, locale.value);
});
</script>

<template>
  <div>
    <section class="border-b border-mist">
      <div class="mx-auto max-w-6xl px-6 py-20">
        <h1 class="h-display text-4xl">{{ t.contact.title }}</h1>
        <p class="mt-4 text-sm text-steel">{{ t.contact.lead }}</p>
      </div>
    </section>

    <section class="mx-auto max-w-2xl px-6 py-20">
      <!-- 접수 완료. 폼을 치우고 결과만 남긴다. 두 번 보내는 것을 막는다. -->
      <div v-if="state === 'done'" class="border border-ink p-8">
        <div class="shard-rule mb-6 w-10 bg-ink" />
        <p class="text-sm leading-relaxed">{{ t.contact.form.done }}</p>
        <a
          :href="`mailto:${t.contact.email}`"
          class="mt-8 inline-block text-sm text-steel hover:text-ink"
        >
          {{ t.contact.email }}
        </a>
      </div>

      <form v-else class="flex flex-col gap-6" novalidate @submit.prevent="submit">
        <div class="grid gap-6 sm:grid-cols-2">
          <label class="flex flex-col gap-2">
            <span class="text-xs uppercase tracking-widest text-steel">
              {{ t.contact.form.name }}
            </span>
            <input
              v-model="form.name"
              type="text"
              required
              autocomplete="name"
              class="border border-mist px-4 py-3 text-sm outline-none focus:border-ink"
            />
          </label>

          <label class="flex flex-col gap-2">
            <span class="text-xs uppercase tracking-widest text-steel">
              {{ t.contact.form.company }}
              <span class="normal-case tracking-normal text-mist">
                ({{ t.contact.form.optional }})
              </span>
            </span>
            <input
              v-model="form.company"
              type="text"
              autocomplete="organization"
              class="border border-mist px-4 py-3 text-sm outline-none focus:border-ink"
            />
          </label>

          <label class="flex flex-col gap-2">
            <span class="text-xs uppercase tracking-widest text-steel">
              {{ t.contact.form.emailField }}
            </span>
            <input
              v-model="form.email"
              type="email"
              required
              autocomplete="email"
              class="border border-mist px-4 py-3 text-sm outline-none focus:border-ink"
            />
          </label>

          <label class="flex flex-col gap-2">
            <span class="text-xs uppercase tracking-widest text-steel">
              {{ t.contact.form.phone }}
              <span class="normal-case tracking-normal text-mist">
                ({{ t.contact.form.optional }})
              </span>
            </span>
            <input
              v-model="form.phone"
              type="tel"
              autocomplete="tel"
              class="border border-mist px-4 py-3 text-sm outline-none focus:border-ink"
            />
          </label>
        </div>

        <label class="flex flex-col gap-2">
          <span class="text-xs uppercase tracking-widest text-steel">
            {{ t.contact.form.subject }}
          </span>
          <input
            v-model="form.subject"
            type="text"
            class="border border-mist px-4 py-3 text-sm outline-none focus:border-ink"
          />
        </label>

        <label class="flex flex-col gap-2">
          <span class="text-xs uppercase tracking-widest text-steel">
            {{ t.contact.form.message }}
          </span>
          <textarea
            v-model="form.message"
            required
            rows="7"
            class="resize-y border border-mist px-4 py-3 text-sm outline-none focus:border-ink"
          />
        </label>

        <!--
          허니팟.

          `display: none` 을 쓰지 않는다 — 요즘 봇은 그것을 보고 건너뛴다.
          화면 밖으로 밀어 두고 `aria-hidden` · `tabindex="-1"` 로 사람과 보조기기의
          동선에서 뺀다. 자동완성도 끈다(브라우저가 채우면 사람이 걸린다).
        -->
        <div class="absolute -left-[9999px] top-0" aria-hidden="true">
          <label>
            Website
            <input v-model="form.website" type="text" tabindex="-1" autocomplete="off" />
          </label>
        </div>

        <!-- 개인정보 동의. 문구는 DB 에서 오고, 체크하지 않으면 보낼 수 없다. -->
        <div class="border border-mist p-6">
          <p class="text-xs uppercase tracking-widest text-steel">
            {{ t.contact.form.consentTitle }}
          </p>
          <RichText
            :text="consentBody"
            gap="mt-3"
            class="mt-4 text-xs leading-relaxed text-graphite"
          />
          <label class="mt-6 flex items-start gap-3 text-sm">
            <input
              v-model="form.consent"
              type="checkbox"
              required
              class="mt-0.5 size-4 shrink-0 accent-ink"
            />
            <span>{{ t.contact.form.consent }}</span>
          </label>
        </div>

        <p v-if="error" class="text-sm text-graphite">{{ error }}</p>

        <div>
          <button
            type="submit"
            :disabled="state === 'sending'"
            class="border border-ink px-8 py-3 text-sm transition-colors hover:bg-ink hover:text-paper disabled:cursor-not-allowed disabled:border-mist disabled:text-mist disabled:hover:bg-transparent"
          >
            {{ state === 'sending' ? t.contact.form.sending : t.contact.form.submit }}
          </button>
        </div>
      </form>
    </section>
  </div>
</template>
