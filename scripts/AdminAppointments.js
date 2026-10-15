var AdminAppointmentsJs = (function () {

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
        $ApiRequest('AdminAppointments/' + method, JSON.stringify(list || []));
    }

    function getCookie(k) {
        var m = document.cookie.match(new RegExp('(?:^|; )' + k + '=([^;]*)'));
        return m ? decodeURIComponent(m[1]) : '';
    }

    function setCookie(k, v, secs) {
        document.cookie = k + '=' + encodeURIComponent(v) + '; path=' + BASE + '; max-age=' + secs + '; samesite=lax';
    }

    function toast(msg) {
        var t = el('ap-toast');
        if (!t) {
            return;
        }
        t.textContent = msg;
        t.classList.add('ap-open');
        clearTimeout(ttimer);
        ttimer = setTimeout(function () {
            t.classList.remove('ap-open');
        }, 3200);
    }

    function openNav() {
        el('ap-nav').classList.add('ap-open');
        el('ap-scrim').classList.add('ap-open');
    }

    function closeNav() {
        el('ap-nav').classList.remove('ap-open');
        el('ap-scrim').classList.remove('ap-open');
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
        el('ap-modal').classList.add('ap-open');
    }

    function closeModal() {
        var m = el('ap-modal');
        if (m) {
            m.classList.remove('ap-open');
        }
    }

    function qs(k) {
        return new URLSearchParams(location.search).get(k) || '';
    }

    function per(k) {
        if (el('ap-per')) {
            el('ap-per').value = k;
        }
        if (k !== 'range') {
            if (el('ap-from')) {
                el('ap-from').value = '';
            }
            if (el('ap-to')) {
                el('ap-to').value = '';
            }
        }
        list(1);
    }

    function period() {
        return [{ key: 'per', vlu: val('ap-per') }, { key: 'pfrom', vlu: val('ap-from') }, { key: 'pto', vlu: val('ap-to') }];
    }

    function search() {
        clearTimeout(stimer);
        stimer = setTimeout(function () {
            list(1);
        }, 320);
    }

    function loaded() {
        times(el('ap-main'));
        if (window.AdminAppointmentsJsloaded) {
            window.AdminAppointmentsJsloaded();
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
        if (!askBox || !askBox.classList.contains('ap-open')) {
            return;
        }
        var x = askParts();
        var f = askDone;
        var v = x.i.value.trim();
        askBox.classList.remove('ap-open');
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
        askBox.className = 'ap-ask';
        askBox.innerHTML = '<div class="ap-askbox" role="alertdialog" aria-modal="true"><h2></h2><p></p><input type="text" maxlength="300" autocomplete="off" spellcheck="false"><em class="ap-err" role="alert"></em><div class="ap-askacts"><button type="button" class="ap-btn ap-btn-ghost ap-btn-sm"></button><button type="button" class="ap-btn ap-btn-sm"></button></div></div>';
        (el('ap-site') || document.body).appendChild(askBox);
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
            if (!askBox.classList.contains('ap-open')) {
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
        x.b[1].className = 'ap-btn ap-btn-sm' + (o.danger ? ' ap-askdel' : '');
        var input = typeof o.value === 'string';
        askBox.firstChild.classList.toggle('ap-askin', input);
        x.i.value = input ? o.value : '';
        x.i.placeholder = o.place || '';
        x.i.setAttribute('aria-label', o.title || '');
        askBox.classList.add('ap-open');
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

    var ptimer = null;

    function openAppt(id) {
        api('Open', [{ key: 'id', vlu: id }]);
    }

    function who() {
        var h = el('ap-mbody') ? el('ap-mbody').querySelector('h2') : null;
        return h && h.firstChild ? h.firstChild.textContent.trim() : 'this patient';
    }

    var STEPS = {
        confirm: ['Confirm this appointment?', 'Mark it as confirmed with the patient.', 'Confirm'],
        checkin: ['Check in ', 'The patient moves to the waiting room.', 'Check in'],
        start: ['Start the visit?', 'The patient moves into the room.', 'Start visit'],
        done: ['Complete this visit?', 'The visit is marked as completed.', 'Complete'],
        reopen: ['Reopen this appointment?', 'It goes back to the schedule as an open visit.', 'Reopen']
    };

    function act(id, a) {
        if (a === 'cancel') {
            ask({ title: 'Cancel this appointment?', text: 'The time becomes free for others. Add a short reason if you like.', value: '', place: 'Reason (optional)', ok: 'Cancel appointment', no: 'Keep it', danger: true }, function (v) {
                api('Act', [{ key: 'id', vlu: id }, { key: 'act', vlu: a }, { key: 'why', vlu: v }]);
            });
            return;
        }
        if (a === 'noshow') {
            ask({ title: 'Mark as no-show?', text: 'Use this when the patient did not come and did not call.', ok: 'Mark no-show', no: 'Back', danger: true }, function () {
                api('Act', [{ key: 'id', vlu: id }, { key: 'act', vlu: a }]);
            });
            return;
        }
        var s = STEPS[a];
        if (!s) {
            return;
        }
        ask({ title: a === 'checkin' ? s[0] + who() + '?' : s[0], text: s[1], ok: s[2], no: 'Back' }, function () {
            api('Act', [{ key: 'id', vlu: id }, { key: 'act', vlu: a }]);
        });
    }

    function desk(id) {
        ask({ title: 'Save the front-desk note?', text: 'The note replaces the one saved before.', ok: 'Save note', no: 'Back' }, function () {
            api('Desk', [{ key: 'id', vlu: id }, { key: 'text', vlu: val('ap-desk') }]);
        });
    }

    function moveForm(id) {
        api('MoveForm', [{ key: 'id', vlu: id }, { key: 'date', vlu: val('ap-mvd') }, { key: 'prov', vlu: val('ap-mvp') }]);
    }

    function hm(t) {
        var p = (t || '').split(':');
        if (p.length !== 2) {
            return t;
        }
        var h = parseInt(p[0], 10);
        return (h % 12 || 12) + ':' + p[1] + (h < 12 ? ' AM' : ' PM');
    }

    function dayTxt(v) {
        var d = new Date(v + 'T12:00:00');
        return isNaN(d.getTime()) ? v : d.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short' });
    }

    function moveTo(id, t, label) {
        ask({ title: 'Move to ' + (label || hm(t)) + '?', text: dayTxt(val('ap-mvd')) + ' with ' + el('ap-mvp').selectedOptions[0].text + '.', ok: 'Move', no: 'Back' }, function () {
            api('Move', [{ key: 'id', vlu: id }, { key: 'date', vlu: val('ap-mvd') }, { key: 'time', vlu: t }, { key: 'prov', vlu: val('ap-mvp') }, { key: 'force', vlu: '0' }]);
        });
    }

    function moveManual(id) {
        var force = el('ap-mvf').checked;
        ask({ title: 'Move to ' + hm(val('ap-mvt')) + '?', text: dayTxt(val('ap-mvd')) + ' with ' + el('ap-mvp').selectedOptions[0].text + (force ? '. Overlap is allowed.' : '.'), ok: 'Move', no: 'Back', danger: force }, function () {
            api('Move', [{ key: 'id', vlu: id }, { key: 'date', vlu: val('ap-mvd') }, { key: 'time', vlu: val('ap-mvt') }, { key: 'prov', vlu: val('ap-mvp') }, { key: 'force', vlu: force ? '1' : '0' }]);
        });
    }

    function newAppt(prov, date, time, pid) {
        api('NewForm', [{ key: 'prov', vlu: prov || '' }, { key: 'date', vlu: date || '' }, { key: 'time', vlu: time || '' }, { key: 'pid', vlu: pid || '' }]);
    }

    function npMode() {
        var m = document.querySelector('input[name="ap-npm"]:checked');
        var isNew = m && m.value === 'new';
        el('ap-npx').hidden = isNew;
        el('ap-npn').hidden = !isNew;
    }

    function pfind() {
        clearTimeout(ptimer);
        ptimer = setTimeout(function () {
            api('PFind', [{ key: 'q', vlu: val('ap-npq') }]);
        }, 250);
    }

    function pchoose(btn, id) {
        el('ap-npid').value = id;
        el('ap-npsel').innerHTML = '<span>' + btn.innerHTML + '</span><button type="button" class="ap-btn ap-btn-sm ap-btn-ghost" onclick="AdminAppointmentsJs.pclear()">Change</button>';
        el('ap-nplist').innerHTML = '';
        el('ap-npq').hidden = true;
    }

    function pclear() {
        el('ap-npid').value = '';
        el('ap-npsel').innerHTML = '';
        el('ap-npq').hidden = false;
        el('ap-npq').value = '';
        el('ap-npq').focus();
    }

    function svFilter() {
        var p = val('ap-nprov');
        var s = el('ap-nsv');
        var opts = s.querySelectorAll('option[data-p]');
        for (var i = 0; i < opts.length; i++) {
            var ok = !p || (' ' + opts[i].getAttribute('data-p') + ' ').indexOf(' ' + p + ' ') >= 0;
            opts[i].disabled = !ok;
            opts[i].hidden = !ok;
        }
        if (s.selectedOptions.length && s.selectedOptions[0].disabled) {
            s.value = '';
        }
    }

    function create() {
        ask({ title: 'Book this appointment?', text: (el('ap-nsv').selectedOptions[0] ? el('ap-nsv').selectedOptions[0].text : '') + ' on ' + dayTxt(val('ap-nd')) + ' at ' + hm(val('ap-nt')) + '.', ok: 'Book', no: 'Back' }, book);
    }

    function book() {
        var m = document.querySelector('input[name="ap-npm"]:checked');
        api('Create', [
            { key: 'mode', vlu: m ? m.value : 'old' }, { key: 'pid', vlu: val('ap-npid') },
            { key: 'first', vlu: val('ap-nfirst') }, { key: 'last', vlu: val('ap-nlast') }, { key: 'phone', vlu: val('ap-nphone') }, { key: 'email', vlu: val('ap-nemail') },
            { key: 'sv', vlu: val('ap-nsv') }, { key: 'prov', vlu: val('ap-nprov') }, { key: 'date', vlu: val('ap-nd') }, { key: 'time', vlu: val('ap-nt') },
            { key: 'src', vlu: val('ap-nsrc') }, { key: 'note', vlu: val('ap-nnote') }, { key: 'force', vlu: el('ap-nforce').checked ? '1' : '0' }
        ]);
    }

    var cur = 1;

    window.AdminAppointmentsJsloaded = function () {
        list(1);
        if (qs('new')) {
            newAppt('', '', '', qs('new'));
        } else if (qs('a')) {
            openAppt(qs('a'));
        }
    };

    function loadArgs() {
        return [];
    }

    function list(pg) {
        cur = pg || 1;
        api('List', [{ key: 'q', vlu: val('ap-q') }, { key: 'st', vlu: val('ap-ast') }, { key: 'prov', vlu: val('ap-apv') }, { key: 'src', vlu: val('ap-asrc') }, { key: 'pg', vlu: String(cur) }].concat(period()));
    }

    function refresh() {
        list(cur);
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
            var id = e.target && e.target.id ? e.target.id.replace('ap-', 'ap-e-') : '';
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
        openAppt: openAppt,
        act: act,
        desk: desk,
        moveForm: moveForm,
        moveTo: moveTo,
        moveManual: moveManual,
        newAppt: newAppt,
        npMode: npMode,
        pfind: pfind,
        pchoose: pchoose,
        pclear: pclear,
        svFilter: svFilter,
        create: create,
        list: list,
        refresh: refresh
    };
})();

if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', AdminAppointmentsJs.reveal);
} else {
    AdminAppointmentsJs.reveal();
}
