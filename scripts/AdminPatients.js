var AdminPatientsJs = (function () {

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
        $ApiRequest('AdminPatients/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('pt-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('pt-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('pt-open');
        }, 3200);
    }

    function openNav() {
        el('pt-nav').classList.add('pt-open');
        el('pt-scrim').classList.add('pt-open');
    }

    function closeNav() {
        el('pt-nav').classList.remove('pt-open');
        el('pt-scrim').classList.remove('pt-open');
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

    var stimer = null;

    function api(method, list) {
        list = list || [];
        list.push({ key: 'tok', vlu: getCookie('cl_tok') });
        call(method, list);
    }

    function expired() {
        setCookie('cl_tok', '', 0);
        location.replace('AdminLogin');
    }

    function signOut() {
        api('SignOut', []);
    }

    function out() {
        setCookie('cl_tok', '', 0);
        location.href = 'AdminLogin';
    }

    function openModal() {
        el('pt-modal').classList.add('pt-open');
    }

    function closeModal() {
        var m = el('pt-modal');
        if (m) {
            m.classList.remove('pt-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('pt-per')) {
            el('pt-per').value = k;
        }
        if (k !== 'range') {
            if (el('pt-from')) {
                el('pt-from').value = '';
            }
            if (el('pt-to')) {
                el('pt-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('pt-per') }, { key: 'pfrom', vlu: val('pt-from') }, { key: 'pto', vlu: val('pt-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('pt-main'));
        if (window.AdminPatientsJsloaded) {
            window.AdminPatientsJsloaded();
        }
    }

    var askBox = null;
    var askDone = null;
    var askCheck = null;
    var askBack = null;

    function askParts() {
        return { h: askBox.querySelector('h2'), p: askBox.querySelector('p'), i: askBox.querySelector('input'), e: askBox.querySelector('em'), b: askBox.querySelectorAll('button') };
    }

    function askClose(ok) {
        if (!askBox || !askBox.classList.contains('pt-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('pt-open');
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
        askBox.className = 'pt-ask';
        askBox.innerHTML = '<div class="pt-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="pt-err" role="alert"></em><div class="pt-askacts"><button type="button" class="pt-btn pt-btn-ghost pt-btn-sm"></button><button type="button" class="pt-btn pt-btn-sm"></button></div></div>';
        (el('pt-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('pt-open')) {
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
        x.b[1].className = 'pt-btn pt-btn-sm' + (o.danger ? ' pt-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('pt-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('pt-open');
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

    var cur = 1;

    window.AdminPatientsJsloaded = function () {
        list(1);
        if (qs('id')) {
            rec(qs('id'));
        }
    };

    function loadArgs() {
        return [];
    }

    function list(pg) {
        cur = pg || 1;
        api('List', [{ key: 'q', vlu: val('pt-q') }, { key: 'f', vlu: val('pt-pf') }, { key: 'ins', vlu: val('pt-pins') }, { key: 'pg', vlu: String(cur) }]);
    }

    function rec(id) {
        api('Record', [{ key: 'id', vlu: id || '' }]);
    }

    function save(id) {
        api('SavePt', [{ key: 'id', vlu: id || '' }, { key: 'first', vlu: val('pt-pfirst') }, { key: 'last', vlu: val('pt-plast') }, { key: 'phone', vlu: val('pt-pphone') }, { key: 'email', vlu: val('pt-pemail') }, { key: 'contact', vlu: val('pt-pcontact') }, { key: 'ins', vlu: val('pt-pinsx') }, { key: 'note', vlu: val('pt-pnote') }]);
    }

    function reveal() {
        if (!getCookie('cl_tok')) {
            location.replace('AdminLogin');
            return;
        }
        api('Load', loadArgs());
        document.addEventListener('keydown', function (e) {
            if (e.key === 'Escape') {
                closeNav();
                closeModal();
            }
        });
        document.addEventListener('input', function (e) {
            var id = e.target && e.target.id ? e.target.id.replace('pt-', 'pt-e-') : '';
            if (id && el(id)) {
                el(id).textContent = '';
            }
        });
    }

    return {
        reveal: reveal,
        toast: toast,
        openNav: openNav,
        closeNav: closeNav,
        signOut: signOut,
        out: out,
        expired: expired,
        openModal: openModal,
        closeModal: closeModal,
        per: per,
        search: search,
        loaded: loaded,
        list: list,
        rec: rec,
        save: save
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminPatientsJs.reveal);
} else {
    AdminPatientsJs.reveal();
}
