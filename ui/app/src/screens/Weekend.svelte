<script lang="ts" module>
  /* What the player has already seen of each weekend, so leaving the screen and coming back does not replay qualifying. */
  const seenQualifying = new Set<string>();
</script>

<script lang="ts">
  import type { BridgeCommandName } from '../lib/api/types.generated';
  import PersonName from '../lib/components/PersonName.svelte';
  import Steps from '../lib/components/Steps.svelte';
  import Tabs from '../lib/components/Tabs.svelte';
  import TrackMap from '../lib/components/TrackMap.svelte';
  import { tyreFamily, tyreLetter } from '../lib/live-race.mjs';
  import { livery } from '../lib/livery.mjs';
  import { formatLapTime } from '../lib/race.mjs';
  import type { WeekendData } from '../lib/screens';
  import { icon, ICON, type Tr } from '../lib/ui';
  import { changed, formOf, nextStop, planCall, revealed, STEPS } from '../lib/weekend.mjs';

  let {
    data,
    tr,
    busy,
    act,
    onWatch,
    onSimulate,
  }: {
    data: WeekendData;
    tr: Tr;
    busy: boolean;
    act: (name: BridgeCommandName, args: Record<string, unknown>) => Promise<boolean>;
    onWatch: () => void;
    onSimulate: () => void;
  } = $props();

  /** ESTIMATE of a watching pace: one more car on the timing screen every this many milliseconds. */
  const REVEAL_MS = 650;

  let weekend = $derived(data.weekend);
  let key = $derived(`${weekend.season}-${weekend.round}`);
  let step = $state('practice');
  let reachable = $state(0);
  let shown = $state(0);
  let timer: ReturnType<typeof setInterval> | null = null;
  let qualifyingMode = $state<'choose' | 'watch' | 'done'>('choose');

  $effect(() => {
    key;
    const seen = seenQualifying.has(key);
    qualifyingMode = seen ? 'done' : 'choose';
    shown = seen ? weekend.grid.length : 0;
    reachable = seen ? STEPS.length - 1 : 0;
    step = weekend.watched ? 'race' : seen ? 'strategy' : 'practice';
    return stopReveal;
  });

  function go(value: string) {
    step = value;
    reachable = Math.max(reachable, STEPS.indexOf(value));
  }

  function next() {
    const at = STEPS.indexOf(step);
    if (step === 'qualifying' && qualifyingMode !== 'done') return;
    if (at < STEPS.length - 1) go(STEPS[at + 1]);
  }

  function stopReveal() {
    if (timer) clearInterval(timer);
    timer = null;
  }

  function finishQualifying() {
    stopReveal();
    shown = weekend.grid.length;
    qualifyingMode = 'done';
    seenQualifying.add(key);
    reachable = Math.max(reachable, STEPS.indexOf('strategy'));
  }

  function watchQualifying() {
    qualifyingMode = 'watch';
    shown = 0;
    stopReveal();
    timer = setInterval(() => {
      shown += 1;
      if (shown >= weekend.grid.length) finishQualifying();
    }, REVEAL_MS);
  }

  let gridShown = $derived(revealed(weekend.grid, shown));
  let lastIn = $derived(qualifyingMode === 'watch' && shown > 0 ? weekend.grid.length - shown + 1 : null);
  let ownPractice = $derived(weekend.practice.filter((line) => line.own));

  function tyreName(id: string) {
    const family = tyreFamily(id);
    return family ? tr.t(`live.tyre.${family}`) : id;
  }

  /* One form per own car, from the plan the world holds; rebuilt when the world's plans change. */
  let forms = $state<Record<string, ReturnType<typeof formOf>>>({});
  $effect(() => {
    const next: Record<string, ReturnType<typeof formOf>> = {};
    for (const plan of weekend.plans) next[plan.carId] = formOf(plan);
    forms = next;
  });

  const PACES = ['auto', 'conserve', 'save', 'standard', 'push', 'qualifying'];
  const PACE_KEY: Record<string, string> = {
    auto: 'live.ui.paceAuto',
    conserve: 'live.ui.paceConserve',
    save: 'live.ui.paceSave',
    standard: 'live.ui.paceStandard',
    push: 'live.ui.pacePush',
    qualifying: 'live.ui.paceQualifying',
  };
  const ENGINE_KEY: Record<string, string> = { lean: 'live.ui.engineLean', standard: 'live.ui.engineStandard', full: 'live.ui.engineFull' };

  let paceItems = $derived(PACES.map((value) => ({ value, label: tr.t(PACE_KEY[value]) })));
  let engineItems = $derived(['lean', 'standard', 'full'].map((value) => ({ value, label: tr.t(ENGINE_KEY[value]) })));
  let tyreItems = $derived(weekend.dryTyres.map((value) => ({ value, label: tyreName(value) })));
  let stopTyreItems = $derived([...weekend.dryTyres, weekend.wetTyres].map((value) => ({ value, label: tyreName(value) })));
  let stopModeItems = $derived([
    { value: 'auto', label: tr.t('weekend.stops.strategist') },
    { value: 'own', label: tr.t('weekend.stops.own'), disabled: !weekend.tyreChange },
  ]);

  function addStop(carId: string) {
    const form = forms[carId];
    if (!form || form.stops.length >= weekend.maxStops) return;
    form.stops = [...form.stops, nextStop(form, weekend.totalLaps, weekend.dryTyres)];
  }

  function removeStop(carId: string, at: number) {
    const form = forms[carId];
    if (form) form.stops = form.stops.filter((_: unknown, index: number) => index !== at);
  }

  async function confirm(carId: string) {
    const form = forms[carId];
    if (form) await act('setRacePlan', planCall(form, weekend.season, weekend.round, carId));
  }

  let steps = $derived(STEPS.map((value) => ({ value, label: tr.t(`weekend.step.${value}`) })));
</script>

{#snippet teamTag(id: string, name: string)}
  <span class="meta team"><i class="tdot" style={`background:${livery(id).main}`}></i>{name}</span>
{/snippet}

{#snippet chip(id: string)}
  <span class="tc c-{tyreLetter(id)}" title={tyreName(id)}>{tyreLetter(id)}</span>
{/snippet}

{#if weekend.found}
  <div class="weekend">
    <section class="panel wk-head">
      <span class="rno big">{weekend.round}</span>
      <div class="wk-title">
        <h1 class="screen">{weekend.circuitName ?? weekend.layoutId}</h1>
        <div class="fields mid">
          <div class="fld"><span class="meta">{tr.t('race.laps')}</span><span class="v num">{weekend.totalLaps}</span></div>
          <div class="fld"><span class="meta">{tr.t('weekend.tyres')}</span><span class="v">{#each weekend.dryTyres as id (id)}{@render chip(id)}{/each}</span></div>
          {#if weekend.requiredDryCompounds > 1}
            <div class="fld"><span class="meta">{tr.t('weekend.mix')}</span><span class="v num">{weekend.requiredDryCompounds}</span></div>
          {/if}
          <div class="fld"><span class="meta">{tr.t('weekend.refuelling')}</span><span class="v">{tr.t(weekend.refuelling ? 'weekend.yes' : 'weekend.no')}</span></div>
        </div>
      </div>
      {#if data.track?.found && data.track.points.length > 0}
        <div class="wk-map"><TrackMap points={data.track.points} label={weekend.circuitName ?? ''} /></div>
      {/if}
    </section>

    <Steps items={steps} current={step} {reachable} onGo={go} />

    {#if step === 'practice'}
      <div class="wk-grid">
        <section class="panel tbl">
          <header><h2>{tr.t('weekend.practice.times')}</h2></header>
          <div class="tbl-scroll">
            <table class="table tight">
              <thead>
                <tr>
                  <th class="c">{tr.t('shell.col.position')}</th>
                  <th>{tr.t('shell.col.driver')}</th>
                  <th class="r">{tr.t('weekend.practice.best')}</th>
                  <th class="r">{tr.t('weekend.gap')}</th>
                  <th class="r">{tr.t('weekend.practice.longRun')}</th>
                </tr>
              </thead>
              <tbody>
                {#each weekend.practice as line (line.carId)}
                  <tr class:mine={line.own}>
                    <td class="c num">{line.position}</td>
                    <td><PersonName name={line.driverName} id={line.carId} nationality="" />{@render teamTag(line.teamId, line.teamName)}</td>
                    <td class="r num">{formatLapTime(line.bestLapMs)}</td>
                    <td class="r num">{line.gapMs > 0 ? `+${(line.gapMs / 1000).toFixed(3)}` : ''}</td>
                    <td class="r num">{formatLapTime(line.longRunMs)}</td>
                  </tr>
                {/each}
              </tbody>
            </table>
          </div>
        </section>
        <div class="col">
          {#each ownPractice as line (line.carId)}
            <section class="panel">
              <header><h2>{line.driverName}</h2><span class="meta num">P{line.position}</span></header>
              <div class="body">
                <div class="tyre-reads">
                  {#each line.tyres as read (read.tyres)}
                    <div class="read">
                      {@render chip(read.tyres)}
                      <span class="meta">{tyreFamily(read.tyres) ? tyreName(read.tyres) : ''}</span>
                      <b class="num">{tr.t('weekend.practice.lapsRange', { low: String(read.lowLaps), high: String(read.highLaps) })}</b>
                    </div>
                  {/each}
                </div>
              </div>
            </section>
          {/each}
          <div class="wk-next"><button class="btn primary" type="button" onclick={next}><span>{tr.t('weekend.step.qualifying')}</span>{@html icon(ICON.arrow, 17)}</button></div>
        </div>
      </div>
    {:else if step === 'qualifying'}
      {#if qualifyingMode === 'choose'}
        <section class="panel wk-choice">
          <button class="btn big" type="button" onclick={watchQualifying}>{@html icon(ICON.play, 20)}<span>{tr.t('weekend.qualifying.watch')}</span></button>
          <button class="btn big" type="button" onclick={finishQualifying}>{@html icon(ICON.arrow, 20)}<span>{tr.t('weekend.qualifying.simulate')}</span></button>
        </section>
      {:else}
        <div class="wk-grid">
          <section class="panel tbl">
            <header>
              <h2>{tr.t('race.startingGrid')}</h2>
              {#if qualifyingMode === 'watch'}<button class="btn sm" type="button" onclick={finishQualifying}>{tr.t('weekend.qualifying.skip')}</button>{/if}
            </header>
            <div class="tbl-scroll">
              <table class="table tight">
                <thead>
                  <tr>
                    <th class="c">{tr.t('shell.col.position')}</th>
                    <th>{tr.t('shell.col.driver')}</th>
                    <th class="r">{tr.t('race.time')}</th>
                    <th class="r">{tr.t('weekend.gap')}</th>
                  </tr>
                </thead>
                <tbody>
                  {#each gridShown as row (row.carId)}
                    <tr class:mine={row.own} class:fresh={lastIn !== null && row.position === lastIn}>
                      <td class="c num">{row.position === 0 ? tr.t('weekend.pitLane') : row.position}</td>
                      <td><PersonName name={row.driverName} id={row.carId} nationality="" />{@render teamTag(row.teamId, row.teamName)}</td>
                      <td class="r num">{formatLapTime(row.bestLapMs)}</td>
                      <td class="r num">{row.gapMs !== null && row.gapMs > 0 ? `+${(row.gapMs / 1000).toFixed(3)}` : ''}</td>
                    </tr>
                  {/each}
                </tbody>
              </table>
            </div>
          </section>
          <div class="col">
            {#if qualifyingMode === 'done' && weekend.notQualified.length > 0}
              <section class="panel">
                <header><h2>{tr.t('weekend.qualifying.out')}</h2></header>
                <div class="body out-list">
                  {#each weekend.notQualified as out (out.carId)}
                    <div class="out" class:mine={out.own}><b>{out.driverName}</b><span class="meta">{tr.t(`weekend.dnq.${out.reason}`)}</span></div>
                  {/each}
                </div>
              </section>
            {/if}
            {#if qualifyingMode === 'done'}
              <div class="wk-next"><button class="btn primary" type="button" onclick={next}><span>{tr.t('weekend.step.strategy')}</span>{@html icon(ICON.arrow, 17)}</button></div>
            {/if}
          </div>
        </div>
      {/if}
    {:else if step === 'strategy'}
      <div class="plans">
        {#each weekend.plans as plan (plan.carId)}
          {@const form = forms[plan.carId]}
          {#if form}
            <section class="panel plan">
              <header>
                <h2>{plan.driverName}</h2>
                <span class="meta num">{plan.grid > 0 ? `P${plan.grid}` : plan.grid === 0 ? tr.t('weekend.pitLane') : ''}</span>
              </header>
              <fieldset class="body" disabled={!weekend.canPlan}>
                <div class="row"><span class="meta">{tr.t('weekend.plan.start')}</span><Tabs group={`start-${plan.carId}`} items={tyreItems} bind:value={form.startTyres} /></div>
                <div class="row"><span class="meta">{tr.t('live.ui.pace')}</span><Tabs group={`pace-${plan.carId}`} items={paceItems} bind:value={form.pace} /></div>
                <div class="row"><span class="meta">{tr.t('live.ui.engine')}</span><Tabs group={`engine-${plan.carId}`} items={engineItems} bind:value={form.engine} /></div>
                <div class="row"><span class="meta">{tr.t('weekend.plan.stops')}</span><Tabs group={`stops-${plan.carId}`} items={stopModeItems} bind:value={form.stopsMode} /></div>
                {#if form.stopsMode === 'own'}
                  <ol class="stops">
                    {#each form.stops as stop, at (at)}
                      <li>
                        <span class="meta">{tr.t('weekend.plan.lap')}</span>
                        <input class="num" type="number" min="1" max={weekend.totalLaps - 1} bind:value={stop.lap} />
                        <Tabs group={`stop-${plan.carId}-${at}`} items={stopTyreItems} bind:value={stop.tyres} />
                        <button class="btn sm" type="button" onclick={() => removeStop(plan.carId, at)}>{tr.t('weekend.plan.remove')}</button>
                      </li>
                    {/each}
                  </ol>
                  {#if form.stops.length < weekend.maxStops}
                    <div><button class="btn sm" type="button" onclick={() => addStop(plan.carId)}>{@html icon(ICON.plus, 15)}<span>{tr.t('weekend.plan.add')}</span></button></div>
                  {/if}
                {/if}
              </fieldset>
              <footer>
                {#if weekend.canPlan}
                  <button class="btn primary" type="button" disabled={busy || !changed(form, plan)} onclick={() => confirm(plan.carId)}>{@html icon(ICON.check, 17)}<span>{tr.t('weekend.plan.confirm')}</span></button>
                {:else if weekend.planLocked}
                  <span class="meta">{tr.t(weekend.planLocked)}</span>
                {/if}
              </footer>
            </section>
          {/if}
        {/each}
      </div>
      <div class="wk-next"><button class="btn primary" type="button" onclick={next}><span>{tr.t('weekend.step.race')}</span>{@html icon(ICON.arrow, 17)}</button></div>
    {:else}
      <section class="panel wk-choice">
        <button class="btn big primary" type="button" disabled={busy} onclick={onWatch}>{@html icon(ICON.play, 20)}<span>{tr.t('live.ui.watch')}</span></button>
        <button class="btn big" type="button" disabled={busy || weekend.watched} onclick={onSimulate}>{@html icon(ICON.arrow, 20)}<span>{tr.t('weekend.race.simulate')}</span></button>
      </section>
    {/if}
  </div>
{:else}
  <section class="panel"><div class="body"><p class="meta">{tr.t('race.weekend.noRace')}</p></div></section>
{/if}

<style>
  .weekend {
    display: grid;
    gap: 18px;
    max-width: 1280px;
  }
  .wk-head {
    display: grid;
    grid-template-columns: auto 1fr auto;
    align-items: center;
    gap: 22px;
    padding: 18px 22px;
  }
  .wk-title {
    display: grid;
    gap: 10px;
    min-width: 0;
  }
  .wk-map {
    width: 220px;
    height: 110px;
  }
  .wk-grid {
    display: grid;
    grid-template-columns: minmax(0, 720px) minmax(260px, 380px);
    gap: 18px;
    align-items: start;
  }
  .col {
    display: grid;
    gap: 18px;
  }
  .tyre-reads {
    display: grid;
    gap: 10px;
  }
  .read {
    display: grid;
    grid-template-columns: auto 1fr auto;
    align-items: center;
    gap: 10px;
  }
  .read b {
    font-size: 18px;
  }
  .wk-choice {
    display: flex;
    gap: 18px;
    padding: 24px;
    justify-self: start;
  }
  .btn.big {
    min-width: 260px;
    min-height: 64px;
    font-size: 18px;
    justify-content: center;
  }
  .wk-next {
    display: flex;
    justify-content: flex-end;
  }
  .plans {
    display: grid;
    grid-template-columns: repeat(auto-fill, minmax(0, 780px));
    gap: 18px;
  }
  .plan .body {
    display: grid;
    gap: 12px;
    border: 0;
    margin: 0;
    min-width: 0;
  }
  .plan .row {
    display: grid;
    gap: 6px;
    justify-items: start;
  }
  .stops {
    list-style: none;
    margin: 0;
    padding: 0;
    display: grid;
    gap: 8px;
  }
  .stops li {
    display: flex;
    align-items: center;
    gap: 10px;
    flex-wrap: wrap;
  }
  .stops input {
    width: 72px;
    font: 600 16px/1 var(--ui);
    padding: 8px;
    border: 1.5px solid var(--rule);
    border-radius: 8px;
    background: var(--card);
    color: var(--ink);
  }
  .out-list {
    display: grid;
    gap: 8px;
  }
  .out {
    display: flex;
    justify-content: space-between;
    gap: 12px;
  }
  tr.fresh td {
    animation: arrive 0.6s ease-out;
  }
  @keyframes arrive {
    from {
      background: color-mix(in oklab, var(--t1) 30%, transparent);
    }
  }
  .tc {
    display: inline-grid;
    place-items: center;
    min-width: 20px;
    height: 20px;
    border-radius: 10px;
    font: 800 11px/1 var(--ui);
    box-sizing: border-box;
    border: 2.5px solid #ece7dc;
    color: #ece7dc;
    background: #15181e;
    margin-right: 4px;
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
  @media (max-width: 1100px) {
    .wk-grid {
      grid-template-columns: 1fr;
    }
    .wk-head {
      grid-template-columns: auto 1fr;
    }
    .wk-map {
      display: none;
    }
  }
</style>
