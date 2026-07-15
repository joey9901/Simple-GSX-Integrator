const state = {
    simconnect: false,
    gsx: false,
    systemActive: false,
    aircraft: '',
    beacon: null,
    engines: null,
    brake: null,
    enginesEverRan: null,
    hasMoved: null,
    hotkeyActivation: '',
    hotkeyReset: '',
    services: { boarding: null, pushback: null, deboard: null },
};

let receivedRealData = false;

function send(obj) {
    window.chrome.webview.postMessage(JSON.stringify(obj));
}

function handleMessage(msg) {
    receivedRealData = true;
    switch (msg.type) {
        case 'simconnect': state.simconnect = msg.connected; render(); break;
        case 'gsx':
            state.gsx = msg.running;
            if (!msg.running) state.services = { boarding: null, pushback: null, deboard: null };
            render();
            break;
        case 'system': state.systemActive = msg.active; render(); break;
        case 'aircraft': state.aircraft = msg.title || ''; render(); break;
        case 'state':
            state.beacon = msg.beaconOn;
            state.engines = msg.enginesOn;
            state.brake = msg.parkingBrake;
            state.enginesEverRan = msg.enginesEverRan;
            state.hasMoved = msg.hasMoved;
            render();
            break;
        case 'hotkeys':
            state.hotkeyActivation = msg.activation;
            state.hotkeyReset = msg.reset;
            renderHotkeys();
            break;
        case 'serviceStatus':
            const svc = msg.service;
            if (svc in state.services) state.services[svc] = msg.status;
            renderServices();
            break;
        case 'update':
            showUpdateBanner(msg.version, msg.url);
            break;
        case 'updateProgress':
            setUpdateProgress(msg.value);
            break;
        case 'groundEquip':
            renderGroundEquip(msg);
            break;
        case 'showPicker':
            showPickerModal(msg);
            break;
        case 'hidePicker':
            hidePickerModal();
            break;
        case 'showConfig':
            showConfigModal(msg);
            break;
        case 'hideConfig':
            hideConfigModal();
            break;
    }
}

function render() {
    renderStatusPills();
    renderAircraftHeader();
    renderStateChips();
    renderServices();
}

const PILL_CONFIG = {
    'pill-simconnect': { on: 'SimConnect: Connected', off: 'SimConnect: Offline' },
    'pill-gsx': { on: 'GSX: Running', off: 'GSX: Not Running' },
    'pill-system': { on: 'System: Active', off: 'System: Inactive' },
};

function renderStatusPills() {
    const active = {
        'pill-simconnect': state.simconnect,
        'pill-gsx': state.gsx,
        'pill-system': state.systemActive,
    };
    for (const [id, cfg] of Object.entries(PILL_CONFIG)) {
        const el = document.getElementById(id);
        if (!el) continue;
        const on = active[id];
        el.className = 'pill' + (on ? ' ok' : '');
        el.querySelector('.label').textContent = on ? cfg.on : cfg.off;
    }
}

function renderAircraftHeader() {
    const el = document.getElementById('aircraft-name');
    if (!el) return;
    if (state.aircraft) {
        el.textContent = state.aircraft;
        el.classList.add('has-aircraft');
    } else {
        el.textContent = 'No aircraft loaded';
        el.classList.remove('has-aircraft');
    }
}

function renderStateChips() {
    const u = state.beacon === null;
    setChip('chip-brake', u ? null : state.brake, state.brake ? 'SET' : 'RELEASED', !!state.brake);
    setChip('chip-beacon', u ? null : state.beacon, state.beacon ? 'ON' : 'OFF', !state.beacon);
    setChip('chip-engines', u ? null : state.engines, state.engines ? 'RUNNING' : 'OFF', !state.engines);
    setChip('chip-moved', u ? null : state.hasMoved, state.hasMoved ? 'YES' : 'NO', !state.hasMoved);
    setChip('chip-engines-ran', u ? null : state.enginesEverRan, state.enginesEverRan ? 'YES' : 'NO', !state.enginesEverRan);
}

function setChip(id, known, valueText, isGood) {
    const el = document.getElementById(id);
    if (!el) return;
    el.classList.remove('good', 'bad');
    const val = el.querySelector('.value');
    val.textContent = known === null ? '—' : valueText;
    if (known !== null) el.classList.add(isGood ? 'good' : 'bad');
}

function renderServices() {
    const { beacon, engines, brake, enginesEverRan, hasMoved } = state;
    const u = beacon === null;

    renderCard('card-boarding', [
        { key: 'ENGINES', current: 'OFF', met: u ? null : !engines },
        { key: 'BEACON', current: 'OFF', met: u ? null : !beacon },
        { key: 'PARKING BRAKE', current: 'SET', met: u ? null : !!brake },
        { key: 'HAS MOVED', current: 'NO', met: u ? null : !hasMoved },
        { key: 'ENGINES RAN', current: 'NO', met: u ? null : !enginesEverRan },
    ], state.services.boarding);

    renderCard('card-pushback', [
        { key: 'ENGINES', current: 'OFF', met: u ? null : !engines },
        { key: 'BEACON', current: 'ON', met: u ? null : !!beacon },
        { key: 'PARKING BRAKE', current: 'SET', met: u ? null : !!brake },
        { key: 'HAS MOVED', current: 'NO', met: u ? null : !hasMoved },
        { key: 'ENGINES RAN', current: 'NO', met: u ? null : !enginesEverRan },
    ], state.services.pushback);

    renderCard('card-deboard', [
        { key: 'ENGINES', current: 'OFF', met: u ? null : !engines },
        { key: 'BEACON', current: 'OFF', met: u ? null : !beacon },
        { key: 'PARKING BRAKE', current: 'SET', met: u ? null : !!brake },
        { key: 'HAS MOVED', current: 'YES', met: u ? null : !!hasMoved },
        { key: 'ENGINES RAN', current: 'YES', met: u ? null : !!enginesEverRan },
    ], state.services.deboard);
}

function fmt(val, t, f) {
    if (val === null || val === undefined) return '—';
    return val ? t : f;
}

const SERVICE_STATUS_DISPLAY = {
    unknown: null,
    callable: null,
    notavailable: ['Not Available', 'notavailable'],
    bypassed: ['Bypassed', 'bypassed'],
    requested: ['Requested', 'requested'],
    active: ['Active', 'active'],
    completed: ['Done ✓', 'completed'],
};

function renderCard(id, conditions, serviceStatus) {
    const card = document.getElementById(id);
    if (!card) return;

    const allMet = conditions.every(c => c.met === true);
    const anyUnknown = conditions.some(c => c.met === null);

    const badge = card.querySelector('.card-status');
    if (badge) {
        const display = serviceStatus ? SERVICE_STATUS_DISPLAY[serviceStatus] : null;
        if (display) {
            badge.textContent = display[0];
            badge.className = 'card-status ' + display[1];
        } else if (anyUnknown) {
            badge.textContent = '—';
            badge.className = 'card-status';
        } else if (allMet) {
            if (serviceStatus === 'callable') {
                badge.textContent = 'Ready ✓';
                badge.className = 'card-status ready';
            } else if (serviceStatus != null) {
                // GSX is running but service isn't callable yet
                badge.textContent = 'Not Callable';
                badge.className = 'card-status notcallable';
            } else {
                // No service state from GSX (GSX not running or not yet received)
                badge.textContent = '—';
                badge.className = 'card-status';
            }
        } else {
            badge.textContent = 'Not Ready';
            badge.className = 'card-status';
        }
    }

    const activeService = serviceStatus && !['unknown', 'callable', null].includes(serviceStatus);
    const isReady = allMet && !anyUnknown && serviceStatus === 'callable';
    const isNotCallable = allMet && !anyUnknown && !activeService && serviceStatus != null && serviceStatus !== 'callable';
    card.classList.toggle('ready', isReady);
    card.classList.toggle('notcallable', isNotCallable);
    card.classList.toggle('unmet', !isReady && !isNotCallable);

    const body = card.querySelector('.card-body');
    if (!body) return;
    body.innerHTML = '';

    for (const c of conditions) {
        const row = document.createElement('div');
        row.className = 'condition-row ' + (c.met === null ? 'unknown' : c.met ? 'met' : 'unmet');

        row.innerHTML =
            `<span class="cond-key">${c.key}</span>` +
            `<span class="cond-fill"></span>` +
            `<span class="cond-val">${c.current}</span>`;

        body.appendChild(row);
    }
}

function renderHotkeys() {
    const act = document.getElementById('hotkey-activation');
    const rst = document.getElementById('hotkey-reset');
    if (act && !act.classList.contains('listening')) act.textContent = state.hotkeyActivation || '—';
    if (rst && !rst.classList.contains('listening')) rst.textContent = state.hotkeyReset || '—';
}

let rebindKey = null;

function startRebind(key, badgeEl) {
    rebindKey = key;
    badgeEl.classList.add('listening');
    badgeEl.textContent = 'Press key…';
    send({ type: 'rebindStart', key });
}

function stopRebind(cancelled) {
    rebindKey = null;
    document.getElementById('hotkey-activation')?.classList.remove('listening');
    document.getElementById('hotkey-reset')?.classList.remove('listening');
    if (cancelled) { renderHotkeys(); send({ type: 'rebindCancel' }); }
}

document.addEventListener('keydown', e => {
    if (!rebindKey) return;
    e.preventDefault();
    e.stopPropagation();
    if (e.code === 'Escape') { stopRebind(true); return; }

    const parts = [];
    if (e.ctrlKey) parts.push('CTRL');
    if (e.altKey) parts.push('ALT');
    if (e.shiftKey) parts.push('SHIFT');

    const main = jsCodeToWindowsKey(e.code);
    if (!main) return;
    parts.push(main);

    const combo = parts.join('+');
    const key = rebindKey;
    stopRebind(false);
    send({ type: 'hotkeyCaptured', key, value: combo });

    if (key === 'activation') state.hotkeyActivation = combo;
    if (key === 'reset') state.hotkeyReset = combo;
    renderHotkeys();
});

function jsCodeToWindowsKey(code) {
    if (/^Key([A-Z])$/.test(code)) return code.slice(3);
    if (/^Digit(\d)$/.test(code)) return 'D' + code.slice(5);
    if (/^F(\d{1,2})$/.test(code)) return code;
    if (/^Numpad(\d)$/.test(code)) return 'NumPad' + code.slice(6);
    const map = {
        Space: 'Space', Enter: 'Return', Backspace: 'Back', Tab: 'Tab',
        Insert: 'Insert', Delete: 'Delete', Home: 'Home', End: 'End',
        PageUp: 'PageUp', PageDown: 'PageDown',
        ArrowUp: 'Up', ArrowDown: 'Down', ArrowLeft: 'Left', ArrowRight: 'Right',
        NumpadAdd: 'Add', NumpadSubtract: 'Subtract', NumpadMultiply: 'Multiply',
        NumpadDivide: 'Divide', NumpadDecimal: 'Decimal',
        Minus: 'OemMinus', Equal: 'Oemplus',
        BracketLeft: 'OemOpenBrackets', BracketRight: 'OemCloseBrackets',
        Semicolon: 'OemSemicolon', Quote: 'OemQuotes',
        Comma: 'Oemcomma', Period: 'OemPeriod',
        Backquote: 'Oemtilde', Backslash: 'OemBackslash',
        ControlLeft: null, ControlRight: null,
        AltLeft: null, AltRight: null, ShiftLeft: null, ShiftRight: null,
        MetaLeft: null, MetaRight: null,
    };
    return map[code] ?? null;
}

function showUpdateBanner(version, url) {
    const banner = document.getElementById('update-banner');
    if (!banner) return;
    banner.querySelector('.update-text').textContent = `Update available: v${version}`;
    banner.dataset.url = url;
    banner.classList.remove('hidden');
}

function setUpdateProgress(pct) {
    const bar = document.getElementById('update-progress');
    const fill = document.getElementById('update-progress-fill');
    const btn = document.getElementById('btn-download');
    if (bar) bar.style.display = 'block';
    if (fill) fill.style.width = pct + '%';
    if (btn) btn.disabled = true;
    document.querySelector('.update-text').textContent = `Downloading… ${pct}%`;
}

function renderGroundEquip(msg) {
    const section = document.getElementById('section-ground');
    if (!section) return;
    const show = !!msg.canManageGroundEquipment;
    section.classList.toggle('hidden', !show);
    if (!show) return;

    setChip('chip-chocks', msg.chocks, msg.chocks ? 'SET' : 'REMOVED', msg.chocks === false);

    document.getElementById('chip-gpu')?.classList.remove('hidden');
    setChip('chip-gpu', msg.gpu, msg.gpu ? 'CONNECTED' : 'REMOVED', msg.gpu === false);

    const doorsChip = document.getElementById('chip-doors-open');
    const hasDoors = msg.showDoors && msg.openDoors !== null && msg.openDoors !== undefined;
    doorsChip?.classList.toggle('hidden', !hasDoors);
    if (hasDoors) setChip('chip-doors-open', true,
        msg.openDoors === 0 ? 'ALL CLOSED' : `${msg.openDoors} OPEN`,
        msg.openDoors === 0);
}

let pickerSelected = null;

function showPickerModal(msg) {
    pickerSelected = null;
    const list = document.getElementById('picker-list');
    list.innerHTML = '';

    const addSection = (label) => {
        const el = document.createElement('div');
        el.className = 'picker-section';
        el.textContent = label;
        list.appendChild(el);
    };

    const addItem = (title) => {
        const el = document.createElement('div');
        el.className = 'picker-item' + (title === msg.currentTitle ? ' selected' : '');
        el.textContent = title;
        el.dataset.title = title;
        el.addEventListener('click', () => {
            list.querySelectorAll('.picker-item').forEach(i => i.classList.remove('selected'));
            el.classList.add('selected');
            pickerSelected = title;
        });
        el.addEventListener('dblclick', () => {
            pickerSelected = title;
            confirmPicker();
        });
        list.appendChild(el);
    };

    if (msg.withFamily?.length) {
        addSection('Custom Profiles');
        for (const { family, titles } of msg.withFamily) {
            titles.forEach(addItem);
        }
    }
    if (msg.withoutFamily?.length) {
        addSection('Standard Aircraft');
        msg.withoutFamily.forEach(addItem);
    }

    if (msg.currentTitle) pickerSelected = msg.currentTitle;

    setModal('picker', true);
}

function hidePickerModal() { setModal('picker', false); }

function confirmPicker() {
    if (!pickerSelected) return;
    hidePickerModal();
    send({ type: 'pickerSelected', title: pickerSelected });
}

let savedConfig = null;

function showConfigModal(msg) {
    savedConfig = msg.config;
    document.getElementById('config-title').textContent = msg.title || 'Aircraft Configuration';

    document.getElementById('chkRefuel').checked = !!msg.config.refuelBeforeBoarding;
    document.getElementById('chkCatering').checked = !!msg.config.cateringOnNewFlight;
    document.getElementById('chkCrewComms').checked = !!msg.config.realisticCrewComms;
    document.getElementById('chkRemoteControl').checked = !!msg.config.disableRemoteControl;

    const caps = msg.caps || {};
    const hasCaps = caps.canManageGroundEquipment || caps.canRemoveCovers || caps.canManageDoors;
    const section = document.getElementById('cfg-aircraft-section');
    section.classList.toggle('hidden', !hasCaps);

    const rowGpu = document.getElementById('rowGpu');
    const rowCovers = document.getElementById('rowCovers');
    const rowDoors = document.getElementById('rowDoors');
    if (caps.canManageGroundEquipment) { rowGpu.classList.remove('hidden'); document.getElementById('chkGpu').checked = !!msg.config.manageGroundEquipment; }
    if (caps.canRemoveCovers) { rowCovers.classList.remove('hidden'); document.getElementById('chkCovers').checked = !!msg.config.removeCovers; }
    if (caps.canManageDoors) { rowDoors.classList.remove('hidden'); document.getElementById('chkDoors').checked = !!msg.config.manageDoors; }

    document.getElementById('inpLvar').value = msg.config.activationLvar || '';
    document.getElementById('inpValue').value = msg.config.activationValue ?? 1;

    setModal('config', true);
}

function hideConfigModal() { setModal('config', false); }

function saveConfig() {
    const caps = {};
    const cfg = {
        refuelBeforeBoarding: document.getElementById('chkRefuel').checked,
        cateringOnNewFlight: document.getElementById('chkCatering').checked,
        realisticCrewComms: document.getElementById('chkCrewComms').checked,
        disableRemoteControl: document.getElementById('chkRemoteControl').checked,
        manageGroundEquipment: document.getElementById('rowGpu').classList.contains('hidden') ? savedConfig?.manageGroundEquipment ?? false : document.getElementById('chkGpu').checked,
        removeCovers: document.getElementById('rowCovers').classList.contains('hidden') ? savedConfig?.removeCovers ?? false : document.getElementById('chkCovers').checked,
        manageDoors: document.getElementById('rowDoors').classList.contains('hidden') ? savedConfig?.manageDoors ?? false : document.getElementById('chkDoors').checked,
        activationLvar: document.getElementById('inpLvar').value.trim(),
        activationValue: parseFloat(document.getElementById('inpValue').value) || 1,
    };
    send({ type: 'saveConfig', config: cfg });
}

function setModal(which, visible) {
    const overlay = document.getElementById('modal-overlay');
    const picker = document.getElementById('modal-picker');
    const config = document.getElementById('modal-config');
    if (visible) {
        overlay.classList.remove('hidden');
        if (which === 'picker') { picker.classList.remove('hidden'); config.classList.add('hidden'); }
        if (which === 'config') { config.classList.remove('hidden'); picker.classList.add('hidden'); }
    } else {
        if (which === 'picker') picker.classList.add('hidden');
        if (which === 'config') config.classList.add('hidden');
        if (picker.classList.contains('hidden') && config.classList.contains('hidden'))
            overlay.classList.add('hidden');
    }
}

document.addEventListener('DOMContentLoaded', () => {
    render();

    window.chrome.webview.addEventListener('message', e => {
        try { handleMessage(JSON.parse(e.data)); } catch (err) { console.error('Failed to parse message:', err); }
    });

    document.getElementById('btn-settings')?.addEventListener('click', () => send({ type: 'openConfig' }));
    document.getElementById('chip-moved')?.addEventListener('click', () => send({ type: 'toggleHasMoved' }));
    document.getElementById('chip-engines-ran')?.addEventListener('click', () => send({ type: 'toggleEnginesRan' }));

    document.getElementById('picker-ok')?.addEventListener('click', confirmPicker);
    document.getElementById('picker-cancel')?.addEventListener('click', () => { hidePickerModal(); send({ type: 'pickerCancelled' }); });
    document.getElementById('picker-close')?.addEventListener('click', () => { hidePickerModal(); send({ type: 'pickerCancelled' }); });

    document.getElementById('config-save')?.addEventListener('click', saveConfig);
    document.getElementById('config-cancel')?.addEventListener('click', () => { hideConfigModal(); send({ type: 'cancelConfig' }); });
    document.getElementById('config-close')?.addEventListener('click', () => { hideConfigModal(); send({ type: 'cancelConfig' }); });
    document.getElementById('hotkey-activation')?.addEventListener('click', function () {
        if (!this.classList.contains('listening')) startRebind('activation', this);
    });
    document.getElementById('hotkey-reset')?.addEventListener('click', function () {
        if (!this.classList.contains('listening')) startRebind('reset', this);
    });
    document.getElementById('btn-download')?.addEventListener('click', () => {
        const btn = document.getElementById('btn-download');
        const bar = document.getElementById('update-progress');
        const fill = document.getElementById('update-progress-fill');
        if (btn) { btn.disabled = true; btn.textContent = 'Starting…'; }
        if (bar) bar.style.display = 'block';
        if (fill) fill.style.width = '0%';
        document.querySelector('.update-text').textContent = 'Downloading… 0%';
        send({ type: 'downloadUpdate' });
    });

    document.addEventListener('click', e => {
        if (rebindKey && !e.target.classList.contains('hotkey-badge')) stopRebind(true);
    }, true);
});