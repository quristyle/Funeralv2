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
 * ── 무엇을 먼저 묻는가는 **이 기기에 적혀 있다** ──────────────
 *
 * 「내 정보 → 보안 설정」의 [로그인 방식 우선순위] 가 localStorage 에 값을
 * 하나 남기고, 로그인 화면이 그것을 읽어 **판을 올리고 기기 확인을 스스로
 * 연다.** 계정이 아니라 브라우저에 적는 까닭은 아래 `PREFERENCE_KEY` 머리말에
 * 적어 두었다 — 요점은 로그인 화면이 아직 누구인지 모른다는 것이다.
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

    // ── 인증 방식 우선순위 ──────────────────────────────────────
    //
    // 「내 정보 → 보안 설정」에서 고르는 값이다. 두 가지뿐이다.
    //
    //   password  비밀번호가 먼저다(기본). 여태 하던 그대로 — 지문 단추는
    //             폼 아래에 서고 누를 때만 기기를 부른다.
    //   passkey   지문·얼굴이 먼저다. 로그인 화면이 뜨는 순간 기기 확인을
    //             **스스로 연다.** 단추도 폼 위로 올라간다.
    //
    // [왜 계정이 아니라 브라우저에 적나]
    //
    // 이 값을 읽어야 하는 곳이 **로그인 화면**이다. 거기는 아직 로그인 전이라
    // 서버가 누구인지 모른다. 아이디를 실어 서버에게 물어보는 길도 있지만,
    // 그러면 아이디만 알면 「이 계정은 지문을 쓴다」를 밖에서 캐낼 수 있다.
    //
    // 무엇보다 **패스키 자체가 기기마다 따로다.** 계정에 적어 두면 지문을
    // 등록한 적 없는 다른 기기에서도 로그인 화면이 기기 확인을 열려 들고,
    // 그 기기에서는 열 때마다 반드시 실패한다.
    //
    // 열쇠 모양은 옆에 있는 둘(`REMEMBER_ME_USERNAME_<호스트>` ·
    // `KEEP_SIGNED_IN_<호스트>`)과 맞춘다. 호스트마다 따로 두는 까닭도 같다 —
    // 개발과 운영을 같은 브라우저로 오간다.
    const PREFERENCE_KEY = 'AUTH_PRIORITY_' + location.hostname;

    /** 지문·얼굴을 먼저 묻는다. */
    const PASSKEY_FIRST = 'passkey';

    /** 비밀번호가 먼저다. 저장된 값이 없으면 이것이다. */
    const PASSWORD_FIRST = 'password';

    /**
     * 지금 고른 우선순위.
     *
     * **모르면 `password` 다.** 사생활 보호 모드에서는 localStorage 가
     * 던지는데, 그때 지문을 먼저 묻는 쪽으로 기울면 아무것도 등록하지 않은
     * 사람에게도 기기 확인 창이 뜬다.
     */
    function preference() {
        try {
            return window.localStorage.getItem(PREFERENCE_KEY) === PASSKEY_FIRST
                ? PASSKEY_FIRST
                : PASSWORD_FIRST;
        } catch (e) {
            return PASSWORD_FIRST;
        }
    }

    /**
     * 우선순위를 적어 둔다. **기본값은 지운다** — 값 하나로 남겨 두면
     * 나중에 기본을 바꿀 때 옛 기본이 박힌 브라우저가 따라오지 않는다.
     *
     * @returns {string} 적고 나서 다시 읽은 값. 못 적었으면 옛 값이 그대로 온다.
     */
    function setPreference(value) {
        try {
            if (value === PASSKEY_FIRST) {
                window.localStorage.setItem(PREFERENCE_KEY, PASSKEY_FIRST);
            } else {
                window.localStorage.removeItem(PREFERENCE_KEY);
            }
        } catch (e) {
            // 사생활 보호 모드다. 이번 창에서만 못 기억할 뿐이라 화면이
            // 거짓말을 하지 않도록 **읽어서 나온 값**을 돌려준다.
        }

        return preference();
    }

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
     *
     * @param {Error} error 브라우저가 던진 것
     * @param {boolean} forRegister 등록 중인가(아니면 로그인 중이다)
     * @param {boolean} [auto] 사람이 누른 것이 아니라 **화면이 스스로 연 것**인가
     */
    function explain(error, forRegister, auto) {
        const name = error && error.name;

        if (name === 'NotAllowedError') {
            // 취소했거나 시간이 지났다. **둘을 구분할 수 없다** — 규격이
            // 같은 오류로 합쳐 두었다(어느 쪽인지 알려 주면 기기에 등록된
            // 열쇠가 있는지가 새어 나간다).
            //
            // 스스로 연 것은 여기에 **한 가지가 더 섞인다.** 사파리는
            // 사람이 누르지 않은 기기 확인을 같은 오류로 거절한다. 그래서
            // 「취소되었다」고만 말하면 취소한 적 없는 사람이 그 글을 본다.
            // 어느 쪽이든 할 일은 같으므로 **할 일만 적는다.**
            if (auto) {
                return '기기 확인을 바로 열지 못했습니다. 아래 단추를 누르거나 아이디와 비밀번호로 들어가 주세요.';
            }

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
     * 우선순위가 지문·얼굴이면 여기서 **판을 폼 위로 올리고 기기 확인을
     * 스스로 연다.** 그 둘이 「우선순위」라는 말의 뜻 전부다.
     *
     * @param {HTMLElement} panel `[data-jsini-passkey]`
     */
    function initLogin(panel) {
        if (!panel || panel.dataset.jsiniPasskeyReady) return;
        panel.dataset.jsiniPasskeyReady = '1';

        // **못 쓰는 브라우저에서는 꺼내지 않는다.** 눌러도 아무 일이 없는
        // 단추는 고장으로 신고된다(로그인 화면이 소셜 로그인을 뺀 것과 같은 규칙).
        //
        // 우선순위가 지문이어도 마찬가지다 — 고른 것은 이 기기에 적혀 있지만
        // 지금 이 브라우저가 못 하면 **고른 적이 없는 것과 같다.**
        if (!supported()) return;

        const form = panel.querySelector('form');
        const assertion = panel.querySelector('[data-passkey-assertion]');
        const keep = panel.querySelector('[data-passkey-keep]');
        const button = panel.querySelector('[data-passkey-start]');
        const note = panel.querySelector('[data-passkey-note]');

        if (!form || !assertion || !button) return;

        panel.hidden = false;

        /**
         * 기기에게 서명을 받아 폼을 제출한다.
         *
         * @param {boolean} auto 사람이 누른 것이 아니라 화면이 스스로 연 것인가
         */
        async function start(auto) {
            // 이미 기기가 답하기를 기다리는 중이다. 스스로 연 것과 사람이
            // 누른 것이 겹치면 창이 둘 뜬다.
            if (button.disabled) return;

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
                say(note, explain(error, false, auto));
            }
        }

        button.addEventListener('click', function () { start(false); });

        if (preference() !== PASSKEY_FIRST) return;

        promote(panel);

        if (shouldAutoStart()) start(true);
    }

    /**
     * 지문 판을 비밀번호 폼 **위로** 올린다.
     *
     * 자리를 바꾸는 일은 CSS 가 한다(`app.css` 의 `--passkey-first`) —
     * DOM 에서 마디를 들어 옮기면 향상된 이동으로 다시 들어왔을 때 Blazor 가
     * 제 것이 아닌 자리에 마크업을 맞추게 된다.
     *
     * 감싸개가 없는 화면(옛 마크업)에서는 아무 일도 하지 않는다. 자리만
     * 그대로일 뿐 단추도 자동 열기도 그대로 듣는다.
     */
    function promote(panel) {
        const methods = panel.closest('[data-jsini-methods]');

        if (methods) methods.classList.add('jsini-login__methods--passkey-first');
    }

    /**
     * 화면이 뜨자마자 기기 확인을 열어도 되는가.
     *
     * **여는 것보다 안 여는 조건을 세는 편이 짧다.** 지문을 먼저 묻기로 한
     * 사람에게는 여는 것이 기본이고, 아래 넷은 열면 오히려 방해가 되는 자리다.
     */
    function shouldAutoStart() {
        // 1. 실패 안내가 떠 있다 — 방금 폼이 되돌아온 것이다(정적 SSR 이라
        //    문서가 새로 로드된다). 그 위에 기기 창을 덮으면 **무엇이 틀렸는지
        //    읽지도 못하고** 비밀번호를 고쳐 칠 수도 없다.
        if (document.querySelector('.jsini-login__error')) return false;

        // 2. `?noauto=1` — 개발 자동 로그인을 건너뛰는 그 표시다. 「이번에는
        //    로그인 화면을 그대로 보고 싶다」는 뜻이라 여기에도 듣게 한다.
        if (/[?&]noauto=1(&|$)/.test(location.search)) return false;

        // 3. 뒤로 가기로 돌아왔다. 들어갔다가 일부러 나온 사람이 대부분이라
        //    그 자리에서 다시 묻는 것은 붙잡는 것에 가깝다.
        try {
            const entries = performance.getEntriesByType('navigation');

            if (entries && entries[0] && entries[0].type === 'back_forward') return false;
        } catch (e) {
            // 옛 브라우저다. 못 물어본 것은 「앞으로 온 것」으로 본다.
        }

        // 4. 보이지 않는 탭이다. 미리 열어 둔 탭에서 창을 띄우면 지금 보고
        //    있는 화면 위에 남의 창이 뜬 것처럼 보인다.
        return document.visibilityState !== 'hidden';
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

    /**
     * 화면이 「쓸 수 있는가」를 묻는 자리. 예외를 삼키고 거짓으로 답한다.
     *
     * **고른 우선순위도 같이 싣는다.** Blazor Server 에서 JS 호출 하나는
     * 브라우저까지 갔다 오는 왕복 하나라, 따로 물으면 보안 설정 탭이 뜰 때
     * 왕복이 둘이 된다(web/CLAUDE.md 「`PortalBoot` 한 곳으로 모은다」).
     */
    async function status() {
        return {
            supported: supported(),
            platformAuthenticator: await hasPlatformAuthenticator(),
            priority: preference(),
        };
    }

    window.jsiniPasskey = {
        initLogin: initLogin,
        register: register,
        status: status,
        // 「내 정보 → 보안 설정」이 회로에서 부른다. 로그인 화면은 회로가
        // 없어 위 `initLogin` 이 안에서 바로 읽는다.
        preference: preference,
        setPreference: setPreference,
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
