/**
 * JS 파일 로드
 * @param src JS 파일 주소
 */
function loadScript(src: string) {
  return new Promise<void>((resolve, reject) => {
    if (document.querySelector(`script[src="${src}"]`)) {
      // 이미 로드된 경우 즉시 resolve
      return resolve();
    }
    const script = document.createElement('script');
    script.src = src;
    script.addEventListener('load', () => resolve());
    script.addEventListener('error', () =>
      reject(new Error(`Failed to load script: ${src}`)),
    );
    document.head.append(script);
  });
}

export { loadScript };
