var AdminSettingsJs = (function () {

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
        $ApiRequest('AdminSettings/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('st-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('st-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('st-open');
        }, 3200);
    }

    function openNav() {
        el('st-nav').classList.add('st-open');
        el('st-scrim').classList.add('st-open');
    }

    function closeNav() {
        el('st-nav').classList.remove('st-open');
        el('st-scrim').classList.remove('st-open');
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
        el('st-modal').classList.add('st-open');
    }

    function closeModal() {
        var m = el('st-modal');
        if (m) {
            m.classList.remove('st-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('st-per')) {
            el('st-per').value = k;
        }
        if (k !== 'range') {
            if (el('st-from')) {
                el('st-from').value = '';
            }
            if (el('st-to')) {
                el('st-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('st-per') }, { key: 'pfrom', vlu: val('st-from') }, { key: 'pto', vlu: val('st-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('st-main'));
        if (window.AdminSettingsJsloaded) {
            window.AdminSettingsJsloaded();
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
        if (!askBox || !askBox.classList.contains('st-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('st-open');
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
        askBox.className = 'st-ask';
        askBox.innerHTML = '<div class="st-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="st-err" role="alert"></em><div class="st-askacts"><button type="button" class="st-btn st-btn-ghost st-btn-sm"></button><button type="button" class="st-btn st-btn-sm"></button></div></div>';
        (el('st-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('st-open')) {
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
        x.b[1].className = 'st-btn st-btn-sm' + (o.danger ? ' st-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('st-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('st-open');
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

    function loadArgs() {
        return [];
    }

    function list() {
    }

    function dayOn(cb) {
        cb.closest('.st-orow').classList.toggle('st-closed', !cb.checked);
    }

    function addRow(box, tpl) {
        el(box).insertAdjacentHTML('beforeend', el(tpl).innerHTML);
    }

    function dropRow(btn) {
        btn.parentNode.remove();
    }

    function rows(sel, fn) {
        var out = [];
        var l = document.querySelectorAll(sel);
        for (var i = 0; i < l.length; i++) {
            var v = fn(l[i]);
            if (v) {
                out.push(v);
            }
        }
        return out.join('~');
    }

    function save() {
        var hours = rows('#st-hours [data-day]', function (r) {
            var x = r.querySelectorAll('input');
            return [r.getAttribute('data-day'), x[0].checked ? '1' : '0', x[1].value, x[2].value].join('|');
        });
        var hols = rows('#st-hols .st-hol', function (r) {
            var x = r.querySelectorAll('input');
            return x[0].value ? x[0].value + '|' + x[1].value.replace(/[|~]/g, ' ') : '';
        });
        var faqs = rows('#st-faqs .st-faqr', function (r) {
            var q = r.querySelector('input').value.replace(/[|~]/g, ' ').trim();
            var a = r.querySelector('textarea').value.replace(/[|~]/g, ' ').trim();
            return q && a ? q + '|' + a : '';
        });
        var keys = ['name', 'tag', 'town', 'addr', 'phone', 'email', 'urgent', 'accent', 'theme', 'pw', 'pws', 'rw', 'rws', 'aw', 'aws', 'slot', 'notice', 'ahead', 'cancel', 'tz', 'ins', 'htitle', 'htext', 'atitle', 'atext', 'policy'];
        var list2 = keys.map(function (k) {
            return { key: k, vlu: val('st-s' + k) };
        });
        list2.push({ key: 'prices', vlu: el('st-sprices').checked ? '1' : '0' });
        list2.push({ key: 'h24', vlu: el('st-sh24').checked ? '1' : '0' });
        list2.push({ key: 'hours', vlu: hours });
        list2.push({ key: 'hols', vlu: hols });
        list2.push({ key: 'faqs', vlu: faqs });
        api('Save', list2);
        return false;
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
            var id = e.target && e.target.id ? e.target.id.replace('st-', 'st-e-') : '';
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
        save: save,
        dayOn: dayOn,
        addRow: addRow,
        dropRow: dropRow
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminSettingsJs.reveal);
} else {
    AdminSettingsJs.reveal();
}
