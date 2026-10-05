<script lang="ts">
  import { untrack } from 'svelte';
  import type { BridgeCommandName, CarBandView, OwnDevelopmentView, OwnProjectView } from '../lib/api/types.generated';
  import Confirmation from '../lib/components/Confirmation.svelte';
  import Status from '../lib/components/Status.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { formatMoney } from '../lib/money.mjs';
  import type { CarData } from '../lib/screens';
  import { icon, ICON, type Tr } from '../lib/ui';

  let {
    data,
    tr,
    teamId,
    busy,
    act,
  }: {
    data: CarData;
    tr: Tr;
    teamId: string;
    busy: boolean;
    act: (name: BridgeCommandName, args: Record<string, unknown>) => Promise<boolean>;
  } = $props();

  /* Priorities run 0 to 10 and the three shares must add up to 100; the bridge refuses anything else and says why. */
  const MAX_PRIORITY = 10;
  const STEP = 5;
  const STREAMS = ['current', 'account', 'nextYear'] as const;
  const AREAS = ['aero', 'chassis', 'reliability', 'tyres'] as const;
  const PERFORMANCE = ['power', 'downforce', 'mechanicalGrip', 'braking', 'reliability'] as const;

  let plan = $derived<OwnDevelopmentView | null>(data.development.own[0] ?? null);
  let cars = $derived(data.cars.own);

  type Draft = { current: number; account: number; nextYear: number; aero: number; chassis: number; reliability: number; tyres: number };
  const draftOf = (view: OwnDevelopmentView | null): Draft => ({
    current: view?.currentPercent ?? 0,
    account: view?.accountPercent ?? 0,
    nextYear: view?.nextYearPercent ?? 0,
    aero: view?.aeroPriority ?? 0,
    chassis: view?.chassisPriority ?? 0,
    reliability: view?.reliabilityPriority ?? 0,
    tyres: view?.tyresPriority ?? 0,
  });

  let draft = $state<Draft>(untrack(() => draftOf(plan)));
  let appliedKey = $state(untrack(() => JSON.stringify(draftOf(plan))));
  let asking = $state<'split' | { commit: string } | null>(null);

  /* After a confirmed change (or a reload) the bridge's numbers are the truth again. */
  $effect(() => {
    const key = JSON.stringify(draftOf(plan));
    if (key !== appliedKey) {
      appliedKey = key;
      draft = draftOf(plan);
    }
  });

  let total = $derived(draft.current + draft.account + draft.nextYear);
  let dirty = $derived(JSON.stringify(draft) !== appliedKey);
  let valid = $derived(total === 100);

  function move(stream: (typeof STREAMS)[number], delta: number) {
    draft[stream] = Math.min(100, Math.max(0, draft[stream] + delta));
  }

  function priority(area: (typeof AREAS)[number], value: number) {
    draft[area] = value;
  }

  async function saveSplit() {
    asking = null;
    await act('setDevelopmentSplit', {
      organizationId: teamId,
      currentPercent: draft.current,
      accountPercent: draft.account,
      nextYearPercent: draft.nextYear,
      aeroPriority: draft.aero,
      chassisPriority: draft.chassis,
      reliabilityPriority: draft.reliability,
      tyresPriority: draft.tyres,
    });
  }

  async function commit(projectId: string) {
    asking = null;
    await act('commitConcept', { organizationId: teamId, projectId });
  }

  const priorityOf = (area: string | null) => (area === 'Aero' ? 'aero' : area === 'Chassis' ? 'chassis' : area === 'Reliability' ? 'reliability' : 'tyres');
  const engineerName = (id: string) => data.staff.people.find((person) => person.personId === id)?.name ?? id;

  function num(value: number) {
    return new Intl.NumberFormat(tr.lang === 'en' ? 'en-GB' : 'pl-PL', { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(value);
  }

  function band(value: CarBandView) {
    return `${num(value.low)}–${num(value.high)}`;
  }

  function gain(value: CarBandView) {
    return `+${band(value)}`;
  }

  const asked = $derived(asking !== null && asking !== 'split' ? asking.commit : null);
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.car')}</h1>
  {#if plan}
    <div class="fields">
      <div class="fld"><span class="meta">{tr.t('dev.headcount')}</span><span class="v num">{plan.headcount}</span></div>
      {#if plan.daysToNextRace !== null}
        <div class="fld"><span class="meta">{tr.t('dev.toRace')}</span><span class="v num">{tr.tCount('shell.days', plan.daysToNextRace)}</span></div>
      {/if}
    </div>
  {/if}
</div>

{#if cars.length === 0 || !plan}
  <Status text={tr.t('dev.none')} tone="warn" />
{:else}
  <div class="dev">
    <section class="panel tbl p-cars">
      <header><h2>{tr.t('dev.cars')}</h2><span class="meta">{cars[0]?.season}</span></header>
      <div class="tbl-scroll">
        <table class="table">
          <thead>
            <tr>
              <th></th>
              {#each cars as car, index (car.carId)}<th class="c">{tr.t('dev.car', { n: String(index + 1) })}</th>{/each}
            </tr>
          </thead>
          <tbody>
            {#each PERFORMANCE as area (area)}
              <tr>
                <td>{tr.t(`dev.area.${area}`)}</td>
                {#each cars as car (car.carId)}
                  <td class="c num">{band(car[area])}</td>
                {/each}
              </tr>
            {/each}
            <tr class="sep-row">
              <td>{tr.t('dev.ceiling')}</td>
              {#each cars as car (car.carId)}<td class="c num">{band(car.ceiling)}</td>{/each}
            </tr>
            <tr>
              <td>{tr.t('dev.understanding')}</td>
              {#each cars as car (car.carId)}<td class="c num">{band(car.understanding)}</td>{/each}
            </tr>
          </tbody>
        </table>
      </div>
    </section>

    <section class="panel p-forecast">
      <header><h2>{tr.t('dev.forecast')}</h2><span class="meta">{formatDate(plan.forecast.until, tr.lang)}</span></header>
      <div class="body">
        <div class="fields boxed eq">
          <div class="fld"><span class="meta">{tr.t('dev.area.downforce')}</span><span class="v num">{band(plan.forecast.downforce)}</span></div>
          <div class="fld"><span class="meta">{tr.t('dev.area.mechanicalGrip')}</span><span class="v num">{band(plan.forecast.mechanicalGrip)}</span></div>
          <div class="fld"><span class="meta">{tr.t('dev.area.braking')}</span><span class="v num">{band(plan.forecast.braking)}</span></div>
          <div class="fld"><span class="meta">{tr.t('dev.area.reliability')}</span><span class="v num">{band(plan.forecast.reliability)}</span></div>
        </div>
        <div class="fields dev-extra">
          <div class="fld"><span class="meta">{tr.t('dev.ongoing')}</span><span class="v num">{gain(plan.ongoingGain)}</span></div>
          <div class="fld"><span class="meta">{tr.t('dev.account')}</span><span class="v num">{band(plan.account)}</span></div>
        </div>
      </div>
    </section>

    <section class="panel plan p-plan">
      <header>
        <h2>{tr.t('dev.split')}</h2>
        <Status text={dirty ? tr.t('dev.unconfirmed') : tr.t('dev.applies')} tone={dirty ? 'warn' : 'good'} />
      </header>
      <div class="body">
        <div class="splitbar" aria-hidden="true">
          {#each STREAMS as stream (stream)}<i class={`s-${stream}`} style="flex:{draft[stream]}"></i>{/each}
        </div>
        <div class="streams">
          {#each STREAMS as stream (stream)}
            <div class="stream" class:chg={draft[stream] !== draftOf(plan)[stream]}>
              <span class="meta"><i class={`s-${stream}`}></i>{tr.t(`dev.stream.${stream}`)}</span>
              <div class="step">
                <button class="btn sm" type="button" disabled={draft[stream] <= 0} aria-label={`− ${tr.t(`dev.stream.${stream}`)}`} onclick={() => move(stream, -STEP)}>−</button>
                <span class="val"><b class="num">{draft[stream]}%</b></span>
                <button class="btn sm" type="button" disabled={draft[stream] >= 100} aria-label={`+ ${tr.t(`dev.stream.${stream}`)}`} onclick={() => move(stream, STEP)}>+</button>
              </div>
            </div>
          {/each}
        </div>
        <div class="prios">
          <span class="meta">{tr.t('dev.priorities')}</span>
          {#each AREAS as area (area)}
            <div class="prio" class:chg={draft[area] !== draftOf(plan)[area]}>
              <span>{tr.t(`development.area.${area === 'aero' ? 'Aero' : area === 'chassis' ? 'Chassis' : area === 'reliability' ? 'Reliability' : 'TyresHandling'}`)}</span>
              <div class="ppips" role="radiogroup" aria-label={tr.t('dev.priorities')}>
                {#each Array.from({ length: MAX_PRIORITY }, (_, index) => index + 1) as value (value)}
                  <button type="button" class="pip" class:on={value <= draft[area]} role="radio" aria-checked={value === draft[area]} aria-label={String(value)} onclick={() => priority(area, value === draft[area] ? 0 : value)}></button>
                {/each}
              </div>
              <b class="num pv">{draft[area]}</b>
            </div>
          {/each}
        </div>
        {#if asking === 'split'}
          <Confirmation {tr} {busy} ask={tr.t('dev.split.ask', { current: String(draft.current), account: String(draft.account), next: String(draft.nextYear) })} onCancel={() => (asking = null)} onConfirm={saveSplit} />
        {:else}
          <div class="confirm">
            <div class="fields"><div class="fld"><span class="meta">{tr.t('dev.total')}</span><span class="v num" class:bad={!valid}>{total}%</span></div></div>
            <button class="btn primary" type="button" disabled={!dirty || !valid || busy} onclick={() => (asking = 'split')}>{@html icon(ICON.check, 17)}<span>{tr.t('shell.confirm')}</span></button>
          </div>
        {/if}
      </div>
    </section>

    <section class="panel tbl projects p-projects">
      <header><h2>{tr.t('dev.projects')}</h2></header>
      {#if plan.projects.length > 0}
        <div class="tbl-scroll">
          <table class="table dev-proj">
            <thead>
              <tr>
                <th>{tr.t('dev.project')}</th>
                <th>{tr.t('dev.engineer')}</th>
                <th>{tr.t('dev.state')}</th>
                <th>{tr.t('dev.gain')}</th>
                <th>{tr.t('dev.until')}</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {#each plan.projects as project (project.projectId)}
                {@render projectRow(project)}
              {/each}
            </tbody>
          </table>
        </div>
      {:else}
        <div class="empty"><Status text={tr.t('dev.noProjects')} /></div>
      {/if}
    </section>
  </div>
{/if}

{#snippet projectRow(project: OwnProjectView)}
  <tr>
    <td>
      <b>{tr.t(`development.kind.${project.kind}`)}</b>
      {#if project.area}<small class="muted">{tr.t(`development.area.${project.area}`)} <span class="num">{draftOf(plan)[priorityOf(project.area)]}/{MAX_PRIORITY}</span></small>{/if}
    </td>
    <td>{engineerName(project.engineer)}</td>
    <td>
      <b class="num pct">{project.progressPercent}%</b>
      <div class="bar thin"><i style="width:{project.progressPercent}%"></i></div>
      <small class="muted">{tr.t(`development.status.${project.status}`)}</small>
    </td>
    <td class="num">{gain(project.expectedGain)}</td>
    <td class="num">
      {formatDate(project.productionEnds ?? project.expectedEnd, tr.lang)}
      {#if project.goesLiveOn}<small class="muted">{tr.t('dev.live', { date: formatDate(project.goesLiveOn, tr.lang) })}</small>{/if}
    </td>
    <td>
      {#if project.status === 'Ready' && project.productionDays !== null}
        {#if asked === project.projectId}
          <Confirmation
            {tr}
            {busy}
            ask={tr.t('dev.commit.ask', { days: String(project.productionDays), cost: formatMoney(project.productionCostCents, tr.lang) })}
            onCancel={() => (asking = null)}
            onConfirm={() => commit(project.projectId)}
          />
        {:else}
          <button class="btn sm primary" type="button" disabled={busy} onclick={() => (asking = { commit: project.projectId })}>{tr.t('dev.commit')}</button>
        {/if}
      {:else if project.status === 'InProduction' && project.productionCostCents !== null}
        <small class="muted num">{formatMoney(project.productionCostCents, tr.lang)}</small>
      {/if}
    </td>
  </tr>
{/snippet}
