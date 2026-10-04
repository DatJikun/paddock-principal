/* =========================================================================
 * PADDOCK PRINCIPAL · LIVE RACE SCREEN (PP-052 / Issue #153)
 * -------------------------------------------------------------------------
 * Full live race screen prototype with 2D map, live standings, pit tracking,
 * telemetry, playback speed controls, and bilingual support.
 *
 * Invariant: UI contains ZERO game logic; it only renders frames and events
 * emitted by the simulation engine (TECH §3).
 * ========================================================================= */

(() => {
  'use strict';

  // Bilingual strings dictionary
  const I18N = {
    pl: {
      backToTrack: 'Przegląd toru',
      round: 'Runda',
      gpTitle: 'GP Wielkiej Brytanii 1976',
      circuitInfo: 'Brands Hatch · 4,206 km · 76 okrążeń',
      greenFlag: 'Zielona flaga',
      yellowFlag: 'Żółta flaga',
      chequeredFlag: 'Koniec wyścigu',
      lap: 'Okrążenie',
      raceTime: 'Czas wyścigu',
      trackTemp: 'Tor / Powietrze',
      classification: 'Klasyfikacja na żywo',
      activeCars: 'aut na torze',
      pos: 'Poz.',
      no: 'Nr',
      driver: 'Kierowca',
      gap: 'Strata',
      interval: 'Odstęp',
      tyres: 'Opony',
      pit: 'Pit',
      lastLap: 'Okr.',
      eventsRadio: 'Wydarzenia i radio',
      telemetry: 'Telemetria bolidu',
      speed: 'Prędkość',
      tyreWear: 'Stan opon',
      gapAhead: 'Do poprzedzającego',
      gapBehind: 'Przewaga z tyłu',
      pitStops: 'Zjazdy do boksu',
      followCar: 'Śledź',
      fitTrack: 'Wyśrodkuj',
      pause: 'Pauza',
      leader: 'Lider',
      out: 'DNF',
      inBox: 'W boksie',
      pitting: 'Zjazd',
      exiting: 'Wyjazd',
      garageTitle: 'Garaż Elf Team Tyrrell · P34',
      engineerQuote: 'Inżynier wyścigowy'
    },
    en: {
      backToTrack: 'Circuit Overview',
      round: 'Round',
      gpTitle: '1976 British Grand Prix',
      circuitInfo: 'Brands Hatch · 4.206 km · 76 laps',
      greenFlag: 'Green Flag',
      yellowFlag: 'Yellow Flag',
      chequeredFlag: 'Chequered Flag',
      lap: 'Lap',
      raceTime: 'Race Time',
      trackTemp: 'Track / Ambient',
      classification: 'Live Classification',
      activeCars: 'cars on track',
      pos: 'Pos',
      no: 'No',
      driver: 'Driver',
      gap: 'Gap',
      interval: 'Interval',
      tyres: 'Tyres',
      pit: 'Pit',
      lastLap: 'Last',
      eventsRadio: 'Events & Radio',
      telemetry: 'Car Telemetry',
      speed: 'Speed',
      tyreWear: 'Tyre Condition',
      gapAhead: 'Gap Ahead',
      gapBehind: 'Gap Behind',
      pitStops: 'Pit Stops',
      followCar: 'Follow',
      fitTrack: 'Fit',
      pause: 'Pause',
      leader: 'Leader',
      out: 'DNF',
      inBox: 'In Box',
      pitting: 'In Pit',
      exiting: 'Exit Pit',
      garageTitle: 'Elf Team Tyrrell Garage · P34',
      engineerQuote: 'Race Engineer'
    }
  };

  let currentLang = 'pl';
  let activeEngine = null;
  let activeMap = null;
  let animFrameId = null;
  let lastTimestamp = 0;
  let playbackSpeed = 1.0;
  let isPaused = false;
  let selectedDriverId = 3; // Jody Scheckter (#3 Elf Tyrrell)

  function t(key) {
    return (I18N[currentLang] && I18N[currentLang][key]) || key;
  }

  function formatRaceTime(ms) {
    const totalSec = Math.floor(ms / 1000);
    const m = Math.floor(totalSec / 60);
    const s = totalSec % 60;
    const tenths = Math.floor((ms % 1000) / 100);
    return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}.${tenths}`;
  }

  function formatGap(sec) {
    if (!sec || sec < 0.05) return t('leader');
    if (sec < 60) return `+${sec.toFixed(1).replace('.', ',')} s`;
    const m = Math.floor(sec / 60);
    const s = (sec % 60).toFixed(1).replace('.', ',');
    return `+${m}:${s.padStart(4, '0')}`;
  }

  function formatLap(ms) {
    if (!ms) return '—';
    const m = Math.floor(ms / 60000);
    const s = ((ms % 60000) / 1000).toFixed(1).replace('.', ',');
    return `${m}:${s.padStart(4, '0')}`;
  }

  /* ============ LIVE RACE SCREEN (PP-052) ============ */
  S.live = (n = 9) => {
    const roundIdx = Math.max(0, Math.min(15, (Number(n) || 9) - 1));
    const cal = DB.calendar[roundIdx];
    const track = DB.tracks[cal[3]] || DB.tracks.brands_hatch;

    return `
    <div class="live-screen" data-round="${roundIdx + 1}">
      <!-- TOP HUD: Race status, Timer, Playback speed, Language -->
      <header class="live-hud">
        <div class="hud-left">
          <a class="btn sm" href="#/wyscig/${roundIdx + 1}">${UI.icon(UI.back, 16)}<span id="txt-back">${t('backToTrack')}</span></a>
          <div class="hud-round-badge"><span class="rno-pill">R${roundIdx + 1}</span></div>
          <div class="hud-race-titles">
            <h2 id="txt-gp-title">${roundIdx === 8 ? t('gpTitle') : cal[2] + ' 1976'}</h2>
            <small id="txt-circuit-info">${track.name} · ${String(track.len).replace('.', ',')} km · ${cal[4]} ${currentLang === 'pl' ? 'okrążeń' : 'laps'}</small>
          </div>
          <div class="hud-flag flag-green" id="hud-flag-badge">
            <i class="flag-dot"></i><span id="hud-flag-text">${t('greenFlag')}</span>
          </div>
        </div>

        <div class="hud-center">
          <div class="hud-tile">
            <span class="meta" id="lbl-lap">${t('lap')}</span>
            <b class="num" id="val-lap">1 / ${cal[4]}</b>
          </div>
          <div class="hud-tile">
            <span class="meta" id="lbl-time">${t('raceTime')}</span>
            <b class="num" id="val-time">00:00.0</b>
          </div>
          <div class="hud-tile">
            <span class="meta" id="lbl-temp">${t('trackTemp')}</span>
            <b class="num">28°C / 24°C</b>
          </div>
        </div>

        <div class="hud-right">
          <!-- Playback Speed Controls -->
          <div class="speed-group" role="group" aria-label="Playback Speed">
            <button class="sp-btn" data-speed="0" id="sp-pause" title="${t('pause')} (Spacja)">⏸</button>
            <button class="sp-btn on" data-speed="1" title="1x">1x</button>
            <button class="sp-btn" data-speed="2" title="2x">2x</button>
            <button class="sp-btn" data-speed="5" title="5x">5x</button>
            <button class="sp-btn" data-speed="10" title="10x">10x</button>
          </div>
          <!-- Language Toggle (PL / EN) -->
          <div class="lang-group">
            <button class="lang-btn ${currentLang === 'pl' ? 'on' : ''}" data-lang="pl">PL</button>
            <button class="lang-btn ${currentLang === 'en' ? 'on' : ''}" data-lang="en">EN</button>
          </div>
        </div>
      </header>

      <!-- MAIN LIVE GRID: Left Map & Telemetry, Right Standings & Events -->
      <div class="live-grid">
        <!-- LEFT: 2D Track Map & Selected Driver Telemetry -->
        <div class="live-col-map">
          <div class="map-wrapper" id="map-wrapper">
            <canvas id="live-map-canvas"></canvas>

            <!-- Map Floating Tools -->
            <div class="map-tools-dock">
              <button class="map-btn" id="map-btn-zoom-in" title="Zoom in">+</button>
              <button class="map-btn" id="map-btn-zoom-out" title="Zoom out">−</button>
              <button class="map-btn" id="map-btn-fit" title="${t('fitTrack')}">Fit</button>
              <button class="map-btn" id="map-btn-follow" title="${t('followCar')}">🎯</button>
            </div>

            <!-- Circuit Silhouette Tag -->
            <div class="map-circuit-tag">
              <b>${track.name}</b> · <span id="map-circuit-sub">${currentLang === 'pl' ? 'Kierunek zgodny z zegarem' : 'Clockwise'}</span>
            </div>
          </div>

          <!-- Selected Driver Telemetry Card -->
          <section class="panel live-telemetry-panel" id="telemetry-panel">
            <!-- Rendered by updateTelemetry() -->
          </section>
        </div>

        <!-- RIGHT: Live Standings & Pit Tracker & Event Feed -->
        <div class="live-col-sidebar">
          <!-- Live Standings Table -->
          <section class="panel live-standings-card">
            <header class="sidebar-header">
              <h3 id="txt-classification">${t('classification')}</h3>
              <span class="meta" id="val-active-count">23 / 23 ${t('activeCars')}</span>
            </header>
            <div class="tbl-scroll live-table-scroll">
              <table class="table tight live-standings-tbl">
                <thead>
                  <tr>
                    <th class="c" style="width:36px" id="th-pos">${t('pos')}</th>
                    <th class="c" style="width:32px" id="th-no">${t('no')}</th>
                    <th id="th-driver">${t('driver')}</th>
                    <th class="r" style="width:72px" id="th-gap">${t('gap')}</th>
                    <th class="r" style="width:68px" id="th-int">${t('interval')}</th>
                    <th class="c" style="width:52px" id="th-tyres">${t('tyres')}</th>
                    <th class="c" style="width:36px" id="th-pit">${t('pit')}</th>
                    <th class="r" style="width:66px" id="th-last">${t('lastLap')}</th>
                  </tr>
                </thead>
                <tbody id="live-standings-rows">
                  <!-- Rows generated dynamically -->
                </tbody>
              </table>
            </div>
          </section>

          <!-- Tyrrell Garage & Event Ticker -->
          <section class="panel live-bottom-card">
            <!-- Tyrrell Team Garage Status -->
            <div class="tyrrell-garage-bar">
              <div class="tg-header">
                <span class="tg-title" id="txt-garage-title">${t('garageTitle')}</span>
                <span class="tg-badge">P34 / 2</span>
              </div>
              <div class="tg-cars" id="tyrrell-cars-status">
                <!-- Scheckter #3 & Depailler #4 live status -->
              </div>
            </div>

            <!-- Race Commentary & Event Stream -->
            <div class="events-feed-wrapper">
              <div class="events-feed-title" id="txt-events-title">${t('eventsRadio')}</div>
              <div class="events-feed-list" id="events-feed-list">
                <!-- Event items -->
              </div>
            </div>
          </section>
        </div>
      </div>
    </div>`;
  };

  // Ensure full-height fit without page scroll (HANDOFF_UI.md §3)
  S.live.fit = true;

  /* ============ AFTER RENDER: INITIALIZE ENGINE & CANVAS ============ */
  S.live.after = (n = 9) => {
    const roundIdx = Math.max(0, Math.min(15, (Number(n) || 9) - 1));
    const cal = DB.calendar[roundIdx];
    const circuitKey = cal[3] || 'brands_hatch';

    // Cancel existing simulation / animation loop if any
    if (animFrameId) {
      cancelAnimationFrame(animFrameId);
      animFrameId = null;
    }

    // Initialize Simulation Engine & Track Spline
    activeEngine = new RaceSimEngine(circuitKey);

    // Initialize Canvas Renderer
    const canvasEl = document.getElementById('live-map-canvas');
    if (!canvasEl) return;

    activeMap = new RaceMapCanvas(canvasEl, activeEngine.spline, (clickedCarId) => {
      selectedDriverId = clickedCarId;
      activeMap.setSelectedCar(clickedCarId);
      updateTelemetry(activeEngine.cars.find(c => c.carId === clickedCarId));
      highlightStandingsRow(clickedCarId);
    });

    activeMap.onFollowChange = (isFollowing) => {
      const followBtn = document.getElementById('map-btn-follow');
      if (followBtn) followBtn.classList.toggle('on', isFollowing);
    };

    // Resize Observer for robust responsiveness
    const wrapper = document.getElementById('map-wrapper');
    if (window.ResizeObserver && wrapper) {
      const ro = new ResizeObserver(() => {
        if (activeMap) activeMap.handleResize();
      });
      ro.observe(wrapper);
    }

    // Attach HUD & Playback Button Listeners
    setupPlaybackControls();
    setupMapControls();
    setupLanguageControls();

    // Bind Spacebar to Pause
    const handleKeydown = (e) => {
      if (e.code === 'Space' && cur && cur.name === 'live') {
        e.preventDefault();
        togglePause();
      }
    };
    window.removeEventListener('keydown', window._liveRaceKeyHandler);
    window._liveRaceKeyHandler = handleKeydown;
    window.addEventListener('keydown', handleKeydown);

    // Initial Telemetry & Synchronous First Frame Render
    const initialDriver = activeEngine.cars.find(c => c.carId === selectedDriverId) || activeEngine.cars[0];
    updateTelemetry(initialDriver);

    const initFrame = activeEngine.tick(0);
    if (initFrame) {
      updateHUD(initFrame);
      updateStandingsTable(initFrame.cars);
      updateGarageBar(initFrame.cars);
      highlightStandingsRow(selectedDriverId);
    }
    if (activeMap) {
      activeMap.render({
        timeMs: activeEngine.timeMs,
        lap: activeEngine.leaderLap,
        cars: activeEngine.cars
      });
    }

    // Simulation Frame Loop
    lastTimestamp = performance.now();
    const frameLoop = (timestamp) => {
      // If user navigated away from live screen, gracefully stop loop
      if (!cur || cur.name !== 'live') {
        if (animFrameId) cancelAnimationFrame(animFrameId);
        animFrameId = null;
        return;
      }

      const elapsedRealMs = Math.min(100, timestamp - lastTimestamp);
      lastTimestamp = timestamp;

      if (!isPaused && activeEngine) {
        const simDeltaMs = elapsedRealMs * playbackSpeed;
        const frame = activeEngine.tick(simDeltaMs);
        if (frame) {
          updateHUD(frame);
          updateStandingsTable(frame.cars);
          updateGarageBar(frame.cars);
          if (frame.events && frame.events.length > 0) {
            appendEvents(frame.events);
          }
          const sel = frame.cars.find(c => c.carId === selectedDriverId);
          if (sel) updateTelemetry(sel);
        }
      }

      // Render 2D track map canvas
      if (activeMap && activeEngine) {
        const frameData = {
          timeMs: activeEngine.timeMs,
          lap: activeEngine.leaderLap,
          cars: activeEngine.cars
        };
        activeMap.render(frameData);
      }

      animFrameId = requestAnimationFrame(frameLoop);
    };

    animFrameId = requestAnimationFrame(frameLoop);
  };

  /* ============ HUD UPDATES ============ */
  function updateHUD(frame) {
    const valLap = document.getElementById('val-lap');
    if (valLap) valLap.textContent = `${frame.lap} / ${frame.totalLaps}`;

    const valTime = document.getElementById('val-time');
    if (valTime) valTime.textContent = formatRaceTime(frame.timeMs);

    const flagBadge = document.getElementById('hud-flag-badge');
    const flagText = document.getElementById('hud-flag-text');
    if (flagBadge && flagText) {
      if (frame.flag === 'yellow') {
        flagBadge.className = 'hud-flag flag-yellow';
        flagText.textContent = t('yellowFlag');
      } else if (frame.flag === 'chequered') {
        flagBadge.className = 'hud-flag flag-chequered';
        flagText.textContent = t('chequeredFlag');
      } else {
        flagBadge.className = 'hud-flag flag-green';
        flagText.textContent = t('greenFlag');
      }
    }
  }

  /* ============ LIVE STANDINGS TABLE ============ */
  function updateStandingsTable(cars) {
    const tbody = document.getElementById('live-standings-rows');
    if (!tbody) return;

    // Build or update rows
    const rowsHtml = cars.map(c => {
      const isMine = !!c.isPlayer;
      const isSelected = (c.carId === selectedDriverId);
      const isPitting = (c.pitState === 'in_box' || c.pitState === 'pitting' || c.pitState === 'exiting');
      const rowClass = [
        isMine ? 'mine' : '',
        isSelected ? 'selected-row' : '',
        'live-row'
      ].filter(Boolean).join(' ');

      // Pit status / Tyre compound badge
      const tyreBadge = `<span class="compound-tag cmp-${c.tyreCompound.toLowerCase()}">${c.tyreCompound}</span>`;
      let gapDisplay = formatGap(c.gapLeaderSec);
      if (isPitting) {
        gapDisplay = `<span class="pit-badge">${c.pitState === 'in_box' ? t('inBox') : t('pitting')}</span>`;
      }

      const intDisplay = c.position === 1 ? '—' : `+${c.gapAheadSec.toFixed(1).replace('.', ',')}`;

      return `
      <tr class="${rowClass}" data-car-id="${c.carId}">
        <td class="c num"><b class="pos-num">${c.position}</b></td>
        <td class="c num muted">${c.carId}</td>
        <td>
          <div class="live-drv-cell">
            <span class="drv-dot" style="background:${c.livery.primary}"></span>
            <span class="drv-name">${c.driverName}</span>
          </div>
        </td>
        <td class="r num font-mono">${gapDisplay}</td>
        <td class="r num muted font-mono">${intDisplay}</td>
        <td class="c">${tyreBadge} <small class="num muted">${c.tyreWearPct}%</small></td>
        <td class="c num">${c.pitStopCount || '—'}</td>
        <td class="r num font-mono">${formatLap(c.lastLapMs)}</td>
      </tr>`;
    }).join('');

    tbody.innerHTML = rowsHtml;

    // Attach row click handlers to focus / follow car
    tbody.querySelectorAll('tr[data-car-id]').forEach(tr => {
      tr.onclick = () => {
        const carId = Number(tr.dataset.carId);
        selectedDriverId = carId;
        if (activeMap) {
          activeMap.setSelectedCar(carId);
          activeMap.setFollowMode(true);
        }
        const followBtn = document.getElementById('map-btn-follow');
        if (followBtn) followBtn.classList.add('on');
        const sel = cars.find(c => c.carId === carId);
        if (sel) updateTelemetry(sel);
        highlightStandingsRow(carId);
      };
    });
  }

  function highlightStandingsRow(carId) {
    document.querySelectorAll('#live-standings-rows tr').forEach(tr => {
      tr.classList.toggle('selected-row', Number(tr.dataset.carId) === carId);
    });
  }

  /* ============ TYRRELL GARAGE STATUS STRIP ============ */
  function updateGarageBar(cars) {
    const bar = document.getElementById('tyrrell-cars-status');
    if (!bar) return;

    // Find Jody Scheckter (#3) and Patrick Depailler (#4)
    const scheckter = cars.find(c => c.carId === 3) || cars[0];
    const depailler = cars.find(c => c.carId === 4) || cars[1];

    const carPill = (c) => {
      const isSel = (c.carId === selectedDriverId);
      const isPitting = (c.pitState === 'in_box' || c.pitState === 'pitting');
      return `
      <div class="tg-car-tile ${isSel ? 'sel' : ''}" data-car-id="${c.carId}">
        <div class="tg-car-top">
          <b>#${c.carId} ${c.driverName}</b>
          <span class="st ${isPitting ? 'solid' : 'team'}">${isPitting ? t('inBox') : `P${c.position}`}</span>
        </div>
        <div class="tg-car-stats">
          <div class="tg-stat"><span class="meta">${t('gap')}</span><b class="num">${formatGap(c.gapLeaderSec)}</b></div>
          <div class="tg-stat"><span class="meta">${t('tyres')}</span><b class="num">${c.tyreWearPct}%</b></div>
          <div class="tg-stat"><span class="meta">${t('pit')}</span><b class="num">${c.pitStopCount}</b></div>
        </div>
      </div>`;
    };

    bar.innerHTML = carPill(scheckter) + carPill(depailler);

    bar.querySelectorAll('.tg-car-tile').forEach(tile => {
      tile.onclick = () => {
        const carId = Number(tile.dataset.carId);
        selectedDriverId = carId;
        if (activeMap) {
          activeMap.setSelectedCar(carId);
          activeMap.setFollowMode(true);
        }
        const followBtn = document.getElementById('map-btn-follow');
        if (followBtn) followBtn.classList.add('on');
        const sel = cars.find(c => c.carId === carId);
        if (sel) updateTelemetry(sel);
        highlightStandingsRow(carId);
      };
    });
  }

  /* ============ TELEMETRY CARD ============ */
  function updateTelemetry(car) {
    const panel = document.getElementById('telemetry-panel');
    if (!panel || !car) return;

    const team = DB.teams[car.teamId]?.name || car.teamId;
    const isMine = !!car.isPlayer;

    // Quotes from race engineer
    const quotes = isMine ? [
      '„Opony w optymalnym oknie. Jody, utrzymuj stałe tempo na wejściu w Hawthorns.”',
      '„Świetny sektor 2. Masz czyste powietrze przed sobą, ciśnij.”',
      '„Temperatura oleju stabilna. Przygotowujemy boks na okrążenie 18.”'
    ] : [
      '„Równomierne tempo, kontrola zużycia ogumienia.”'
    ];
    const radioQuote = quotes[car.carId % quotes.length];

    panel.innerHTML = `
    <div class="telemetry-inner">
      <div class="tel-driver-hero">
        <span class="tel-num" style="background:${car.livery.primary};color:${car.livery.text}">${car.carId}</span>
        <div class="tel-driver-info">
          <div class="tel-name"><b>${car.driverName}</b> ${UI.flag(car.nat)}</div>
          <small class="muted">${team} · P${car.position}</small>
        </div>
        <div class="tel-follow-badge">
          <span class="st ${isMine ? 'hi' : ''}">${isMine ? 'Elf Team Tyrrell' : 'Rywal'}</span>
        </div>
      </div>

      <div class="tel-fields-grid">
        <div class="tel-field">
          <span class="meta">${t('speed')}</span>
          <b class="num tel-speed">${car.speedKmh} <small>km/h</small></b>
        </div>
        <div class="tel-field">
          <span class="meta">${t('tyreWear')}</span>
          <div class="tel-tyre-bar-wrap">
            <b class="num">${car.tyreWearPct}% (${car.tyreCompound})</b>
            <div class="bar thin"><i style="width:${car.tyreWearPct}%;background:${car.tyreWearPct < 40 ? 'var(--bad)' : 'var(--good)'}"></i></div>
          </div>
        </div>
        <div class="tel-field">
          <span class="meta">${t('gapAhead')}</span>
          <b class="num font-mono">${car.position === 1 ? '—' : `+${car.gapAheadSec.toFixed(1).replace('.', ',')} s`}</b>
        </div>
        <div class="tel-field">
          <span class="meta">${t('lastLap')}</span>
          <b class="num font-mono">${formatLap(car.lastLapMs)}</b>
        </div>
      </div>

      <div class="tel-radio-quote">
        <span class="meta">${t('engineerQuote')}:</span>
        <p>${radioQuote}</p>
      </div>
    </div>`;
  }

  /* ============ EVENT FEED ============ */
  function appendEvents(events) {
    const list = document.getElementById('events-feed-list');
    if (!list) return;

    for (const ev of events) {
      const item = document.createElement('div');
      item.className = 'event-item event-' + ev.type;

      const text = currentLang === 'pl' ? ev.textPl : ev.textEn;
      const typeIcons = {
        overtake: '▲',
        pit_in: '⏱',
        pit_out: '➜',
        fastest_lap: '⚡',
        yellow_flag: '⚠',
        green_flag: '✓',
        chequered_flag: '🏁'
      };

      item.innerHTML = `
      <span class="event-icon">${typeIcons[ev.type] || '•'}</span>
      <div class="event-body">
        <span class="event-text">${text}</span>
        <small class="event-time">${formatRaceTime(ev.timeMs)} · Okr. ${ev.lap}</small>
      </div>`;

      list.prepend(item);
    }

    // Keep max 30 items in DOM
    while (list.children.length > 30) {
      list.removeChild(list.lastChild);
    }
  }

  /* ============ PLAYBACK & MAP CONTROLS ============ */
  function setupPlaybackControls() {
    const pauseBtn = document.getElementById('sp-pause');
    const speedBtns = document.querySelectorAll('.speed-group .sp-btn[data-speed]');

    speedBtns.forEach(btn => {
      btn.onclick = () => {
        const sp = Number(btn.dataset.speed);
        if (sp === 0) {
          togglePause();
        } else {
          isPaused = false;
          playbackSpeed = sp;
          speedBtns.forEach(b => b.classList.remove('on'));
          btn.classList.add('on');
          if (pauseBtn) pauseBtn.textContent = '⏸';
        }
      };
    });
  }

  function togglePause() {
    isPaused = !isPaused;
    const pauseBtn = document.getElementById('sp-pause');
    if (pauseBtn) {
      pauseBtn.classList.toggle('on', isPaused);
      pauseBtn.textContent = isPaused ? '▶' : '⏸';
    }
  }

  function setupMapControls() {
    const btnIn = document.getElementById('map-btn-zoom-in');
    const btnOut = document.getElementById('map-btn-zoom-out');
    const btnFit = document.getElementById('map-btn-fit');
    const btnFollow = document.getElementById('map-btn-follow');

    if (btnIn) btnIn.onclick = () => activeMap && activeMap.zoomBy(1.3);
    if (btnOut) btnOut.onclick = () => activeMap && activeMap.zoomBy(1 / 1.3);
    if (btnFit) btnFit.onclick = () => {
      if (activeMap) {
        activeMap.resetView();
        activeMap.setFollowMode(false);
        if (btnFollow) btnFollow.classList.remove('on');
      }
    };
    if (btnFollow) btnFollow.onclick = () => {
      if (activeMap) {
        const next = !activeMap.followMode;
        activeMap.setFollowMode(next);
        btnFollow.classList.toggle('on', next);
      }
    };
  }

  function setupLanguageControls() {
    const langBtns = document.querySelectorAll('.lang-group .lang-btn');
    langBtns.forEach(btn => {
      btn.onclick = () => {
        const lang = btn.dataset.lang;
        if (lang === currentLang) return;
        currentLang = lang;
        langBtns.forEach(b => b.classList.toggle('on', b.dataset.lang === currentLang));
        refreshLanguageStrings();
      };
    });
  }

  function refreshLanguageStrings() {
    const setText = (id, key) => {
      const el = document.getElementById(id);
      if (el) el.textContent = t(key);
    };

    setText('txt-back', 'backToTrack');
    setText('txt-gp-title', 'gpTitle');
    setText('lbl-lap', 'lap');
    setText('lbl-time', 'raceTime');
    setText('lbl-temp', 'trackTemp');
    setText('txt-classification', 'classification');
    setText('th-pos', 'pos');
    setText('th-no', 'no');
    setText('th-driver', 'driver');
    setText('th-gap', 'gap');
    setText('th-int', 'interval');
    setText('th-tyres', 'tyres');
    setText('th-pit', 'pit');
    setText('th-last', 'lastLap');
    setText('txt-garage-title', 'garageTitle');
    setText('txt-events-title', 'eventsRadio');

    if (activeEngine) {
      const sel = activeEngine.cars.find(c => c.carId === selectedDriverId);
      if (sel) updateTelemetry(sel);
    }
  }

  // Alias for route matching
  S.wyscig_live = S.live;

})();

