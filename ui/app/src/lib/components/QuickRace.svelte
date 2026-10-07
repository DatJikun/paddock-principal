<script lang="ts">
  /*
   * The quick race (#280): season, team and a round of that season's calendar, then straight to the grid. The world and the
   * race are the bridge's (the same as a new career's); this form only gathers the three choices.
   */
  import { untrack } from 'svelte';
  import { BridgeError, HUMAN_MANAGER_ID, query } from '../api/client';
  import type { QuickRaceCall, QuickRoundView, TeamOptionView, TranslationMessage } from '../api/types.generated';
  import { clampYear } from '../career.mjs';
  import { formatDay } from '../date.mjs';
  import { livery } from '../livery.mjs';
  import { hasFlag } from '../flags.mjs';
  import { freshSeed, quickRaceArgs, quickTeamsArgs } from '../quick-race.mjs';
  import { countryName, icon, ICON, type Tr } from '../ui';
  import Flag from './Flag.svelte';
  import Steps from './Steps.svelte';
  import TeamCard from './TeamCard.svelte';
  import TrackMap from './TrackMap.svelte';

  let {
    tr,
    suggestedYear,
    busy,
    error,
    onStart,
  }: {
    tr: Tr;
    suggestedYear: number;
    busy: boolean;
    error: string;
    onStart: (call: QuickRaceCall) => void;
  } = $props();

  const ORDER = ['season', 'team', 'track'];

  let step = $state('season');
  let reachable = $state(0);
  let year = $state(untrack(() => suggestedYear));
  /* One seed per visit to the form: the cards and the race come from the same world. */
  let seed = $state(freshSeed());
  let teams = $state<TeamOptionView[]>([]);
  let rounds = $state<QuickRoundView[]>([]);
  let problem = $state<TranslationMessage | null>(null);
  let loadedYear = $state(0);
  let loading = $state(false);
  let loadFault = $state<BridgeError | null>(null);
  let teamId = $state('');
  let round = $state(0);
  let token = 0;

  let steps = $derived(ORDER.map((value) => ({ value, label: tr.t(`quick.step.${value}`) })));
  let at = $derived(ORDER.indexOf(step));
  let chosen = $derived(teams.find((item) => item.id === teamId) ?? null);
  let call = $derived(quickRaceArgs(HUMAN_MANAGER_ID, year, teamId, round, seed));
  /* The chosen team's colours carry the form from the team step on, as they carry the race. */
  let colours = $derived(teamId ? livery(teamId) : null);

  $effect(() => {
    /* Another season is another grid and calendar: the later steps have to be walked again. */
    if (year !== loadedYear && reachable > 0) reachable = 0;
  });

  async function loadSeason(): Promise<boolean> {
    if (year === loadedYear) return problem === null && teams.length > 0;
    const mine = ++token;
    loading = true;
    loadFault = null;
    try {
      const [list, calendar] = await Promise.all([
        query('teams', quickTeamsArgs(HUMAN_MANAGER_ID, year, seed)),
        query('quickRounds', { managerId: HUMAN_MANAGER_ID, year }),
      ]);
      if (mine !== token) return false;
      teams = list.teams;
      problem = list.problem;
      rounds = calendar.rounds;
      loadedYear = year;
      if (!teams.some((item) => item.id === teamId)) teamId = '';
      if (!rounds.some((item) => item.round === round)) round = 0;
      return list.problem === null && teams.length > 0;
    } catch (caught) {
      if (mine === token) loadFault = caught instanceof BridgeError ? caught : new BridgeError('bridge.error.internal');
      return false;
    } finally {
      if (mine === token) loading = false;
    }
  }

  function go(next: string) {
    const index = ORDER.indexOf(next);
    if (index < 0 || index > reachable) return;
    step = next;
  }

  async function forward() {
    if (busy || loading) return;
    if (step === 'season') {
      year = clampYear(year, suggestedYear);
      if (await loadSeason()) {
        reachable = Math.max(reachable, 1);
        step = 'team';
      }
    } else if (step === 'team' && chosen) {
      reachable = Math.max(reachable, 2);
      step = 'track';
    } else if (step === 'track' && call) {
      onStart(call);
    }
  }

  function back() {
    if (at > 0) step = ORDER[at - 1] ?? 'season';
  }

  let canForward = $derived(step === 'season' ? !loading : step === 'team' ? chosen !== null : call !== null && !busy);
</script>

<div class="wizard quick" style={colours ? `--t1:${colours.main};--t2:${colours.accent};--on1:${colours.on}` : undefined}>
  <Steps items={steps} current={step} {reachable} onGo={go} />

  {#if step === 'season'}
    <div class="wiz-world">
      <label class="fld">
        <span class="meta">{tr.t('shell.start.year')}</span>
        <input
          class="text num year"
          type="number"
          min="1950"
          max="2025"
          bind:value={year}
          onchange={() => (year = clampYear(year, suggestedYear))}
          onkeydown={(event) => {
            if (event.key === 'Enter') void forward();
          }}
        />
      </label>
      {#if problem && loadedYear === year}<p class="bad">{tr.tMsg(problem)}</p>{/if}
    </div>
  {:else if step === 'team'}
    {#if loading}
      <p class="muted">{tr.t('career.team.loading')}</p>
    {:else if teams.length === 0}
      <p class="muted">{tr.t('career.team.empty')}</p>
    {:else}
      <div class="cards" role="group">
        {#each teams as team (team.id)}
          <TeamCard {team} {tr} selected={team.id === teamId} onPick={(id) => (teamId = id)} />
        {/each}
      </div>
    {/if}
  {:else}
    <div class="fields quick-pick">
      <div class="fld"><span class="meta">{tr.t('quick.step.season')}</span><span class="v num">{year}</span></div>
      <div class="fld"><span class="meta">{tr.t('quick.step.team')}</span><span class="v">{chosen?.name ?? '—'}</span></div>
      <div class="fld"><span class="meta">{tr.t('quick.rounds')}</span><span class="v num">{rounds.length}</span></div>
    </div>
    {#if rounds.length === 0}
      <p class="muted">{tr.t('quick.noRounds')}</p>
    {:else}
      <div class="cal" role="radiogroup" aria-label={tr.t('quick.step.track')}>
        {#each rounds as item (item.round)}
          <button type="button" class="rnd" class:sel={item.round === round} role="radio" aria-checked={item.round === round} onclick={() => (round = item.round)}>
            <span class="rno">{item.round}</span>
            <span class="rinfo">
              <span class="rdate">{formatDay(item.date, tr.lang)}</span>
              <b>{#if item.country && hasFlag(item.country)}<Flag code={item.country} size="md" />{/if}<span>{item.country ? countryName(tr, item.country) : ''}</span></b>
              <small>{item.circuitName ?? item.layoutId}</small>
            </span>
            <TrackMap points={item.points} label={item.circuitName ?? item.layoutId} />
          </button>
        {/each}
      </div>
    {/if}
  {/if}

  {#if loadFault}<p class="bad">{tr.t(loadFault.key, loadFault.parameters)}</p>{/if}
  {#if error}<p class="bad">{error}</p>{/if}

  <div class="wiz-nav">
    {#if at > 0}
      <button class="btn" type="button" onclick={back}>{@html icon(ICON.back, 17)}<span>{tr.t('career.back')}</span></button>
    {/if}
    <button class="btn primary" type="button" disabled={!canForward || busy} onclick={forward}>
      {#if step === 'track'}
        <span>{busy ? tr.t('quick.starting') : tr.t('quick.start')}</span>{@html icon(ICON.flag, 17)}
      {:else}
        <span>{tr.t('career.next')}</span>{@html icon(ICON.arrow, 17)}
      {/if}
    </button>
  </div>
</div>
