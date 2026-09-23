/**
 * 패스키(WebAuthn) 배관 — 지문 · 얼굴 · 윈도우 Hello 로 로그인한다.
 *
 * ── 손님이 둘이고 부르는 방식이 다르다 ────────────────────────
 *
 *  1. **로그인 화면**(정적 SSR, 회로 없음) — 스스로 문서를 훑어 붙는다.
 *     JS interop 을 쓸 수 없는 화면이라 다른 길이 없다.
 *  2. **내 정보 → 보안 설정**(회로 있음) — `IJSRuntime` 으로 `register` 를
 *     부른다. 결과를 C# 으로 돌려주고 서버에 올리는 일은 그쪽이 한다.
 *
 * ── BFF 를 깨지 않는다 ─────────────────────────────────────────
 *
 * 이 파일은 **게이트웨이(/api/…)를 직접 부르지 않는다.** 로그인은 셸의
 * `/passkey/options` 로 도전값을 받고, 받은 서명은 폼에 담아 서버로 보낸다.
 * 등록은 회로를 거쳐 C# 이 게이트웨이를 부른다. 토큰이 브라우저로 내려오는
 * 자리가 한 군데도 없어야 한다(web/CLAUDE.md 「인증 — BFF」).
 *
 * ── 오류를 삼키는 자리와 안 삼키는 자리 ───────────────────────
 *
 * 지원 여부 확인은 삼킨다 — 못 쓰는 브라우저에서 단추를 감추면 그만이다.
 * 사람이 단추를 눌러 부른 것은 **까닭을 담아 돌려준다.** 거기서 삼키면
 * 화면이 「아무 일도 안 일어났다」밖에 말할 수 없고, 그것이 이 기능에서
 * 가장 흔한 신고다(지문을 댔는데 아무 반응이 없다).
 */
(function () {
    'use strict';

    /** 셸이 도전값을 중계해 주는 자리. 게이트웨이가 아니다 — 머리말 참고. */
    const OPTIONS_URL = '/passkey/options';

    // ── base64url ↔ 바이트 ──────────────────────────────────────
    //
    // WebAuthn 의 이진값은 전부 ArrayBuffer 인데 JSON 으로는 못 싣는다.
    // 서버와 **base64url** 로 주고받는다(AuthServer 의 Base64Url 과 짝).

    function toBytes(value) {
        const text = value.replace(/-/g, '+').replace(/_/g, '/');
        const padded = text.padEnd(text.length + ((4 - (text.length % 4)) % 4), '=');
        const binary = window.atob(padded);
        const bytes = new Uint8Array(binary.length);

        for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);

        return bytes;
    }

    function toBase64Url(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';

        // `String.fromCharCode(...bytes)` 는 큰 버퍼에서 인자 개수 한도를
        // 넘겨 터진다. 공개 키가 몇백 바이트라 지금은 안 터지지만, 한 번
        // 터지면 증상이 「특정 기기에서만 등록이 안 된다」라 찾기 어렵다.
        for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);

        return window.btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }

    /** 평범한 base64 (공개 키는 .NET 이 그대로 읽는 SPKI DER 이라 이쪽이다). */
    function toBase64(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';

        for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);

        return window.btoa(binary);
    }

    // ── 지원 여부 ───────────────────────────────────────────────

    /**
     * 이 브라우저가 패스키를 다룰 수 있는가.
     *
     * **보안 문맥(https 또는 localhost)이 아니면 API 자체가 없다.** 그래서
     * 이 한 줄이 「http 로 열었다」까지 함께 걸러 준다.
     */
    function supported() {
        return typeof window.PublicKeyCredential === 'function'
            && !!(navigator.credentials && navigator.credentials.get);
    }

    /**
     * 이 기기에 **붙박이 인증기**(지문 · 얼굴 · Hello)가 있는가.
     *
     * 없어도 보안 열쇠로는 쓸 수 있으므로 **단추를 감추는 근거로 쓰지
     * 않는다.** 등록 화면이 「이 기기에는 지문이 없습니다」를 말하는 데만 쓴다.
     */
    async function hasPlatformAuthenticator() {
        if (!supported() || !window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable) {
            return false;
        }

        try {
            return await window.PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable();
        } catch (e) {
            return false;
        }
    }

    // ── 서버가 준 설정을 브라우저가 먹는 모양으로 ──────────────

    function decodeRequestOptions(publicKey) {
        const options = Object.assign({}, publicKey);

        options.challenge = toBytes(publicKey.challenge);
        options.allowCredentials = (publicKey.allowCredentials || []).map(function (c) {
            return { type: c.type, id: toBytes(c.id) };
        });

        return options;
    }

    function decodeCreationOptions(publicKey) {
        const options = Object.assign({}, publicKey);

        options.challenge = toBytes(publicKey.challenge);
        options.user = Object.assign({}, publicKey.user, { id: toBytes(publicKey.user.id) });
        options.excludeCredentials = (publicKey.excludeCredentials || []).map(function (c) {
            return { type: c.type, id: toBytes(c.id) };
        });

        return options;
    }

    // ── 사람에게 할 말 ──────────────────────────────────────────

    /**
     * 브라우저가 던진 오류를 **사용자가 할 수 있는 일**로 옮긴다.
     *
     * 원문(`NotAllowedError: The operation either timed out or was not
     * allowed`)을 그대로 보여 주면 아무도 무엇을 해야 할지 모른다.
     */
    function explain(error, forRegister) {
        const name = error && error.name;

        if (name === 'NotAllowedError') {
            // 취소했거나 시간이 지났다. **둘을 구분할 수 없다** — 규격이
            // 같은 오류로 합쳐 두었다(어느 쪽인지 알려 주면 기기에 등록된
            // 열쇠가 있는지가 새어 나간다).
            return forRegister
                ? '기기 확인이 취소되었거나 시간이 지났습니다.'
                : '기기 확인이 취소되었거나 시간이 지났습니다. 아이디와 비밀번호로 들어갈 수 있습니다.';
        }

        if (name === 'InvalidStateError') {
            return '이 기기는 이미 등록되어 있습니다.';
        }

        if (name === 'NotSupportedError') {
            return '이 기기가 지원하지 않는 방식입니다.';
        }

        if (name === 'SecurityError') {
            // 도메인이 안 맞는다. 운영에서 이 글자가 보이면 AuthServer 의
            // WebAuthn:RelyingPartyId(기본값은 Portal:BaseUrl 의 호스트)를 본다.
            return '이 주소에서는 기기 인증을 쓸 수 없습니다. 관리자에게 알려 주십시오.';
        }

        return (error && error.message) || '기기 인증에 실패했습니다.';
    }

    // ── 로그인 ──────────────────────────────────────────────────

    /**
     * 로그인 화면의 패스키 판을 살린다. 두 번 불러도 한 번만 듣는다.
     *
     * @param {HTMLElement} panel `[data-jsini-passkey]`
     */
    function initLogin(panel) {
        if (!panel || panel.dataset.jsiniPasskeyReady) return;
        panel.dataset.jsiniPasskeyReady = '1';

        // **못 쓰는 브라우저에서는 꺼내지 않는다.** 눌러도 아무 일이 없는
        // 단추는 고장으로 신고된다(로그인 화면이 소셜 로그인을 뺀 것과 같은 규칙).
        if (!supported()) return;

        const form = panel.querySelector('form');
        const assertion = panel.querySelector('[data-passkey-assertion]');
        const keep = panel.querySelector('[data-passkey-keep]');
        const button = panel.querySelector('[data-passkey-start]');
        const note = panel.querySelector('[data-passkey-note]');

        if (!form || !assertion || !button) return;

        panel.hidden = false;

        button.addEventListener('click', async function () {
            say(note, null);
            button.disabled = true;

            try {
                const username = readUsername();
                const options = await fetchOptions(username);
                const credential = await navigator.credentials.get({
                    publicKey: decodeRequestOptions(options.publicKey),
                });

                if (!credential) throw new Error('기기가 응답하지 않았습니다.');

                // 감춘 칸에 담고 제출한다. **여기서 게이트웨이를 부르지 않는다** —
                // 서버가 받아 넘겨야 쿠키를 구울 수 있다(머리말).
                assertion.value = JSON.stringify(packAssertion(options.sessionId, credential));

                // 사용자가 보는 「로그인 유지」는 옆 폼에 하나뿐이다.
                // 그 값을 이쪽 폼으로 옮겨 담는다.
                if (keep) {
                    const source = document.querySelector('[data-keep-signed-in]');
                    keep.checked = !!(source && source.checked);
                }

                // 감춘 칸은 Blazor 가 `change` 로 값을 읽는다. 값만 넣고
                // 제출하면 **서버에는 빈 문자열이 간다.**
                fire(assertion);
                if (keep) fire(keep);

                form.requestSubmit();
            } catch (error) {
                button.disabled = false;
                say(note, explain(error, false));
            }
        });
    }

    /** 아이디 칸에 적힌 값. 비어 있으면 기기가 스스로 열쇠를 고른다. */
    function readUsername() {
        const input = document.querySelector('#username');
        return input && input.value ? input.value.trim() : null;
    }

    async function fetchOptions(username) {
        const response = await fetch(OPTIONS_URL, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ username: username }),
            // 같은 오리진이지만 적어 둔다 — 셸이 신원을 볼 수도 있는 자리다.
            credentials: 'same-origin',
        });

        if (!response.ok) {
            throw new Error('지금은 기기 인증을 쓸 수 없습니다. 잠시 뒤 다시 시도해 주세요.');
        }

        return await response.json();
    }

    function packAssertion(sessionId, credential) {
        const response = credential.response;

        return {
            sessionId: sessionId,
            credentialId: credential.id,
            authenticatorData: toBase64Url(response.authenticatorData),
            clientDataJson: toBase64Url(response.clientDataJSON),
            signature: toBase64Url(response.signature),
            userHandle: response.userHandle ? toBase64Url(response.userHandle) : null,
        };
    }

    /**
     * Blazor 가 감춘 칸의 값을 읽게 한다.
     *
     * 정적 SSR 의 `InputText` 도 결국 `<input name="...">` 이라 폼 제출에는
     * 값이 실린다. 그래도 사건을 쏘는 것은 **대화형으로 바뀌어도 같은 코드가
     * 돌게** 하려는 것이다 — 나중에 이 화면이 회로를 갖게 되면 사건이 없는
     * 값은 서버에 닿지 않는다.
     */
    function fire(element) {
        element.dispatchEvent(new Event('change', { bubbles: true }));
    }

    function say(note, message) {
        if (!note) return;

        note.textContent = message || '';
        note.hidden = !message;
    }

    // ── 등록 (회로에서 부른다) ──────────────────────────────────

    /**
     * 이 기기에 패스키를 만든다.
     *
     * 서버가 준 설정을 그대로 받아 기기에 넘기고, 돌아온 결과를 **서버가
     * 저장할 수 있는 모양**으로 바꿔 돌려준다. 저장하는 일은 C# 이 한다.
     *
     * **CBOR 을 풀지 않는다.** `getPublicKey()` 와 `getAuthenticatorData()`
     * 가 `attestationObject` 안의 필요한 조각을 이미 풀어서 준다
     * (WebAuthn Level 3 — 크롬 85 · 사파리 16 · 파이어폭스 119 이상).
     * 그 둘이 없는 브라우저는 여기서 **분명히 거절한다** — 서버에 CBOR
     * 해석기를 두는 것보다 「이 브라우저에서는 안 된다」고 말하는 편이 낫다.
     *
     * @param {object} options 서버가 준 `{ sessionId, publicKey }`
     * @param {string} label 사람이 붙인 이름
     * @returns {Promise<object>} `{ ok, error?, ...저장할 값 }`
     */
    async function register(options, label) {
        if (!supported()) {
            return { ok: false, error: '이 브라우저는 기기 인증(패스키)을 지원하지 않습니다.' };
        }

        let credential;

        try {
            credential = await navigator.credentials.create({
                publicKey: decodeCreationOptions(options.publicKey),
            });
        } catch (error) {
            return { ok: false, error: explain(error, true) };
        }

        if (!credential) {
            return { ok: false, error: '기기가 응답하지 않았습니다.' };
        }

        const response = credential.response;

        if (typeof response.getPublicKey !== 'function'
            || typeof response.getAuthenticatorData !== 'function') {
            return {
                ok: false,
                error: '이 브라우저는 아직 패스키 등록을 지원하지 않습니다. 최신 브라우저에서 다시 시도해 주세요.',
            };
        }

        const publicKey = response.getPublicKey();

        if (!publicKey) {
            return { ok: false, error: '기기가 공개 키를 내주지 않았습니다.' };
        }

        return {
            ok: true,
            sessionId: options.sessionId,
            credentialId: credential.id,
            publicKey: toBase64(publicKey),
            algorithm: response.getPublicKeyAlgorithm(),
            authenticatorData: toBase64Url(response.getAuthenticatorData()),
            clientDataJson: toBase64Url(response.clientDataJSON),
            label: label || null,
            // 붙박이인지 따로 꽂는 것인지. 목록에서 그림을 가르는 데 쓴다.
            attachment: credential.authenticatorAttachment || null,
        };
    }

    /** 화면이 「쓸 수 있는가」를 묻는 자리. 예외를 삼키고 거짓으로 답한다. */
    async function status() {
        return {
            supported: supported(),
            platformAuthenticator: await hasPlatformAuthenticator(),
        };
    }

    window.jsiniPasskey = {
        initLogin: initLogin,
        register: register,
        status: status,
    };

    // ── 스스로 붙는다 ───────────────────────────────────────────
    //
    // 로그인 화면에는 회로가 없어 C# 이 불러 줄 수 없고, 이 파일은 `defer`
    // 라 화면의 인라인 <script> 보다 늦게 돈다. 그래서 훑는 일을 여기서 한다.
    //
    // `enhancedload` 도 듣는다 — 향상된 이동으로 로그인 화면에 들어오면
    // 문서가 새로 파싱되지 않아 아래 한 번으로는 못 받는다.
    function scan() {
        const panels = document.querySelectorAll('[data-jsini-passkey]');

        for (let i = 0; i < panels.length; i++) initLogin(panels[i]);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', hook);
    } else {
        hook();
    }

    function hook() {
        scan();

        if (window.Blazor && window.Blazor.addEventListener) {
            window.Blazor.addEventListener('enhancedload', scan);
        }
    }
})();
