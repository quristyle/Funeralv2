<script setup lang="ts">
import BrandLogo from '#/components/brand-logo.vue';
import { useSite } from '#/composables/use-site';

const { t } = useSite();
const year = new Date().getFullYear();
</script>

<template>
  <footer class="bg-ink text-mist">
    <div class="mx-auto max-w-6xl px-6 py-14">
      <div class="flex flex-col gap-10 md:flex-row md:items-start md:justify-between">
        <div>
          <BrandLogo knockout :height="26" />
          <p class="mt-5 max-w-sm text-sm leading-relaxed text-steel">
            {{ t.hero.lead }}
          </p>
        </div>

        <div class="flex flex-col gap-3 text-sm">
          <span class="text-xs uppercase tracking-widest text-steel">Contact</span>
          <a :href="`mailto:${t.contact.email}`" class="transition-colors hover:text-paper">
            {{ t.contact.email }}
          </a>
          <!--
            관리 포털은 다른 오리진이다(admin.jsini.co.kr). 오리진을 나눠야
            공개 사이트와 업무 시스템의 토큰·쿠키 표면이 섞이지 않는다.
          -->
          <a
            href="https://admin.jsini.co.kr"
            rel="noopener"
            class="text-steel transition-colors hover:text-paper"
          >
            {{ t.footer.portal }} →
          </a>
        </div>
      </div>

      <div class="mt-12 border-t border-graphite pt-6 text-xs text-steel">
        © {{ year }} {{ t.footer.rights }}
      </div>
    </div>
  </footer>
</template>
