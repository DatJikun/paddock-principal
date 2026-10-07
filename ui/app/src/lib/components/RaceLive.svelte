<script lang="ts">
  /*
   * Race mode (PP-052, HANDOFF_UI "Tryb wyścigu"): the race the career just ran, played back from its tape on the host's
   * clock. Display only: the result is already written, every viewer reads the same clock, and an order (pause, speed, skip)
   * moves it for everyone. Strategy is the strategist's: the panel shows his calls, it does not make them.
   */
  import { onMount } from 'svelte';
  import { command, HUMAN_MANAGER_ID, query } from '../api/client';
  import type { LiveClockView, LiveEventView, LiveFramesView, LiveRaceView } from '../api/types.generated';
  import {
    clockNow,
    conditionAt,
    covered,
    fastestAt,
    flagAt,
    formatClock,
    formatTowerGap,
    lastIndexAt,
    radioAt,
    sampleAt,
    towerAt,
    transcriptAt,
    tyreFamily,
    tyreLetter,
  } from '../live-race.mjs';
  import { livery } from '../livery.mjs';
  import { fallbackPoints, RaceMap, TrackSpline } from '../race-map.mjs';
  import { formatLapTime } from '../race.mjs';
  import type { Tr } from '../ui';
  import Flag from './Flag.svelte';

  let {
    tr,
    pushed,
    onexit,
    backKey = 'live.ui.back',
  }: { tr: Tr; pushed: LiveClockView | null; onexit: () => void; backKey?: string } = $props();

  const call = { managerId: HUMAN_MANAGER_ID };
  /* How far ahead of the race time frames are fetched, in seconds of real time at the current speed. */
  const LOOKAHEAD_S = 12;
  const MAX_WINDOW_MS = 300_000;

  let race = $state<LiveRaceView | null>(null);
  let clock = $state<LiveClockView | null>(null);
  let readAt = 0;
  let now = $state(0);
  let tower = $state(true);
  let transcript = $state(false);
  let mine = $state(false);
  let selected = $state<string | null>(null);
  let following = $state(false);
  let curtain = $state(true);
  /* Speed of the selected car, read from the frames a few times a second (km/h). */
  let selectedKmh = $state<number | null>(null);
  let mapHost: HTMLDivElement | undefined = $state();
  let canvas: HTMLCanvasElement | undefined = $state();

  let map: RaceMap | null = null;
  let windows: LiveFramesView[] = [];
  let fetching = false;
  let lastSamples = new Map<string, ReturnType<typeof sampleAt>>();
  /* Cars that have retired by now leave the map (read from the tower, refreshed with it). */
  let gone = new Set<string>();

  function adopt(view: LiveClockView) {
    clock = view;
    readAt = performance.now();
  }

  $effect(() => {
    if (pushed && pushed.active) adopt(pushed);
  });

  async function control(action: string, speed: number | null = null) {
    try {
      adopt(await command('liveRaceControl', { managerId: HUMAN_MANAGER_ID, action, speed }));
    } catch {
      /* A refused order leaves the clock as it was; the next reading shows the truth. */
    }
  }

  async function fetchFrames(t: number) {
    if (fetching || !clock) return;
    const ahead = Math.max(60_000, clock.speed * LOOKAHEAD_S * 1000 * 2);
    const from = Math.max(0, t - 2_000);
    const to = Math.min(race?.durationMs ?? t, from + Math.min(MAX_WINDOW_MS, ahead));
    fetching = true;
    try {
      const view = await query('liveFrames', { managerId: HUMAN_MANAGER_ID, fromMs: from, toMs: to });
      if (view.found) windows = [...windows.filter((w) => w.toMs >= t - 5_000).slice(-2), view];
    } finally {
      fetching = false;
    }
  }

  function frame() {
    if (!race || !clock) return;
    const t = clockNow(clock, readAt, performance.now());
    const ahead = Math.min(race.durationMs, t + clock.speed * LOOKAHEAD_S * 1000);
    if (!covered(windows, t, ahead)) void fetchFrames(t);
    const cars = [];
    for (const car of race.cars) {
      if (gone.has(car.carId)) continue;
      const sample = sampleAt(windows, car.carId, t) ?? lastSamples.get(car.carId) ?? null;
      if (!sample) continue;
      lastSamples.set(car.carId, sample);
      cars.push(sample);
    }
    map?.render(cars);
  }

  function line(event: LiveEventView) {
    const parameters: Record<string, string> = {};
    for (const arg of event.args) {
      const family = arg.name === 'tyres' ? tyreFamily(arg.value) : null;
      parameters[arg.name] = family ? tr.t(`live.tyre.${family}`) : arg.value;
    }
    return tr.t(event.key ?? '', parameters);
  }

  function onKey(event: KeyboardEvent) {
    if (event.target instanceof HTMLInputElement) return;
    const key = event.key;
    if (key === ' ') {
      event.preventDefault();
      void togglePause();
    } else if (key === 't' || key === 'T') tower = !tower;
    else if (key === 'r' || key === 'R') transcript = !transcript;
    else if ((key === 'f' || key === 'F') && selected) setFollow(!following);
    else if (key === '0') placeMap();
    else if (key === '+' || key === '=') map?.zoomBy(1.25);
    else if (key === '-') map?.zoomBy(0.8);
    else if (key === 'Escape') {
      /* Esc closes the car card; leaving the race is only "Wróć do gry" (HANDOFF_UI). */
      event.preventDefault();
      event.stopPropagation();
      select(null);
    }
  }

  async function togglePause() {
    if (!clock || clock.finished) return;
    await control(clock.paused ? 'play' : 'pause');
  }

  function select(id: string | null) {
    selected = id;
    if (map) map.selected = id;
    if (!id) setFollow(false);
  }

  function setFollow(on: boolean) {
    following = on;
    if (map) map.follow = on;
  }

  /* The track is fitted to the area the overlays leave free, so no part of it sits under the tower or the pit wall. */
  function placeMap() {
    if (!map) return;
    map.setInset({ l: tower ? 290 : 12, t: 82, r: 344, b: 56 });
    map.fit();
    map.draw();
  }

  $effect(() => {
    tower;
    placeMap();
  });

  function colors() {
    return {
      bg: '#121512',
      infield: '#172119',
      grass: '#1b2a1d',
      runoff: '#2a2c27',
      road: '#33363d',
      edge: 'rgba(240,236,226,.55)',
      kerbRed: '#b8231f',
      kerbWhite: '#e9e4d8',
      wall: '#8d8a82',
      boxes: 'rgba(240,236,226,.35)',
      label: 'rgba(236,231,220,.85)',
      accent: '#f5c518',
      own: getComputedStyle(document.body).getPropertyValue('--t2').trim() || '#e03a3e',
    };
  }

  onMount(() => {
    let raf = 0;
    let poll: ReturnType<typeof setInterval> | undefined;
    let tick: ReturnType<typeof setInterval> | undefined;
    let alive = true;
    const resize = () => {
      map?.resize();
      placeMap();
    };

    (async () => {
      const [view, live] = await Promise.all([query('liveRace', call), query('liveClock', call)]);
      if (!alive) return;
      race = view;
      adopt(live);
      if (!view.found || !canvas) return;
      const track = view.layoutId ? await query('track', { managerId: HUMAN_MANAGER_ID, layoutId: view.layoutId }).catch(() => null) : null;
      if (!alive || !canvas) return;
      const points = track?.found && track.points.length >= 3 ? track.points : fallbackPoints(view.lapLengthM);
      const entries = new Map(
        view.cars.map((car) => {
          const colours = livery(car.teamId);
          return [car.carId, { label: car.shortName, livery: colours, own: car.own }];
        }),
      );
      map = new RaceMap(canvas, new TrackSpline(points), view.lapLengthM, entries, colors());
      placeMap();
      map.onSelect = (id: string | null) => select(id);
      map.onFollow = (on: boolean) => (following = on);
      await fetchFrames(clockNow(live, readAt, performance.now()));
      setTimeout(() => (curtain = false), 350);
      if (live.active && live.paused && !live.finished && live.raceTimeMs === 0) void control('play');
      const loop = () => {
        frame();
        raf = requestAnimationFrame(loop);
      };
      raf = requestAnimationFrame(loop);
      tick = setInterval(() => {
        if (!clock) return;
        now = clockNow(clock, readAt, performance.now());
        const sample = selected ? lastSamples.get(selected) : null;
        selectedKmh = sample ? Math.round(sample.speedMps * 3.6) : null;
      }, 250);
      poll = setInterval(() => {
        void query('liveClock', call).then((view) => {
          if (view.active) adopt(view);
        });
      }, 1_000);
    })().catch(() => {
      curtain = false;
    });

    window.addEventListener('keydown', onKey, true);
    window.addEventListener('resize', resize);
    return () => {
      alive = false;
      cancelAnimationFrame(raf);
      clearInterval(poll);
      clearInterval(tick);
      map?.destroy();
      window.removeEventListener('keydown', onKey, true);
      window.removeEventListener('resize', resize);
    };
  });

  let towerView = $derived(race ? towerAt(race, now) : null);
  $effect(() => {
    gone = new Set((towerView?.rows ?? []).filter((row) => row.out).map((row) => row.carId));
  });
  let flag = $derived(race ? flagAt(race.events, now) : 'green');
  let condition = $derived(race ? conditionAt(race, now) : null);
  let lines = $derived(race ? transcriptAt(race, now, mine).slice(0, 80) : []);
  let cars = $derived(new Map((race?.cars ?? []).map((car) => [car.carId, car])));
  let card = $derived(selected && towerView ? towerView.rows.find((row) => row.carId === selected) ?? null : null);
  let cardCar = $derived(selected ? cars.get(selected) ?? null : null);
  let ownRows = $derived(towerView ? towerView.rows.filter((row) => cars.get(row.carId)?.own) : []);
  let finished = $derived(clock?.finished || flag === 'chequered');
  let podium = $derived(
    towerView && clock?.finished ? towerView.rows.filter((row) => row.finished).slice(0, 6) : [],
  );
  /* Recomputed on every event boundary only; the map moves every animation frame on its own. */
  let eventIndex = $derived(race ? lastIndexAt(race.events, now) : -1);
  let speed = $derived(clock?.speed ?? 0);
  let fastest = $derived(race ? fastestAt(race.events, now) : null);
  let radio = $derived(race ? radioAt(race, now) : []);
  let teamName = $derived(race?.cars.find((car) => car.own)?.teamName ?? '');
</script>

<div class="rm" data-tower={tower ? 'on' : 'off'} data-event={eventIndex}>
  <div class="rm-host" bind:this={mapHost}>
    <canvas class="rm-map" bind:this={canvas}></canvas>
  </div>

  {#if race && !race.found}
    <div class="ov rm-empty">
      <p>{tr.t('live.error.noRace')}</p>
      <button type="button" class="rm-btn primary" onclick={onexit}>{tr.t(backKey)}</button>
    </div>
  {:else if race}
    <div class="ov rm-status">
      <div class="seg gp">
        {#if race.country}<Flag code={race.country} />{/if}
        <span><b>{race.circuitName ?? ''}</b><small>{race.season} · {tr.t('live.ui.race')} {race.round}</small></span>
      </div>
      <div class="seg">
        <span class="meta">{tr.t('live.ui.lap')}</span>
        <b class="num">{towerView?.lap ?? 0}<i>/{race.totalLaps}</i></b>
      </div>
      <div class="seg">
        <span class="meta">{tr.t('live.ui.flag')}</span>
        <b class="flagchip {flag}">{tr.t(`live.flag.${flag}`)}</b>
      </div>
      <div class="seg">
        <span class="meta">{tr.t('live.ui.time')}</span>
        <b class="num">{formatClock(now)}</b>
      </div>
      {#if fastest}
        <div class="seg">
          <span class="meta">{tr.t('live.ui.fastest')}</span>
          <b class="fl">{cars.get(fastest.carId ?? '')?.shortName ?? ''} <small class="num">{fastest.lapTimeMs ? formatLapTime(fastest.lapTimeMs) : ''}</small></b>
        </div>
      {/if}
      {#if condition}
        <div class="seg">
          <span class="meta">{tr.t('live.ui.track')}</span>
          <b class="wx">{tr.t(`live.condition.${condition}`)}{#if race.startAirC}<small>{tr.t('live.ui.air', { temp: race.startAirC })}</small>{/if}</b>
        </div>
      {/if}
    </div>

    <div class="ov rm-pace">
      {#if !finished}
        <div class="seg">
          <span class="meta">{tr.t('live.ui.pace')}</span>
          <div class="speeds">
            <button type="button" class="ctl" aria-pressed={clock?.paused ?? true} title={clock?.paused ? tr.t('live.ui.play') : tr.t('live.ui.pause')} onclick={togglePause}>
              {#if clock?.paused}▶{:else}❚❚{/if}
            </button>
            {#each clock?.speeds ?? [] as option (option)}
              <button type="button" class="ctl" aria-pressed={!clock?.paused && speed === option} onclick={() => control('setSpeed', option)}>{option}×</button>
            {/each}
          </div>
        </div>
      {:else}
        <div class="seg back">
          <button type="button" class="rm-btn primary" onclick={onexit}>{tr.t(backKey)}</button>
        </div>
      {/if}
    </div>

    <button type="button" class="ctl rm-tower-tab" onclick={() => (tower = true)}>{tr.t('live.ui.towerKey')}</button>
    <div class="ov rm-tower">
      <header>
        <b>{tr.t('live.ui.tower')}</b>
        <button type="button" class="ctl ico" title={tr.t('live.ui.towerKey')} onclick={() => (tower = false)}>✕</button>
      </header>
      <ol>
        {#each towerView?.rows ?? [] as row (row.carId)}
          {@const car = cars.get(row.carId)}
          {@const colours = livery(car?.teamId ?? '')}
          <li>
            <button type="button" class:mine={car?.own} class:sel={selected === row.carId} class:out={row.out} onclick={() => select(row.carId)}>
              <b class="pos">{row.out ? '—' : row.pos}</b>
              <i class="sw" style:background={colours.main} style:border-color={colours.accent}></i>
              <span class="nm">{car?.shortName ?? row.carId}</span>
              {#if row.inPit}<span class="tag pit">{tr.t('live.ui.inPit')}</span>{:else if row.finished}<span class="chq"></span>{/if}
              <span class="gap num">
                {#if row.out}{tr.t('live.ui.out')}
                {:else if row.pos === 1}{tr.t('live.ui.leader')}
                {:else if row.lapsDown > 0}{tr.tCount('race.lapsDown', row.lapsDown)}
                {:else}{formatTowerGap(row.gapMs)}{/if}
              </span>
              <span class="ty">{#if row.tyres}<span class="tc c-{tyreLetter(row.tyres)}">{tyreLetter(row.tyres)}</span>{/if}</span>
            </button>
          </li>
        {/each}
      </ol>
    </div>

    <div class="rm-right">
      {#if card && cardCar}
        {@const colours = livery(cardCar.teamId)}
        <div class="ov rm-card">
          <header>
            <span class="bignum" style:background={colours.main} style:color={colours.on} style:box-shadow={`inset 0 0 0 2px ${colours.accent}`}>{card.out ? '—' : card.pos}</span>
            <div class="who">
              <b>{cardCar.driverName}</b>
              <div class="sub"><Flag code={cardCar.nationality} />{cardCar.teamName}</div>
            </div>
            <button type="button" class="ctl ico" title={tr.t('live.ui.close')} onclick={() => select(null)}>✕</button>
          </header>
          <div class="tiles">
            <div><span class="meta">{tr.t('live.ui.speedNow')}</span><b class="num">{selectedKmh !== null && !card.out ? tr.t('live.ui.kmh', { speed: String(selectedKmh) }) : '—'}</b></div>
            <div><span class="meta">{tr.t('live.ui.lastLap')}</span><b class="num">{card.lastLapMs ? formatLapTime(card.lastLapMs) : '—'}</b></div>
            <div><span class="meta">{tr.t('live.ui.bestLap')}</span><b class="num">{card.bestLapMs ? formatLapTime(card.bestLapMs) : '—'}</b></div>
            <div><span class="meta">{tr.t('live.ui.toLeader')}</span><b class="num">{card.pos === 1 ? tr.t('live.ui.leader') : card.lapsDown > 0 ? tr.tCount('race.lapsDown', card.lapsDown) : formatTowerGap(card.gapMs) || '—'}</b></div>
            <div><span class="meta">{tr.t('live.ui.toAhead')}</span><b class="num">{formatTowerGap(card.intervalMs) || '—'}</b></div>
            <div><span class="meta">{tr.t('live.ui.gained')}</span><b class="num" class:up={card.gained > 0} class:down={card.gained < 0}>{card.gained > 0 ? `+${card.gained}` : card.gained}</b></div>
            <div><span class="meta">{tr.t('live.ui.tyres')}</span><b>{card.tyres && tyreFamily(card.tyres) ? tr.t(`live.tyre.${tyreFamily(card.tyres)}`) : card.tyres ?? '—'}</b></div>
            <div><span class="meta">{tr.t('live.ui.tyreAge')}</span><b class="num">{card.tyreLaps}</b></div>
            <div><span class="meta">{tr.t('live.ui.stops')}</span><b class="num">{card.stops}</b></div>
          </div>
          <button type="button" class="rm-btn sm" class:on={following} onclick={() => setFollow(!following)}>
            {following ? tr.t('live.ui.following') : tr.t('live.ui.follow')}
          </button>
        </div>
      {/if}

      <div class="ov rm-pit">
        <header><b>{tr.t('live.ui.pitWall')}</b><small>{teamName}</small></header>
        <p class="who">
          <span class="meta">{tr.t('live.ui.strategist')}</span>
          {race.strategy.strategistName ?? tr.t('live.ui.noStrategist')}
        </p>
        {#each ownRows as row (row.carId)}
          {@const car = cars.get(row.carId)}
          <button type="button" class="own" class:sel={selected === row.carId} onclick={() => select(row.carId)}>
            <span class="pos">{row.out ? '—' : `P${row.pos}`}</span>
            <span class="nm">{car?.shortName}</span>
            {#if row.tyres}<span class="tc c-{tyreLetter(row.tyres)}">{tyreLetter(row.tyres)}</span>{/if}
            <span class="stops">{tr.t('live.ui.stops')}: {row.stops}</span>
            <small>{row.call ? line(row.call) : tr.t('live.ui.noCall')}</small>
          </button>
        {/each}
        <p class="note">{tr.t('live.ui.autoStrategy')}</p>
      </div>

      <div class="ov rm-radio">
        <header><b>{tr.t('live.ui.radioLog')}</b></header>
        <ol>
          {#each radio as event, index (event.seq)}
            <li class:fresh={index === 0} class:call={event.kind === 'call'}>
              <span class="meta">
                {event.kind === 'call' ? `${tr.t('live.ui.radio')} · ${cars.get(event.carId ?? '')?.shortName ?? ''}` : tr.t('live.ui.control')}
                {#if event.lap > 0}· {tr.t('live.ui.lap')} {event.lap}{/if}
              </span>
              <span>{line(event)}</span>
            </li>
          {:else}
            <li class="empty">{tr.t('live.ui.noRadio')}</li>
          {/each}
        </ol>
      </div>
    </div>

    <button type="button" class="ctl rm-log-tab" aria-pressed={transcript} onclick={() => (transcript = !transcript)}>{tr.t('live.ui.transcriptKey')}</button>
    {#if transcript && podium.length === 0}
      <div class="ov rm-log">
        <header>
          <b>{tr.t('live.ui.transcript')}</b>
          <span class="gapsw">
            <button type="button" class="ctl" aria-pressed={!mine} onclick={() => (mine = false)}>{tr.t('live.ui.all')}</button>
            <button type="button" class="ctl" aria-pressed={mine} onclick={() => (mine = true)}>{tr.t('live.ui.mine')}</button>
          </span>
        </header>
        <ol>
          {#each lines as event (event.seq)}
            <li class:own={event.own} class={event.kind}>
              <span class="num">{event.lap > 0 ? `${tr.t('live.ui.lap')} ${event.lap}` : formatClock(event.timeMs)}</span>
              <span>{line(event)}</span>
            </li>
          {:else}
            <li class="empty">{tr.t('live.ui.empty')}</li>
          {/each}
        </ol>
      </div>
    {/if}

    {#if podium.length > 0}
      <div class="ov rm-finish">
        <span class="meta">{tr.t('live.ui.resultCard')}</span>
        <ol>
          {#each podium as row (row.carId)}
            {@const car = cars.get(row.carId)}
            <li class:own={car?.own}>
              <b>{row.finishPos}</b>
              <span>{car?.driverName}<small>{car?.teamName}</small></span>
              <span class="num">{row.finishPos === 1 ? '' : row.lapsDown > 0 ? tr.tCount('race.lapsDown', row.lapsDown) : formatTowerGap(row.gapMs)}</span>
            </li>
          {/each}
        </ol>
        <button type="button" class="rm-btn primary big" onclick={onexit}>{tr.t(backKey)}</button>
      </div>
    {/if}

    {#if race.framesApproximate}
      <p class="rm-approx">{tr.t('live.ui.approximate')}</p>
    {/if}

    <div class="ov rm-zoom">
      <button type="button" class="ctl" title={tr.t('live.ui.zoomIn')} onclick={() => map?.zoomBy(1.25)}>+</button>
      <button type="button" class="ctl" title={tr.t('live.ui.zoomOut')} onclick={() => map?.zoomBy(0.8)}>−</button>
      <button type="button" class="ctl" title={tr.t('live.ui.fit')} onclick={placeMap}>⤢</button>
    </div>
  {/if}

  <div class="curtain" class:open={!curtain} aria-hidden={!curtain}>
    <span>{race?.circuitName ?? tr.t('live.ui.loading')}</span>
  </div>
</div>

<style>
  .rm {
    --ov: rgba(21, 24, 30, 0.86);
    --ov-line: rgba(255, 255, 255, 0.09);
    --ov-ink: #ece7dc;
    --ov-ink2: #a9a59c;
    --ov-ink3: #7d7a73;
    --gold: #f5c518;
    position: fixed;
    inset: 0;
    z-index: 30;
    overflow: hidden;
    background: #15181e;
    color: var(--ov-ink);
    font: 500 14px/1.3 var(--ui);
    user-select: none;
  }
  .rm-host {
    position: absolute;
    inset: 0;
  }
  .rm-map {
    position: absolute;
    inset: 0;
    display: block;
    cursor: grab;
    touch-action: none;
  }
  .ov {
    position: absolute;
    box-sizing: border-box;
    background: var(--ov);
    border: 1px solid var(--ov-line);
    border-radius: 14px;
    backdrop-filter: blur(10px) saturate(1.2);
    box-shadow: 0 18px 40px -22px rgba(0, 0, 0, 0.8);
  }
  .num {
    font-variant-numeric: tabular-nums;
  }
  .meta {
    display: block;
    font: 600 10.5px/1 var(--ui);
    letter-spacing: 0.09em;
    text-transform: uppercase;
    color: var(--ov-ink3);
  }
  .ctl {
    all: unset;
    box-sizing: border-box;
    cursor: pointer;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    gap: 6px;
    height: 30px;
    min-width: 36px;
    padding: 0 9px;
    border-radius: 9px;
    font: 700 13px var(--ui);
    color: var(--ov-ink2);
    transition: background 0.2s, color 0.2s;
  }
  .ctl:hover {
    background: rgba(255, 255, 255, 0.07);
    color: var(--ov-ink);
  }
  .ctl[aria-pressed='true'] {
    background: var(--ov-ink);
    color: #15181e;
  }
  .ctl:focus-visible,
  .rm-btn:focus-visible {
    outline: 2px solid var(--gold);
    outline-offset: 2px;
  }
  .rm-btn {
    all: unset;
    box-sizing: border-box;
    cursor: pointer;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    height: 38px;
    padding: 0 16px;
    border-radius: 10px;
    border: 1px solid rgba(255, 255, 255, 0.18);
    font: 700 14px var(--ui);
    color: var(--ov-ink);
    box-shadow: 0 3px 0 rgba(0, 0, 0, 0.45);
  }
  .rm-btn.primary {
    background: var(--t2, var(--gold));
    color: #15181e;
    border-color: #15181e;
  }
  .rm-btn.on {
    background: var(--gold);
    color: #15181e;
  }
  .rm-btn.sm {
    height: 32px;
    padding: 0 12px;
    font-size: 13px;
    width: 100%;
  }
  .rm-btn.big {
    height: 48px;
    font-size: 16px;
    width: 100%;
  }

  .rm-status,
  .rm-pace {
    top: 12px;
    display: flex;
    align-items: stretch;
    padding: 0 6px;
    height: 58px;
    white-space: nowrap;
  }
  .rm-status {
    left: 12px;
  }
  .rm-pace {
    right: 12px;
  }
  .seg {
    display: flex;
    flex-direction: column;
    justify-content: center;
    gap: 5px;
    padding: 0 12px;
  }
  .seg + .seg {
    border-left: 1px solid var(--ov-line);
  }
  .seg b {
    font: 800 20px/1 var(--display);
  }
  .seg b i {
    font-style: normal;
    color: var(--ov-ink3);
    font-size: 15px;
  }
  .gp {
    flex-direction: row;
    align-items: center;
    gap: 10px;
  }
  .gp b {
    display: block;
    font: 800 18px/1 var(--display);
    text-transform: uppercase;
  }
  .gp small {
    display: block;
    font: 600 11.5px/1.2 var(--ui);
    color: var(--ov-ink2);
    margin-top: 3px;
  }
  .wx {
    font: 700 14px/1 var(--ui) !important;
  }
  .wx small {
    font: 600 12px var(--ui);
    color: var(--ov-ink2);
    margin-left: 6px;
  }
  .speeds {
    display: flex;
    gap: 2px;
  }
  .flagchip {
    display: inline-flex;
    align-items: center;
    gap: 7px;
    font: 800 15px/1 var(--display) !important;
    text-transform: uppercase;
  }
  .flagchip::before {
    content: '';
    width: 14px;
    height: 10px;
    border-radius: 2px;
    background: currentColor;
  }
  .flagchip.green {
    color: #4cc46a;
  }
  .flagchip.sc,
  .flagchip.vsc {
    color: var(--gold);
  }
  .flagchip.red {
    color: #ff5a4a;
  }
  .flagchip.chequered::before {
    background: repeating-conic-gradient(#f4efe4 0 25%, #111 0 50%) 0 0/7px 5px;
  }

  .rm-tower {
    top: 82px;
    left: 12px;
    width: 266px;
    max-height: calc(100% - 94px);
    overflow: auto;
    padding: 8px 4px 4px;
    transition: transform 0.42s var(--ease), opacity 0.3s;
  }
  .rm-tower ol {
    list-style: none;
    margin: 4px 0 0;
    padding: 0;
  }
  .rm-tower li button {
    all: unset;
    box-sizing: border-box;
    cursor: pointer;
    display: flex;
    align-items: center;
    gap: 7px;
    width: 100%;
    height: 27px;
    padding: 0 6px;
    border-radius: 6px;
  }
  .rm-tower li button:hover {
    background: rgba(255, 255, 255, 0.05);
  }
  .rm-tower li button.mine {
    background: linear-gradient(90deg, color-mix(in oklab, var(--t1, #c4161c) 60%, transparent), transparent);
  }
  .rm-tower li button.sel {
    background: rgba(245, 197, 24, 0.16);
    box-shadow: inset 3px 0 0 var(--gold);
  }
  .rm-tower li button.out {
    color: var(--ov-ink3);
  }
  .rm-tower li button:focus-visible {
    outline: 2px solid var(--gold);
  }
  .rm-tower .nm {
    flex: 1;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }
  .rm-tower .gap {
    font-size: 12px;
    color: var(--ov-ink2);
    text-align: right;
  }
  .rm-tower .ty {
    width: 17px;
    display: inline-flex;
  }
  .rm[data-tower='off'] .rm-tower {
    transform: translateX(calc(-100% - 24px));
    opacity: 0;
    pointer-events: none;
  }
  .rm-tower > header,
  .rm-log > header,
  .rm-pit > header {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 0 4px 8px 8px;
    border-bottom: 1px solid var(--ov-line);
  }
  .rm-tower > header > b,
  .rm-log > header > b,
  .rm-pit > header > b {
    font: 800 17px/1 var(--display);
    text-transform: uppercase;
    margin-right: auto;
  }
  .rm-pit > header small {
    color: var(--ov-ink2);
    font-size: 12px;
  }
  .pos {
    font: 800 15px/1 var(--display);
    width: 22px;
    text-align: right;
  }
  .nm {
    font-weight: 700;
  }
  .sw {
    display: inline-block;
    width: 4px;
    height: 14px;
    margin-right: 7px;
    vertical-align: -2px;
    border-radius: 2px;
    border-right: 2px solid;
  }
  .tag {
    display: inline-block;
    font: 700 10px/1 var(--ui);
    letter-spacing: 0.07em;
    text-transform: uppercase;
    padding: 3px 5px;
    border-radius: 4px;
  }
  .tag.pit {
    background: var(--gold);
    color: #15181e;
  }
  .chq {
    display: inline-block;
    width: 14px;
    height: 10px;
    border-radius: 2px;
    background: repeating-conic-gradient(#f4efe4 0 25%, #111 0 50%) 0 0/7px 5px;
  }
  .tc {
    display: inline-grid;
    place-items: center;
    min-width: 17px;
    height: 17px;
    border-radius: 9px;
    font: 800 10px/1 var(--ui);
    box-sizing: border-box;
    border: 2.5px solid #ece7dc;
    color: #ece7dc;
    background: #15181e;
  }
  .tc.c-S {
    border-color: #e8473b;
    color: #e8473b;
  }
  .tc.c-M {
    border-color: #f5c518;
    color: #f5c518;
  }
  .tc.c-W {
    border-color: #3b8be8;
    color: #3b8be8;
  }
  .tc.c-Q {
    border-color: #c46cf0;
    color: #c46cf0;
  }
  .rm-tower-tab {
    position: absolute;
    top: 82px;
    left: 12px;
    background: var(--ov);
    border: 1px solid var(--ov-line);
    opacity: 0;
    pointer-events: none;
  }
  .rm[data-tower='off'] .rm-tower-tab {
    opacity: 1;
    pointer-events: auto;
  }

  .rm-right {
    position: absolute;
    top: 82px;
    right: 12px;
    bottom: 48px;
    width: 320px;
    display: flex;
    flex-direction: column;
    gap: 10px;
    pointer-events: none;
  }
  .rm-right > .ov {
    position: relative;
    pointer-events: auto;
  }
  .rm-right > .rm-pit {
    margin-top: auto;
  }
  .rm-card .tiles {
    margin: 12px 0 10px;
  }
  .rm-card {
    padding: 14px;
  }
  .rm-card > header {
    display: flex;
    align-items: center;
    gap: 12px;
  }
  .bignum {
    display: grid;
    place-items: center;
    width: 46px;
    height: 46px;
    border-radius: 10px;
    font: 900 24px/1 var(--display);
    flex: none;
  }
  .who {
    flex: 1;
    min-width: 0;
  }
  .rm-card .who b {
    display: block;
    font: 800 19px/1.05 var(--display);
    text-transform: uppercase;
  }
  .sub {
    display: flex;
    align-items: center;
    gap: 7px;
    margin-top: 5px;
    font: 600 13px var(--ui);
    color: var(--ov-ink2);
  }
  .tiles {
    display: grid;
    grid-template-columns: repeat(3, minmax(0, 1fr));
    gap: 1px;
    margin: 14px 0 12px;
    background: var(--ov-line);
    border: 1px solid var(--ov-line);
    border-radius: 10px;
    overflow: hidden;
  }
  .tiles > div {
    background: #1b1f26;
    padding: 9px 10px;
    display: flex;
    flex-direction: column;
    gap: 6px;
  }
  .tiles b {
    font: 700 14px/1 var(--ui);
  }
  .rm-pit {
    padding: 10px 8px 10px;
  }
  .rm-pit p.who {
    margin: 10px 8px 6px;
    font-weight: 700;
  }
  .rm-pit p.who .meta {
    margin-bottom: 4px;
  }
  .own {
    all: unset;
    box-sizing: border-box;
    cursor: pointer;
    display: grid;
    grid-template-columns: 34px 1fr auto auto;
    align-items: center;
    gap: 2px 8px;
    width: 100%;
    padding: 7px 8px;
    border-radius: 9px;
  }
  .own:hover,
  .own.sel {
    background: rgba(255, 255, 255, 0.06);
  }
  .own .stops {
    font-size: 12px;
    color: var(--ov-ink2);
  }
  .own small {
    grid-column: 1 / -1;
    color: var(--gold);
    font-size: 12px;
  }
  .note {
    margin: 8px 8px 0;
    font-size: 12px;
    color: var(--ov-ink3);
  }

  .rm-log-tab {
    position: absolute;
    left: 12px;
    bottom: 12px;
    background: var(--ov);
    border: 1px solid var(--ov-line);
  }
  .rm-log {
    left: 50%;
    bottom: 54px;
    transform: translateX(-50%);
    width: min(560px, calc(100% - 720px));
    min-width: 360px;
    max-height: 38%;
    display: flex;
    flex-direction: column;
    padding: 8px 6px 6px;
  }
  .rm-log ol {
    list-style: none;
    margin: 0;
    padding: 4px 4px 0;
    overflow: auto;
  }
  .rm-log li {
    display: grid;
    grid-template-columns: 96px 1fr;
    gap: 8px;
    padding: 5px 4px;
    border-top: 1px solid rgba(255, 255, 255, 0.04);
    font-size: 13px;
  }
  .rm-log li .num {
    color: var(--ov-ink3);
  }
  .rm-log li.own {
    color: #fff;
    background: color-mix(in oklab, var(--t1, #c4161c) 22%, transparent);
  }
  .rm-log li.call {
    color: var(--gold);
  }
  .rm-log li.empty {
    display: block;
    color: var(--ov-ink3);
  }
  .gapsw {
    display: flex;
    gap: 2px;
  }

  .rm-radio {
    padding: 10px 8px 8px;
  }
  .rm-radio > header {
    padding: 0 4px 8px 8px;
    border-bottom: 1px solid var(--ov-line);
  }
  .rm-radio > header b {
    font: 800 17px/1 var(--display);
    text-transform: uppercase;
  }
  .rm-radio ol {
    list-style: none;
    margin: 0;
    padding: 4px 0 0;
  }
  .rm-radio li {
    display: flex;
    flex-direction: column;
    gap: 3px;
    padding: 6px 8px;
    border-left: 3px solid var(--ov-line);
    margin-top: 4px;
    font-size: 12.5px;
    color: var(--ov-ink2);
  }
  .rm-radio li.call {
    border-left-color: var(--gold);
  }
  .rm-radio li.fresh {
    color: var(--ov-ink);
    font-weight: 700;
    animation: pop 0.35s var(--ease);
  }
  .rm-radio li.empty {
    border: 0;
    color: var(--ov-ink3);
  }
  @keyframes pop {
    from {
      opacity: 0;
      transform: translateY(-6px);
    }
  }
  .fl small {
    font: 600 12px var(--ui);
    color: var(--ov-ink2);
  }
  .fl {
    font: 700 14px/1 var(--ui) !important;
  }
  .up {
    color: #4cc46a;
  }
  .down {
    color: #ff6b57;
  }
  .rm-finish {
    left: 50%;
    top: 50%;
    transform: translate(-50%, -50%);
    width: 380px;
    padding: 18px;
  }
  .rm-finish ol {
    list-style: none;
    margin: 10px 0 16px;
    padding: 0;
  }
  .rm-finish li {
    display: grid;
    grid-template-columns: 28px 1fr auto;
    align-items: center;
    gap: 10px;
    padding: 7px 4px;
    border-top: 1px solid var(--ov-line);
  }
  .rm-finish li.own {
    background: color-mix(in oklab, var(--t1, #c4161c) 30%, transparent);
  }
  .rm-finish li b {
    font: 800 20px/1 var(--display);
  }
  .rm-finish li small {
    display: block;
    color: var(--ov-ink2);
    font-size: 12px;
  }
  .rm-empty {
    left: 50%;
    top: 50%;
    transform: translate(-50%, -50%);
    padding: 20px;
    text-align: center;
  }
  .rm-approx {
    position: absolute;
    right: 12px;
    bottom: 12px;
    margin: 0;
    max-width: 320px;
    font-size: 11.5px;
    color: var(--ov-ink3);
    text-align: right;
  }
  .rm-zoom {
    left: 50%;
    bottom: 12px;
    transform: translateX(-50%);
    display: flex;
    gap: 2px;
    padding: 3px;
  }

  .curtain {
    position: absolute;
    inset: 0;
    display: grid;
    place-items: center;
    background: linear-gradient(115deg, var(--t1, #c4161c) 0 62%, var(--t2, #f5c518) 62% 66%, var(--t1, #c4161c) 66%);
    color: var(--on1, #fff);
    font: 900 clamp(32px, 6vw, 72px) / 1 var(--display);
    text-transform: uppercase;
    transition: transform 0.7s var(--sweep), opacity 0.2s 0.6s;
    z-index: 5;
  }
  .curtain.open {
    transform: translateY(-101%);
    opacity: 0;
    pointer-events: none;
  }
</style>
