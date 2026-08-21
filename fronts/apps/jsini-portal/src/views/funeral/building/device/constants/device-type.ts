/** 장비 유형 코드 → 표시 정보 매핑 상수 */
export const DEVICE_TYPE_MAP: Record<string, { label: string; icon: string; color: string }> = {
  FUNERAL_PORTRAIT: { label: '영정사진', icon: 'lucide:image',       color: 'blue' },
  MULTIMEDIA:       { label: '멀티미디어', icon: 'lucide:play-circle', color: 'violet' },
  ROOM_GUIDE:       { label: '호실 안내', icon: 'lucide:layout-list', color: 'green' },
  ENTRANCE_GUIDE:   { label: '입구 안내', icon: 'lucide:door-open',   color: 'orange' },
  KIOSK:            { label: '키오스크',  icon: 'lucide:tablet',      color: 'purple' },
};

export function getDeviceTypeInfo(type?: string) {
  return DEVICE_TYPE_MAP[type ?? ''] ?? { label: type ?? '알 수 없음', icon: 'lucide:cpu', color: 'default' };
}
