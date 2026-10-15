var AdminServicesJs = (function () {

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
        $ApiRequest('AdminServices/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ss-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ss-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ss-open');
        }, 3200);
    }

    function openNav() {
        el('ss-nav').classList.add('ss-open');
        el('ss-scrim').classList.add('ss-open');
    }

    function closeNav() {
        el('ss-nav').classList.remove('ss-open');
        el('ss-scrim').classList.remove('ss-open');
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
        el('ss-modal').classList.add('ss-open');
    }

    function closeModal() {
        var m = el('ss-modal');
        if (m) {
            m.classList.remove('ss-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('ss-per')) {
            el('ss-per').value = k;
        }
        if (k !== 'range') {
            if (el('ss-from')) {
                el('ss-from').value = '';
            }
            if (el('ss-to')) {
                el('ss-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('ss-per') }, { key: 'pfrom', vlu: val('ss-from') }, { key: 'pto', vlu: val('ss-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('ss-main'));
        if (window.AdminServicesJsloaded) {
            window.AdminServicesJsloaded();
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
        if (!askBox || !askBox.classList.contains('ss-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('ss-open');
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
        askBox.className = 'ss-ask';
        askBox.innerHTML = '<div class="ss-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="ss-err" role="alert"></em><div class="ss-askacts"><button type="button" class="ss-btn ss-btn-ghost ss-btn-sm"></button><button type="button" class="ss-btn ss-btn-sm"></button></div></div>';
        (el('ss-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('ss-open')) {
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
        x.b[1].className = 'ss-btn ss-btn-sm' + (o.danger ? ' ss-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('ss-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('ss-open');
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

    function tab(btn, id) {
        var bs = btn.parentNode.querySelectorAll('.ss-tab');
        for (var i = 0; i < bs.length; i++) {
            bs[i].classList.toggle('ss-act', bs[i] === btn);
        }
        var ps = el('ss-mbody').querySelectorAll('.ss-pane');
        for (var j = 0; j < ps.length; j++) {
            ps[j].hidden = ps[j].id !== id;
        }
    }

    function picked(name) {
        var r = document.querySelector('input[name="' + name + '"]:checked');
        return r ? r.value : '';
    }

    function checks(sel) {
        var out = [];
        var l = document.querySelectorAll(sel);
        for (var i = 0; i < l.length; i++) {
            if (l[i].checked) {
                out.push(l[i].value);
            }
        }
        return out.join(',');
    }

    window.AdminServicesJsloaded = function () {
        list();
    };

    function loadArgs() {
        return [];
    }

    var cur = 1;

    function list(pg) {
        cur = pg || cur;
        api('List', [{ key: 'tab', vlu: el('ss-tabv').value }, { key: 'q', vlu: val('ss-q') }, { key: 'team', vlu: val('ss-tm') }, { key: 'pg', vlu: String(cur) }]);
    }

    function view(btn, t) {
        el('ss-tabv').value = t;
        el('ss-tml').hidden = t === 'team';
        cur = 1;
        var bs = btn.parentNode.querySelectorAll('.ss-tab');
        for (var i = 0; i < bs.length; i++) {
            bs[i].classList.toggle('ss-act', bs[i] === btn);
        }
        list(1);
    }

    function editSv(k) {
        api('SvForm', [{ key: 'k', vlu: k || '' }]);
    }

    function editTeam(k) {
        api('TeamForm', [{ key: 'k', vlu: k || '' }]);
    }

    function saveSv(k) {
        api('SaveSv', [
            { key: 'k', vlu: k || '' }, { key: 'name', vlu: val('ss-sname') }, { key: 'team', vlu: val('ss-steam') }, { key: 'short', vlu: val('ss-sshort') },
            { key: 'text', vlu: val('ss-stext') }, { key: 'min', vlu: val('ss-smin') }, { key: 'price', vlu: val('ss-sprice') }, { key: 'order', vlu: val('ss-sorder') },
            { key: 'expect', vlu: val('ss-sexp') }, { key: 'img', vlu: picked('ss-simg') }, { key: 'provs', vlu: checks('#ss-sprov input') },
            { key: 'online', vlu: el('ss-sonline').checked ? '1' : '0' }, { key: 'newok', vlu: el('ss-snew').checked ? '1' : '0' }, { key: 'active', vlu: el('ss-sactive').checked ? '1' : '0' }
        ]);
    }

    function addQ() {
        var box = el('ss-qs');
        if (box.querySelectorAll('.ss-qrow').length >= 6) {
            toast('Up to six questions per team.');
            return;
        }
        var t = el('ss-qtpl');
        box.insertAdjacentHTML('beforeend', t.innerHTML);
        var rows = box.querySelectorAll('.ss-qrow');
        rows[rows.length - 1].querySelector('input').focus();
    }

    function dropQ(btn) {
        btn.closest('.ss-qrow').remove();
    }

    function saveTeam(k) {
        var qs2 = [];
        var rows = document.querySelectorAll('#ss-qs .ss-qrow');
        for (var i = 0; i < rows.length; i++) {
            var t = rows[i].querySelector('input[type="text"]').value.replace(/[|~]/g, ' ').trim();
            if (t) {
                qs2.push([rows[i].getAttribute('data-id') || '', t, rows[i].querySelector('select').value, rows[i].querySelector('input[type="checkbox"]').checked ? '1' : '0'].join('|'));
            }
        }
        api('SaveTeam', [
            { key: 'k', vlu: k || '' }, { key: 'name', vlu: val('ss-tname') }, { key: 'blurb', vlu: val('ss-tblurb') }, { key: 'color', vlu: val('ss-tcolor') },
            { key: 'order', vlu: val('ss-torder') }, { key: 'img', vlu: picked('ss-timg') }, { key: 'active', vlu: el('ss-tactive').checked ? '1' : '0' }, { key: 'qs', vlu: qs2.join('~') }
        ]);
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
            var id = e.target && e.target.id ? e.target.id.replace('ss-', 'ss-e-') : '';
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
        tab: tab,
        view: view,
        list: list,
        editSv: editSv,
        editTeam: editTeam,
        saveSv: saveSv,
        addQ: addQ,
        dropQ: dropQ,
        saveTeam: saveTeam
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminServicesJs.reveal);
} else {
    AdminServicesJs.reveal();
}
