import type { LifeWeatherApi } from '#/api/life/weather';

import dayjs, { type Dayjs } from 'dayjs';

/**
 * [기상 화면 공용 유틸]
 *
 * 원본(ghubfront)의 SingleWeatherWidget · WeatherForecast · WeatherWarning 에
 * 흩어져 있던 아이콘 매핑 · 시각 변환을 한곳에 모았다.
 * 아이콘은 lucide-vue-next 대신 IconifyIcon('lucide:...') 이름을 돌려준다.
 */

/** 강수형태(PTY) 코드 → 한글 명칭 */
export function getPtyName(pty?: null | number | string): string {
  if (pty === undefined || pty === null || pty === '') return '-';
  const code = Number(pty);
  if (Number.isNaN(code)) return '-';
  const map: Record<number, string> = {
    0: '없음',
    1: '비',
    2: '비/눈',
    3: '눈',
    5: '빗방울',
    6: '빗방울/눈날림',
    7: '눈날림',
  };
  return map[code] ?? '-';
}

/** 관측 시각(UTC ISO) 기준 야간 여부 */
function isNightTime(iso: string): boolean {
  const hour = dayjs(iso).hour();
  return hour < 6 || hour >= 19;
}

/** 실황(condition 텍스트) → 아이콘 이름 */
export function conditionIconOf(w: LifeWeatherApi.Info): string {
  const cond = (w.condition ?? '').toLowerCase();
  const night = isNightTime(w.observationTime);
  if (cond.includes('천둥') || cond.includes('thunder')) return 'lucide:zap';
  if (cond.includes('눈') || cond.includes('snow')) return 'lucide:snowflake';
  if (cond.includes('비') || cond.includes('rain')) {
    return (w.rainfall ?? 0) > 5 ? 'lucide:cloud-rain' : 'lucide:cloud-drizzle';
  }
  if (cond.includes('안개') || cond.includes('fog') || cond.includes('mist')) {
    return 'lucide:haze';
  }
  if (
    cond.includes('구름') ||
    cond.includes('cloud') ||
    cond.includes('흐림') ||
    cond.includes('overcast')
  ) {
    return night ? 'lucide:cloud-moon' : 'lucide:cloud-sun';
  }
  if (cond.includes('맑음') || cond.includes('clear') || cond.includes('sun')) {
    return night ? 'lucide:moon' : 'lucide:sun';
  }
  return 'lucide:cloud';
}

/**
 * 실황 카드 배경 테마 (원본 getCardTheme 이식).
 * 날씨·기온·주야에 따라 그라디언트를 고른다. 글자색까지 포함한다.
 */
export function cardThemeOf(w: LifeWeatherApi.Info): string {
  const cond = (w.condition ?? '').toLowerCase();
  const temp = w.temperatureC;
  const night = isNightTime(w.observationTime);
  const base = 'shadow-lg ';
  const white = 'text-white ';
  if (cond.includes('천둥') || cond.includes('thunder')) {
    return `${base}${white}bg-gradient-to-br from-slate-900 via-purple-900 to-indigo-900`;
  }
  if (cond.includes('눈') || cond.includes('snow')) {
    return `${base}text-slate-700 bg-gradient-to-br from-slate-50 via-blue-100 to-indigo-200`;
  }
  if (cond.includes('비') || cond.includes('rain')) {
    return (w.rainfall ?? 0) > 10
      ? `${base}${white}bg-gradient-to-br from-slate-800 via-slate-700 to-blue-900`
      : `${base}${white}bg-gradient-to-br from-blue-600 via-slate-500 to-slate-400`;
  }
  if (cond.includes('안개') || cond.includes('fog') || cond.includes('mist')) {
    return `${base}text-slate-800 bg-gradient-to-br from-slate-200 via-gray-300 to-slate-400`;
  }
  if (night) {
    return cond.includes('구름') || cond.includes('cloud') || cond.includes('흐림')
      ? `${base}${white}bg-gradient-to-br from-slate-800 via-slate-700 to-slate-600`
      : `${base}${white}bg-gradient-to-br from-slate-900 via-blue-950 to-slate-800`;
  }
  if (temp >= 33) {
    return `${base}${white}bg-gradient-to-br from-red-500 via-orange-500 to-amber-500`;
  }
  if (temp >= 28) {
    return `${base}${white}bg-gradient-to-br from-orange-400 via-amber-400 to-yellow-300`;
  }
  if (temp <= -10) {
    return `${base}${white}bg-gradient-to-br from-indigo-600 via-blue-700 to-cyan-600`;
  }
  if (cond.includes('구름') || cond.includes('cloud') || cond.includes('흐림')) {
    return `${base}${white}bg-gradient-to-br from-slate-400 via-slate-500 to-slate-400`;
  }
  return `${base}${white}bg-gradient-to-br from-sky-400 via-blue-400 to-blue-500`;
}

/**
 * 예보 타임라인 한 칸 → 아이콘 이름.
 * 과거(isPast) 구간은 sky 에 실황 텍스트가, 예보 구간은 SKY/PTY 코드가 들어온다.
 */
export function forecastIconOf(item: LifeWeatherApi.TimelinePoint): string {
  if (item.isPast) {
    const cond = item.sky ?? '';
    if (cond.includes('비')) return 'lucide:cloud-rain';
    if (cond.includes('눈')) return 'lucide:cloud-snow';
    if (cond.includes('구름') || cond.includes('흐림')) return 'lucide:cloud';
    return 'lucide:sun';
  }
  const pty = Number.parseInt(String(item.pty ?? '0'), 10) || 0;
  const sky = Number.parseInt(String(item.sky ?? '1'), 10) || 1;
  if (pty > 0) return pty === 3 ? 'lucide:cloud-snow' : 'lucide:cloud-rain';
  if (sky === 1) return 'lucide:sun';
  if (sky === 3) return 'lucide:cloud-sun';
  return 'lucide:cloud';
}

/** 중기예보 하늘 텍스트("맑음"·"구름많음"…) → 아이콘 이름 */
export function iconFromSkyText(text?: null | string): string {
  const t = text ?? '';
  if (t.includes('맑음')) return 'lucide:sun';
  if (t.includes('구름많음')) return 'lucide:cloud-sun';
  if (t.includes('흐림')) return 'lucide:cloud';
  if (t.includes('비') || t.includes('소나기')) return 'lucide:cloud-rain';
  if (t.includes('눈')) return 'lucide:cloud-snow';
  return 'lucide:cloud';
}

/** 기상청 발표 시각(YYYYMMDDHHmm, KST 문자열) → dayjs. 형식이 다르면 null */
export function parseTmFc(tmFc?: null | string): Dayjs | null {
  if (!tmFc || tmFc.length < 12) return null;
  return dayjs(
    `${tmFc.slice(0, 4)}-${tmFc.slice(4, 6)}-${tmFc.slice(6, 8)} ${tmFc.slice(8, 10)}:${tmFc.slice(10, 12)}`,
  );
}

/** 기상청 발표 시각 표시용 포맷 */
export function formatTmFc(tmFc?: null | string, fmt = 'YYYY-MM-DD HH:mm'): string {
  const d = parseTmFc(tmFc);
  return d ? d.format(fmt) : (tmFc ?? '');
}

/** 발표 시각으로부터 경과 시간 문구 */
export function elapsedFromNow(tmFc?: null | string): string {
  const d = parseTmFc(tmFc);
  if (!d) return '';
  const diffMin = Math.floor((Date.now() - d.valueOf()) / 60_000);
  if (diffMin <= 0) return '방금 전';
  const days = Math.floor(diffMin / (60 * 24));
  const hours = Math.floor((diffMin % (60 * 24)) / 60);
  const minutes = diffMin % 60;
  if (days > 0) return `${days}일 ${hours}시간 전`;
  if (hours > 0) return `${hours}시간 ${minutes}분 전`;
  return `${minutes}분 전`;
}
