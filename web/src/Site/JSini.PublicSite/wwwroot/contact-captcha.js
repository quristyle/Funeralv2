// 문의 폼 「새 그림」. 폼을 통째로 보내면 화면이 다시 그려져 스크롤이 맨 위로 가므로, 그림과 토큰만 /_captcha 로 받아 바꾼다.
// 받지 못하면 막지 않고 원래 길(그 단추로 폼 POST)로 보낸다.
(function () {
  'use strict';

  var busy = false;

  document.addEventListener('click', function (e) {
    var btn = e.target.closest && e.target.closest('[data-captcha-refresh]');
    if (!btn || !window.fetch) return;
    if (btn.dataset.fallback === '1') return; // 실패해서 원래 길로 다시 누른 것
    e.preventDefault();
    if (busy) return;
    busy = true;
    btn.setAttribute('aria-busy', 'true');

    var box = btn.closest('.captcha');
    fetch('/_captcha', { cache: 'no-store', credentials: 'same-origin' })
      .then(function (res) {
        if (!res.ok) throw new Error('captcha ' + res.status);
        return res.json();
      })
      .then(function (c) {
        if (!c || !c.token || !c.image || c.image.indexOf('data:image/png;base64,') !== 0) {
          throw new Error('captcha shape');
        }
        var img = box.querySelector('[data-captcha-image]');
        if (!img) {
          var missing = box.querySelector('[data-captcha-missing]');
          img = document.createElement('img');
          img.className = 'captcha__image';
          img.setAttribute('data-captcha-image', '');
          img.width = 180;
          img.height = 60;
          img.alt = btn.dataset.alt || '';
          if (missing) missing.replaceWith(img);
          else btn.parentNode.insertBefore(img, btn);
        }
        img.src = c.image;
        box.querySelector('[data-captcha-token]').value = c.token;
        var answer = box.querySelector('[data-captcha-answer]');
        if (answer) {
          answer.value = '';
          answer.focus({ preventScroll: true });
        }
      })
      .catch(function () {
        btn.dataset.fallback = '1';
        btn.click();
      })
      .then(function () {
        busy = false;
        btn.removeAttribute('aria-busy');
      });
  });
})();
