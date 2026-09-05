/**
 * 라이트/다크 전환.
 *
 * [왜 Blazor 가 아니라 여기서 하나]
 *
 * 테마는 첫 화면이 그려지기 전에 정해져야 한다. Blazor Server 회로는 문서가
 * 그려진 뒤에 붙으므로, C# 에서 정하면 라이트로 한 번 그려졌다가 다크로 바뀐다.
 * 이 스크립트는 <body> 맨 앞에서 동기로 실행되어 그 번쩍임을 없앤다.
 *
 * [스타일시트를 갈아 끼우지 않는다]
 *
 * App.razor 가 라이트/다크 두 장을 미리 실어 두고 하나를 disabled 로 꺼 둔다.
 * href 를 바꿔 새로 내려받게 하면 그동안 스타일 없는 화면이 보인다.
 */
window.jsiniTheme = (function () {
    const KEY = 'jsini.theme';

    function apply(dark) {
        document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
        document.querySelectorAll('link[data-jsini-theme]').forEach((link) => {
            link.disabled = (link.dataset.jsiniTheme === 'dark') !== dark;
        });
    }

    function stored() {
        try {
            const saved = localStorage.getItem(KEY);
            if (saved === 'dark') return true;
            if (saved === 'light') return false;
        } catch {
            // 사생활 보호 모드에서 localStorage 가 막힌다. 기본값으로 간다.
        }
        // 정해 둔 값이 없으면 기기 설정을 따른다.
        return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches;
    }

    let dark = stored();
    apply(dark);

    return {
        isDark: () => dark,
        toggle: function () {
            dark = !dark;
            apply(dark);
            try {
                localStorage.setItem(KEY, dark ? 'dark' : 'light');
            } catch {
                // 저장하지 못해도 이번 세션에서는 바뀐 채로 쓴다.
            }
            return dark;
        },
    };
})();
