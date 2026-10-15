var BookJs = (function () {

    var ttimer = null;
    var BASE = location.pathname.replace(/[^\/]*$/, '');

    function el(id) {
        return document.getElementById(id);
    }

    function val(id) {
        var e = el(id);
        return e ? (e.value || '').trim() : '';
    }

    function call(method, list) {
        $ApiRequest('Book/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('bk-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('bk-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('bk-open');
        }, 3200);
    }

    function openNav() {
        el('bk-nav').classList.add('bk-open');
        el('bk-scrim').classList.add('bk-open');
    }

    function closeNav() {
        el('bk-nav').classList.remove('bk-open');
        el('bk-scrim').classList.remove('bk-open');
    }

    function ago(d) {
        var s = (Date.now() - d.getTime()) / 1000;
        if (s < 60) {
            return 'Just now';
        }
        if (s < 3600) {
            var m = Math.floor(s / 60);
            return m + (m === 1 ? ' min ago' : ' mins ago');
        }
        if (s < 86400) {
            var h = Math.floor(s / 3600);
            return h + (h === 1 ? ' hour ago' : ' hours ago');
        }
        return d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
    }

    function times(root) {
        var list = (root || document).querySelectorAll('time[data-t]');
        for (var i = 0; i < list.length; i++) {
            var t = list[i];
            var d = new Date(t.getAttribute('datetime'));
            if (isNaN(d.getTime())) {
                continue;
            }
            var mode = t.getAttribute('data-t');
            if (mode === 'ago') {
                t.textContent = ago(d);
                t.title = d.toLocaleString();
            } else if (mode === 'hm') {
                t.textContent = d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
            } else if (mode === 'full') {
                t.textContent = d.toLocaleString(undefined, { weekday: 'short', day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit' });
            } else if (mode === 'day') {
                t.textContent = d.toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' });
            }
        }
    }

    var ARGA = 'sv';
    var ARGB = 'p';
    var ARGC = 't';

    function arg(k) {
        return k ? (new URLSearchParams(location.search).get(k) || '') : '';
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function applyTheme(theme) {
        el('bk-site').setAttribute('data-theme', theme === 'dark' ? 'dark' : 'light');
    }

    function toggleTheme() {
        var next = el('bk-site').getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
        applyTheme(next);
        try {
            localStorage.setItem('cl-theme', next);
        } catch (e) {
        }
    }

    function demoBar() {
        el('bk-demo-bar').innerHTML = 'You are using your own demo copy of the clinic. It resets after an hour. <a href="Admin">Open the clinic admin</a>';
    }

    function hydrated(ok, accent, theme) {
        document.documentElement.classList.remove('bk-hydrating');
        if (!ok) {
            setCookie('cl_sbx', '', 0);
            if (window.BookJsready) {
                window.BookJsready();
            }
            return;
        }
        if (accent) {
            el('bk-site').style.setProperty('--bk-accent', accent);
        }
        var stored = '';
        try {
            stored = localStorage.getItem('cl-theme') || '';
        } catch (e) {
        }
        applyTheme(stored || theme);
        demoBar();
        times();
        if (window.BookJsready) {
            window.BookJsready();
        }
    }

    function sandboxed(id) {
        setCookie('cl_sbx', id, 3600);
        demoBar();
    }

    function sbx() {
        return getCookie('cl_sbx');
    }

    var askBox = null;
    var askDone = null;
    var askCheck = null;
    var askBack = null;

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('bk-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('bk-open');
        askDone = null;
        askCheck = null;
        if (askBack && askBack.focus && document.body.contains(askBack)) {
            askBack.focus();
        }
        askBack = null;
        if (ok && f) {
            f(v);
        }
    }

    function askOk() {
        var x = askParts();
        if (askCheck) {
            var m = askCheck(x.i.value.trim());
            if (m) {
                x.e.textContent = m;
                x.i.focus();
                return;
            }
        }
        askClose(true);
    }

    function askBuild() {
        askBox = document.createElement('div');
        askBox.className = 'bk-ask';
        askBox.innerHTML = '<div class="bk-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="bk-err" role="alert"></em><div class="bk-askacts"><button type="button" class="bk-btn bk-btn-ghost bk-btn-sm"></button><button type="button" class="bk-btn bk-btn-sm"></button></div></div>';
        (el('bk-site') || document.body).appendChild(askBox);
        var x = askParts();
        x.b[0].addEventListener('click', function () {
            askClose(false);
        });
        x.b[1].addEventListener('click', askOk);
        x.i.addEventListener('input', function () {
            x.e.textContent = '';
        });
        askBox.addEventListener('mousedown', function (e) {
            if (e.target === askBox) {
                askClose(false);
            }
        });
        window.addEventListener('keydown', function (e) {
            if (!askBox.classList.contains('bk-open')) {
                return;
            }
            if (e.key === 'Escape') {
                e.preventDefault();
                e.stopPropagation();
                askClose(false);
            } else if (e.key === 'Enter' && e.target !== x.b[0]) {
                e.preventDefault();
                e.stopPropagation();
                askOk();
            } else if (e.key === 'Tab') {
                var f = [x.i, x.b[0], x.b[1]].filter(function (n) {
                    return n.offsetParent !== null;
                });
                var at = f.indexOf(document.activeElement);
                e.preventDefault();
                f[(at + (e.shiftKey ? f.length - 1 : 1)) % f.length].focus();
            }
        }, true);
    }

    function ask(o, done) {
        if (!askBox) {
            askBuild();
        }
        var x = askParts();
        askBack = document.activeElement;
        askDone = done;
        askCheck = o.check || null;
        x.h.textContent = o.title || '';
        x.p.textContent = o.text || '';
        x.e.textContent = '';
        x.b[0].textContent = o.no || 'Cancel';
        x.b[1].textContent = o.ok || 'OK';
        x.b[1].className = 'bk-btn bk-btn-sm' + (o.danger ? ' bk-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('bk-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('bk-open');
        if (input) {
            x.i.focus();
            x.i.setSelectionRange(x.i.value.length, x.i.value.length);
        } else {
            x.b[o.danger ? 0 : 1].focus();
        }
    }

    function leave(go) {
        ask({ title: 'Leave without saving?', text: 'Your changes since the last save will be lost.', ok: 'Leave', no: 'Keep editing', danger: true }, go);
    }

    function st() {
        var s = el('bk-st');
        var o = {};
        ['sv', 'p', 'pa', 't', 'wk', 'day', 'pref'].forEach(function (k) {
            o[k] = s ? (s.getAttribute('data-' + k) || '') : '';
        });
        return o;
    }

    function send(o) {
        var list = [{ key: 'sbx', vlu: sbx() }];
        ['sv', 'p', 'pa', 't', 'wk', 'day', 'pref'].forEach(function (k) {
            list.push({ key: k, vlu: o[k] || '' });
        });
        call('Step', list);
    }

    function pickService(k) {
        var o = st();
        o.sv = k;
        o.t = '';
        o.pa = '';
        if (o.pref && !o.p) {
            o.p = o.pref;
        }
        send(o);
    }

    function pickProvider(id) {
        var o = st();
        o.p = id;
        o.t = '';
        o.pa = '';
        o.day = '';
        send(o);
    }

    function pickTime(t, pid) {
        var o = st();
        o.t = t;
        o.pa = pid;
        send(o);
    }

    function week(d) {
        var o = st();
        o.wk = d;
        o.day = '';
        send(o);
    }

    function day(d) {
        var o = st();
        o.day = d;
        send(o);
    }

    function go(step) {
        var o = st();
        if (step === 'sv') {
            o = { pref: o.pref };
        } else if (step === 'p') {
            o.p = '';
            o.t = '';
            o.pa = '';
            o.day = '';
        } else if (step === 't') {
            o.t = '';
            o.pa = '';
        }
        send(o);
    }

    function filter(btn, sp) {
        var chips = btn.parentNode.querySelectorAll('.bk-chip');
        for (var i = 0; i < chips.length; i++) {
            chips[i].classList.toggle('bk-act', chips[i] === btn);
        }
        var opts = document.querySelectorAll('.bk-opt');
        for (var j = 0; j < opts.length; j++) {
            opts[j].hidden = sp !== '' && opts[j].getAttribute('data-sp') !== sp;
        }
    }

    function url() {
        var o = st();
        var q = [];
        if (o.sv) {
            q.push('sv=' + encodeURIComponent(o.sv));
        }
        if (o.p && o.p !== 'any') {
            q.push('p=' + encodeURIComponent(o.p));
        } else if (!o.sv && o.pref) {
            q.push('p=' + encodeURIComponent(o.pref));
        }
        try {
            history.replaceState(null, '', 'Book' + (q.length ? '?' + q.join('&') : ''));
        } catch (e) {
        }
    }

    function after() {
        url();
        var cur = el('bk-cur');
        if (cur) {
            var r = cur.getBoundingClientRect();
            if (r.top < 70 || r.top > window.innerHeight * 0.6) {
                cur.scrollIntoView({ behavior: 'smooth', block: 'start' });
            }
            var f = el('bk-first');
            if (f && !f.value) {
                setTimeout(function () {
                    f.focus({ preventScroll: true });
                }, 350);
            }
        }
    }

    function radio(name) {
        var r = document.querySelector('input[name="' + name + '"]:checked');
        return r ? r.value : '';
    }

    function confirm() {
        var o = st();
        var f = el('bk-dform');
        var list = [{ key: 'sbx', vlu: sbx() }];
        ['sv', 'p', 'pa', 't', 'wk'].forEach(function (k) {
            list.push({ key: k, vlu: o[k] || '' });
        });
        list.push({ key: 'newp', vlu: radio('bk-new') });
        ['first', 'last', 'phone', 'email', 'contact', 'ins', 'note'].forEach(function (k) {
            list.push({ key: k, vlu: val('bk-' + k) });
        });
        var n = parseInt(f.getAttribute('data-qn') || '0', 10);
        for (var i = 0; i < n; i++) {
            list.push({ key: 'q' + i, vlu: el('bk-q' + i) ? val('bk-q' + i) : radio('bk-q' + i) });
        }
        list.push({ key: 'agree', vlu: el('bk-agree').checked ? '1' : '' });
        var errs = f.querySelectorAll('.bk-err');
        for (var j = 0; j < errs.length; j++) {
            errs[j].textContent = '';
        }
        call('Confirm', list);
        return false;
    }

    function invalid() {
        var e = document.querySelector('#bk-dform .bk-err:not(:empty)');
        if (e) {
            var box = e.parentNode;
            box.scrollIntoView({ behavior: 'smooth', block: 'center' });
            var i = box.querySelector('input, select, textarea');
            if (i) {
                i.focus({ preventScroll: true });
            }
        }
    }

    function booked() {
        try {
            history.replaceState(null, '', 'Book');
        } catch (e) {
        }
        window.scrollTo({ top: 0, behavior: 'smooth' });
    }

    function ics(box) {
        var b = el(box);
        if (!b) {
            return;
        }
        var esc = function (s) {
            return String(s || '').replace(/([,;\\])/g, '\\$1');
        };
        var lines = ['BEGIN:VCALENDAR', 'VERSION:2.0', 'PRODID:-//Clinic//Booking//EN', 'BEGIN:VEVENT', 'UID:' + b.getAttribute('data-code') + '@clinic', 'DTSTAMP:' + new Date().toISOString().replace(/[-:]/g, '').replace(/\.\d+/, ''),
            'DTSTART:' + b.getAttribute('data-start'), 'DTEND:' + b.getAttribute('data-end'), 'SUMMARY:' + esc(b.getAttribute('data-title')), 'LOCATION:' + esc(b.getAttribute('data-loc')),
            'DESCRIPTION:' + esc('Booking code ' + b.getAttribute('data-code')), 'END:VEVENT', 'END:VCALENDAR'];
        var blob = new Blob([lines.join('\r\n')], { type: 'text/calendar' });
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob);
        a.download = 'appointment-' + b.getAttribute('data-code') + '.ics';
        document.body.appendChild(a);
        a.click();
        setTimeout(function () {
            URL.revokeObjectURL(a.href);
            a.remove();
        }, 500);
    }

    function start() {
        document.addEventListener('change', function (e) {
            if (e.target && e.target.name && e.target.name.indexOf('bk-q') === 0) {
                var x = el('bk-e-' + e.target.name.replace('bk-', ''));
                if (x) {
                    x.textContent = '';
                }
            }
            if (e.target && e.target.id === 'bk-agree') {
                el('bk-e-agree').textContent = '';
            }
        });
        if (el('bk-st') && el('bk-st').getAttribute('data-t')) {
            after();
        }
    }

    function reveal() {
        var stored = '';
        try {
            stored = localStorage.getItem('cl-theme') || '';
        } catch (e) {
        }
        if (stored) {
            applyTheme(stored);
        }
        times();
        if (sbx()) {
            call('Hydrate', [{ key: 'sbx', vlu: sbx() }, { key: 'argA', vlu: arg(ARGA) }, { key: 'argB', vlu: arg(ARGB) }, { key: 'argC', vlu: arg(ARGC) }]);
        }
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('bk-', 'bk-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
        start();
    }

    if (document.cookie.indexOf('cl_sbx=') >= 0) {
        document.documentElement.classList.add('bk-hydrating');
        setTimeout(function () {
            document.documentElement.classList.remove('bk-hydrating');
        }, 2500);
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        toggleTheme: toggleTheme,
        hydrated: hydrated,
        sandboxed: sandboxed,
        pickService: pickService,
        pickProvider: pickProvider,
        pickTime: pickTime,
        week: week,
        day: day,
        go: go,
        filter: filter,
        after: after,
        confirm: confirm,
        invalid: invalid,
        booked: booked,
        ics: ics
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', BookJs.reveal);
} else {
    BookJs.reveal();
}
