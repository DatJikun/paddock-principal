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

  /* The player's choices (PP-066): the slider between the car that races and the next concept, the character of the next
     concept, and when to introduce a finished one. Everything else is the engineers'. */
  const STEP = 5;
  const CHARACTERS = ['Evolution', 'Revolution'] as const;
  const AEROS = ['Straights', 'Balanced', 'Corners'] as const;

  let view = $derived<OwnDevelopmentView | null>(data.development.own[0] ?? null);

  type Draft = { share: number; philosophy: number; aero: number };
  const draftOf = (v: OwnDevelopmentView | null): Draft => ({
    share: v?.next.sharePercent ?? 0,
    philosophy: v?.next.philosophyMilli ?? -1000,
    aero: v?.next.aeroMilli ?? 0,
  });

  let draft = $state<Draft>(untrack(() => draftOf(view)));
  let appliedKey = $state(untrack(() => JSON.stringify(draftOf(view))));
  let asking = $state<'plan' | 'commit' | null>(null);

  /* After a confirmed change (or a reload) the bridge's numbers are the truth again. */
  $effect(() => {
    const key = JSON.stringify(draftOf(view));
    if (key !== appliedKey) {
      appliedKey = key;
      draft = draftOf(view);
    }
  });

  let applied = $derived(draftOf(view));
  let shareChanged = $derived(draft.share !== applied.share);
  let characterChanged = $derived(draft.philosophy !== applied.philosophy || draft.aero !== applied.aero);
  let dirty = $derived(shareChanged || characterChanged);

  const characterName = (milli: number) => (milli < 0 ? 'Evolution' : milli > 0 ? 'Revolution' : 'Neutral');
  const aeroName = (milli: number) => (milli < 0 ? 'Straights' : milli > 0 ? 'Corners' : 'Balanced');

  let ask = $derived.by(() => {
    const parts: string[] = [];
    if (shareChanged) parts.push(tr.t('dev.slider.ask', { next: String(draft.share), now: String(100 - draft.share) }));
    if (characterChanged)
      parts.push(tr.t('dev.char.ask', { philosophy: tr.t(`dev.char.${characterName(draft.philosophy)}`), aero: tr.t(`dev.aero.${aeroName(draft.aero)}`) }));
    return parts.join(' ');
  });

  async function save() {
    asking = null;
    if (shareChanged) await act('setDevelopmentSplit', { organizationId: teamId, nextPercent: draft.share });
    if (characterChanged) await act('setNextConcept', { organizationId: teamId, philosophyMilli: draft.philosophy, aeroMilli: draft.aero });
  }

  async function commit(projectId: string) {
    asking = null;
    await act('commitConcept', { organizationId: teamId, projectId });
  }

  const engineerName = (id: string) => data.staff.people.find((person) => person.personId === id)?.name ?? id;
  const whole = (value: number) => String(Math.round(value));
  const band = (value: CarBandView) => `${whole(value.low)}–${whole(value.high)}`;
  const gain = (value: CarBandView) => `+${whole(value.low)}–${whole(value.high)}`;
  const signed = (value: number) => (value > 0 ? `+${whole(value)}` : whole(value));
  const mid = (value: CarBandView) => (value.low + value.high) / 2;
  const pct = (value: number) => `${value > 0 ? '+' : ''}${value.toFixed(1).replace(/\.0$/, '')}`;

  /* The comparison reads from one scale for the whole grid, so a longer bar is a faster car in every row. */
  const SCALE_LOW = 20;
  const SCALE_HIGH = 100;
  const at = (value: number) => `${Math.max(0, Math.min(100, ((value - SCALE_LOW) / (SCALE_HIGH - SCALE_LOW)) * 100))}%`;
  const span = (value: CarBandView) => `left:${at(value.low)};width:calc(${at(value.high)} - ${at(value.low)})`;

  let rivals = $derived(view?.areas[0]?.rivals ?? []);
  let next = $derived(view?.next ?? null);
  let decision = $derived(next?.decision ?? null);
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.car')}</h1>
  {#if view}
    <div class="fields">
      <div class="fld"><span class="meta">{tr.t('dev.headcount')}</span><span class="v num">{view.headcount}</span></div>
      {#if view.daysToNextRace !== null}
        <div class="fld"><span class="meta">{tr.t('dev.toRace')}</span><span class="v num">{tr.tCount('shell.days', view.daysToNextRace)}</span></div>
      {/if}
    </div>
  {/if}
</div>

{#if !view || !next || view.areas.length === 0}
  <Status text={tr.t('dev.none')} tone="warn" />
{:else}
  <div class="dev">
    <section class="panel p-concept">
      <header><h2>{tr.t('dev.concept')}</h2><b class="cname">{view.concept.name}</b></header>
      <div class="body">
        <div class="fields boxed eq">
          <div class="fld"><span class="meta">{tr.t('dev.ceiling')}</span><span class="v num">{band(view.concept.ceiling)}</span></div>
          <div class="fld"><span class="meta">{tr.t(`dev.char.${characterName(view.concept.philosophyMilli)}`)}</span><span class="v">{tr.t(`dev.aero.${aeroName(view.concept.aeroMilli)}`)}</span></div>
        </div>
      </div>
    </section>

    <section class="panel tbl p-grid">
      <header><h2>{tr.t('dev.areas')}</h2></header>
      <div class="tbl-scroll">
        <table class="table dev-grid">
          <thead>
            <tr>
              <th></th>
              <th class="c">{tr.t('dev.you')}</th>
              {#each rivals as rival (rival.organizationId)}<th class="c">{rival.name}</th>{/each}
            </tr>
          </thead>
          <tbody>
            {#each view.areas as area (area.area)}
              <tr class:sep-row={area.area === 'Total'}>
                <td>{tr.t(`dev.area.${area.area}`)}</td>
                <td class="c num own"><b>{band(area.own)}</b><span class="rng" aria-hidden="true"><i style={span(area.own)}></i></span></td>
                {#each area.rivals as rival (rival.organizationId)}
                  <td class="c num"><span>{band(rival.band)}</span><span class="rng rv" aria-hidden="true"><i style={span(rival.band)}></i></span></td>
                {/each}
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
    </section>

    <section class="panel p-und">
      <header><h2>{tr.t('dev.understanding')}</h2><b class="num">{band(view.understanding.level)}%</b></header>
      <div class="body">
        <div class="bar"><i style="width:{Math.round(mid(view.understanding.level))}%"></i></div>
        {#if view.understanding.notes.length > 0}
          <ul class="notes">
            {#each view.understanding.notes as note, index (index)}
              <li class:neg={note.points < 0}>
                <span>{tr.t('dev.note.line', { source: tr.t(`dev.note.${note.source}`), points: pct(note.points) })}</span>
                <small class="muted">{formatDate(note.on, tr.lang)}</small>
              </li>
            {/each}
          </ul>
        {:else}
          <Status text={tr.t('dev.noNotes')} />
        {/if}
      </div>
    </section>

    <section class="panel plan p-next">
      <header>
        <h2>{tr.t('dev.next')}</h2>
        <Status text={dirty ? tr.t('dev.unconfirmed') : tr.t('dev.applies')} tone={dirty ? 'warn' : 'good'} />
      </header>
      <div class="body">
        <div class="slide" class:chg={shareChanged}>
          <span class="meta">{tr.t('dev.slider')}</span>
          <div class="slide-row">
            <span class="num"><b>{100 - draft.share}%</b> <small class="muted">{tr.t('dev.slider.now')}</small></span>
            <input type="range" min="0" max="100" step={STEP} bind:value={draft.share} aria-label={tr.t('dev.slider')} />
            <span class="num"><b>{draft.share}%</b> <small class="muted">{tr.t('dev.slider.next')}</small></span>
          </div>
        </div>

        <div class="chars" role="radiogroup" aria-label={tr.t('dev.character')}>
          <span class="meta">{tr.t('dev.character')}</span>
          {#each next.characters as option (option.id)}
            <button
              type="button"
              class="opt"
              class:on={draft.philosophy === option.philosophyMilli}
              role="radio"
              aria-checked={draft.philosophy === option.philosophyMilli}
              onclick={() => (draft.philosophy = option.philosophyMilli)}
            >
              <b>{tr.t(`dev.char.${option.id}`)}</b>
              <small>{tr.t('dev.char.facts', { share: String(option.startSharePercent), start: band(option.startLevel), ceiling: band(option.ceiling) })}</small>
            </button>
          {/each}
        </div>

        <div class="chars" role="radiogroup" aria-label={tr.t('dev.aero')}>
          <span class="meta">{tr.t('dev.aero')}</span>
          {#each next.aeros as option (option.id)}
            <button
              type="button"
              class="opt"
              class:on={draft.aero === option.aeroMilli}
              role="radio"
              aria-checked={draft.aero === option.aeroMilli}
              onclick={() => (draft.aero = option.aeroMilli)}
            >
              <b>{tr.t(`dev.aero.${option.id}`)}</b>
              <small>{tr.t('dev.aero.facts', { straights: signed(option.straightsPercent), corners: signed(option.cornersPercent) })}</small>
            </button>
          {/each}
        </div>

        {#if asking === 'plan'}
          <Confirmation {tr} {busy} {ask} onCancel={() => (asking = null)} onConfirm={save} />
        {:else}
          <div class="confirm">
            <div class="fields">{#if next.status === 'Active'}<div class="fld"><small class="muted">{tr.t('dev.char.running')}</small></div>{/if}</div>
            <button class="btn primary" type="button" disabled={!dirty || busy} onclick={() => (asking = 'plan')}>{@html icon(ICON.check, 17)}<span>{tr.t('shell.confirm')}</span></button>
          </div>
        {/if}

        <div class="state">
          {#if next.status === 'Active' && next.readyOn}
            <Status text={tr.t('dev.status.Active', { date: formatDate(next.readyOn, tr.lang) })} />
            <div class="bar thin"><i style="width:{next.progressPercent}%"></i></div>
          {:else if next.status === 'InProduction' && next.readyOn}
            <Status text={tr.t('dev.status.InProduction', { date: formatDate(next.readyOn, tr.lang) })} />
            {#if next.goesLiveOn}<small class="muted">{tr.t('dev.status.live', { date: formatDate(next.goesLiveOn, tr.lang) })}</small>{/if}
          {:else if next.status === 'Ready'}
            <Status text={tr.t('dev.status.Ready')} tone="warn" />
          {:else}
            <Status text={tr.t('dev.status.None')} />
          {/if}
        </div>
      </div>
    </section>

    {#if decision}
      <section class="panel p-decision">
        <header><h2>{tr.t('dev.decision', { concept: decision.name })}</h2></header>
        <div class="body">
          {#if decision.breakthrough}<Status text={tr.t('dev.decision.big')} tone="good" />{/if}
          <div class="fields boxed eq">
            <div class="fld"><span class="meta">{tr.t('dev.decision.ceiling', { ceiling: band(decision.ceiling), now: band(decision.ceilingNow) })}</span><span class="v num">{gain(decision.gain)}</span></div>
            <div class="fld"><span class="meta">{tr.t('dev.decision.start', { start: band(decision.startLevel), now: band(decision.levelNow) })}</span></div>
            <div class="fld"><span class="meta">{tr.t('dev.decision.build', { days: String(decision.buildDays), cost: formatMoney(decision.costCents, tr.lang) })}</span></div>
            {#if decision.firstRace}<div class="fld"><span class="meta">{tr.t('dev.status.live', { date: formatDate(decision.firstRace, tr.lang) })}</span></div>{/if}
          </div>
          {#if asking === 'commit'}
            <Confirmation
              {tr}
              {busy}
              ask={tr.t('dev.commit.ask', { days: String(decision.buildDays), cost: formatMoney(decision.costCents, tr.lang) })}
              onCancel={() => (asking = null)}
              onConfirm={() => commit(decision.projectId)}
            />
          {:else}
            <div class="confirm">
              <span></span>
              <button class="btn primary" type="button" disabled={busy} onclick={() => (asking = 'commit')}>{tr.t('dev.commit')}</button>
            </div>
          {/if}
        </div>
      </section>
    {/if}

    <section class="panel tbl projects p-projects">
      <header><h2>{tr.t('dev.parts')}</h2></header>
      {#if view.projects.length > 0}
        <div class="tbl-scroll">
          <table class="table dev-proj">
            <thead>
              <tr>
                <th>{tr.t('dev.part')}</th>
                <th>{tr.t('dev.engineer')}</th>
                <th>{tr.t('dev.state')}</th>
                <th>{tr.t('dev.gain')}</th>
                <th>{tr.t('dev.until')}</th>
              </tr>
            </thead>
            <tbody>
              {#each view.projects as project (project.projectId)}
                {@render partRow(project)}
              {/each}
            </tbody>
          </table>
        </div>
      {:else}
        <div class="empty"><Status text={tr.t('dev.noParts')} /></div>
      {/if}
    </section>
  </div>
{/if}

{#snippet partRow(project: OwnProjectView)}
  <tr>
    <td><b>{project.area ? tr.t(`development.area.${project.area}`) : tr.t(`development.kind.${project.kind}`)}</b></td>
    <td>{engineerName(project.engineer)}</td>
    <td>
      <b class="num pct">{project.progressPercent}%</b>
      <div class="bar thin"><i style="width:{project.progressPercent}%"></i></div>
    </td>
    <td class="num">{gain(project.expectedGain)}</td>
    <td class="num">{formatDate(project.expectedEnd, tr.lang)}</td>
  </tr>
{/snippet}
