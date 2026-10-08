<script lang="ts">
  import { untrack } from 'svelte';
  import { BridgeError, HUMAN_MANAGER_ID, query } from '../api/client';
  import type { NewCareerCall, PresetView, TeamOptionView, TranslationMessage } from '../api/types.generated';
  import {
    AXES,
    clampYear,
    COUNTRIES,
    emptySetup,
    isCustom,
    newCareerArgs,
    PRESET_KEYS,
    teamsArgs,
    TILT_KEYS,
    TILTS,
    withPreset,
  } from '../career.mjs';
  import { hasFlag } from '../flags.mjs';
  import { icon, ICON, initials, countryName, type Tr } from '../ui';
  import Flag from './Flag.svelte';
  import Status from './Status.svelte';
  import Steps from './Steps.svelte';
  import Tabs from './Tabs.svelte';
  import TeamCard from './TeamCard.svelte';

  let {
    tr,
    presets,
    suggestedYear,
    suggestedSeed,
    busy,
    error,
    onStart,
  }: {
    tr: Tr;
    presets: PresetView[];
    suggestedYear: number;
    suggestedSeed: string;
    busy: boolean;
    error: string;
    onStart: (call: NewCareerCall) => void;
  } = $props();

  const ORDER = ['you', 'world', 'team', 'summary'];

  let step = $state('you');
  let reachable = $state(0);
  let you = $state({ given: '', family: '', nationality: '', tilt: 'none' });
  let setup = $state(
    untrack(() => {
      const preset = presets.find((item) => item.name === 'Balanced') ?? presets[0];
      const start = emptySetup(suggestedYear, suggestedSeed);
      return preset ? withPreset(start, preset) : start;
    }),
  );
  let teams = $state<TeamOptionView[]>([]);
  let teamId = $state('');
  let problem = $state<TranslationMessage | null>(null);
  let loading = $state(false);
  let loadFault = $state<BridgeError | null>(null);
  let loadedKey = $state('');
  let token = 0;
  let advanced = $state(false);

  let preset = $derived(presets.find((item) => item.name === setup.preset) ?? null);
  let custom = $derived(isCustom(setup, preset));
  let setupKey = $derived(JSON.stringify(teamsArgs(HUMAN_MANAGER_ID, setup)));
  let youReady = $derived(you.given.trim().length > 0 && you.family.trim().length > 0 && you.nationality.length > 0);
  let chosen = $derived(teams.find((item) => item.id === teamId) ?? null);
  let steps = $derived(ORDER.map((value) => ({ value, label: tr.t(`career.step.${value}`) })));
  let at = $derived(ORDER.indexOf(step));
  let name = $derived(`${you.given.trim()} ${you.family.trim()}`.trim());
  let countries = $derived(
    [...COUNTRIES].sort((a, b) => countryName(tr, a).localeCompare(countryName(tr, b), tr.lang === 'en' ? 'en' : 'pl')),
  );

  $effect(() => {
    /* A changed world invalidates the grid: the steps after it have to be walked again. */
    if (setupKey !== loadedKey && reachable > 1) reachable = 1;
  });

  function pickPreset(value: string) {
    const next = presets.find((item) => item.name === value);
    if (next) setup = withPreset(setup, next);
  }

  function resetAxes() {
    if (preset) setup = withPreset(setup, preset);
  }

  async function loadTeams(): Promise<boolean> {
    if (setupKey === loadedKey) return problem === null;
    const mine = ++token;
    loading = true;
    loadFault = null;
    try {
      const list = await query('teams', teamsArgs(HUMAN_MANAGER_ID, setup));
      if (mine !== token) return false;
      teams = list.teams;
      problem = list.problem;
      loadedKey = setupKey;
      if (teamId && !teams.some((item) => item.id === teamId)) teamId = '';
      return list.problem === null;
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
    if (step === 'you' && youReady) {
      reachable = Math.max(reachable, 1);
      step = 'world';
    } else if (step === 'world') {
      setup.year = clampYear(setup.year, suggestedYear);
      if (await loadTeams()) {
        reachable = Math.max(reachable, 2);
        step = 'team';
      }
    } else if (step === 'team' && chosen) {
      reachable = Math.max(reachable, 3);
      step = 'summary';
    } else if (step === 'summary' && chosen) {
      onStart(newCareerArgs(HUMAN_MANAGER_ID, setup, you, chosen.id));
    }
  }

  function back() {
    if (at > 0) step = ORDER[at - 1] ?? 'you';
  }

  let canForward = $derived(
    step === 'you' ? youReady : step === 'world' ? !loading : step === 'team' ? chosen !== null : chosen !== null && !busy,
  );
</script>

<div class="wizard">
  <Steps items={steps} current={step} {reachable} onGo={go} />

  {#if step === 'you'}
    <div class="wiz-grid">
      <section class="panel wiz-form">
        <header><h2>{tr.t('career.step.you')}</h2></header>
        <div class="body form">
          <label class="fld">
            <span class="meta">{tr.t('shell.start.given')}</span>
            <input class="text" type="text" bind:value={you.given} autocomplete="off" />
          </label>
          <label class="fld">
            <span class="meta">{tr.t('shell.start.family')}</span>
            <input class="text" type="text" bind:value={you.family} autocomplete="off" />
          </label>
          <label class="fld">
            <span class="meta">{tr.t('career.you.country')}</span>
            <select class="text" bind:value={you.nationality}>
              <option value="" disabled>—</option>
              {#each countries as code (code)}
                <option value={code}>{countryName(tr, code)}</option>
              {/each}
            </select>
          </label>
          <div class="fld">
            <span class="meta">{tr.t('shell.start.tilt')}</span>
            <div class="tilts" role="radiogroup" aria-label={tr.t('shell.start.tilt')}>
              {#each TILTS as value (value)}
                <button type="button" class="tilt" role="radio" aria-checked={you.tilt === value} onclick={() => (you.tilt = value)}>
                  <span class="rd">{#if you.tilt === value}{@html icon(ICON.check, 13)}{/if}</span>
                  <span class="tl"><b>{tr.t(TILT_KEYS[value as keyof typeof TILT_KEYS])}</b><span class="muted">{tr.t(`${TILT_KEYS[value as keyof typeof TILT_KEYS]}.hint`)}</span></span>
                </button>
              {/each}
            </div>
          </div>
        </div>
      </section>
      <section class="panel dossier dark">
        <span class="av big">{#if name}{initials(name)}{:else}{@html icon(ICON.person, 38)}{/if}</span>
        <h2>{name || '—'}</h2>
        <div class="tags">
          {#if you.nationality}
            <span class="tag">{#if hasFlag(you.nationality)}<Flag code={you.nationality} />{/if}{countryName(tr, you.nationality)}</span>
          {/if}
          {#if you.tilt !== 'none'}<span class="tag">{tr.t(TILT_KEYS[you.tilt as keyof typeof TILT_KEYS])}</span>{/if}
        </div>
      </section>
    </div>
  {:else if step === 'world'}
    <div class="wiz-world">
      <div class="fld">
        <span class="meta">{tr.t('shell.start.year')}</span>
        <input
          class="text num"
          type="number"
          min="1950"
          max="2026"
          bind:value={setup.year}
          onchange={() => (setup.year = clampYear(setup.year, suggestedYear))}
        />
      </div>
      <div class="fld">
        <span class="meta">{tr.t('shell.start.preset')}</span>
        <div class="choices presets" role="radiogroup">
          {#each presets as item (item.name)}
            <button type="button" class="choice" role="radio" aria-checked={setup.preset === item.name} onclick={() => pickPreset(item.name)}>
              <span class="ch"><b>{tr.t(PRESET_KEYS[item.name as keyof typeof PRESET_KEYS])}</b><span class="rd">{#if setup.preset === item.name}{@html icon(ICON.check, 14)}{/if}</span></span>
              <dl class="kv">
                <dt>{tr.t('career.axis.people')}</dt><dd>{tr.t(`career.people.${item.people}`)}</dd>
                <dt>{tr.t('career.axis.ai')}</dt><dd>{tr.t(`career.ai.${item.ai}`)}</dd>
                <dt>{tr.t('career.axis.rules')}</dt><dd>{tr.t(`career.rules.${item.rules}`)}</dd>
              </dl>
            </button>
          {/each}
        </div>
      </div>

      <details class="advanced" bind:open={advanced}>
        <summary>
          <span class="link">{tr.t('career.world.advanced')}</span>
          {#if custom}<Status text={tr.t('career.world.custom')} tone="hi" />{/if}
        </summary>
        <div class="panel adv-body">
          <div class="adv-grid">
            <div class="fld">
              <span class="meta">{tr.t('career.axis.people')}</span>
              <Tabs group="people" items={AXES.people.map((value) => ({ value, label: tr.t(`career.people.${value}`) }))} bind:value={setup.people} />
            </div>
            <div class="fld">
              <span class="meta">{tr.t('career.axis.ai')}</span>
              <Tabs group="ai" items={AXES.ai.map((value) => ({ value, label: tr.t(`career.ai.${value}`) }))} bind:value={setup.ai} />
            </div>
            <div class="fld">
              <span class="meta">{tr.t('career.axis.rules')}</span>
              <Tabs group="rules" items={AXES.rules.map((value) => ({ value, label: tr.t(`career.rules.${value}`) }))} bind:value={setup.rules} />
            </div>
            <div class="fld">
              <span class="meta">{tr.t('career.axis.fatality')}</span>
              <Tabs group="fatality" items={AXES.fatality.map((value) => ({ value, label: tr.t(`career.toggle.${value}`) }))} bind:value={setup.fatality} />
            </div>
            <div class="fld">
              <span class="meta">{tr.t('career.axis.noNumbers')}</span>
              <Tabs group="no-numbers" items={AXES.fatality.map((value) => ({ value, label: tr.t(`career.toggle.${value}`) }))} bind:value={setup.noNumbers} />
            </div>
            <div class="adv-nums">
              <label class="fld slider">
                <span class="meta">{tr.t('career.axis.history')}</span>
                <span class="sl">
                  <input type="range" min="0" max="10" step="1" bind:value={setup.history} />
                  <b class="num">{setup.history}</b>
                </span>
                <small class="muted num">{tr.t('career.range', { min: '0', max: '10' })}</small>
              </label>
              <label class="fld slider">
                <span class="meta">{tr.t('career.axis.randomness')}</span>
                <span class="sl">
                  <input type="range" min="0" max="100" step="1" bind:value={setup.randomness} />
                  <b class="num">{setup.randomness}</b>
                </span>
                <small class="muted num">{tr.t('career.range', { min: '0', max: '100' })}</small>
              </label>
              <label class="fld">
                <span class="meta">{tr.t('career.axis.seed')}</span>
                <input class="text num" type="text" inputmode="numeric" bind:value={setup.seed} autocomplete="off" />
              </label>
            </div>
          </div>
          {#if custom}
            <div class="confirm">
              <button class="btn sm" type="button" onclick={resetAxes}>{tr.t('career.world.reset')}</button>
            </div>
          {/if}
        </div>
      </details>
      {#if problem}<p class="bad">{tr.tMsg(problem)}</p>{/if}
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
  {:else if chosen}
    <div class="wiz-summary">
      <TeamCard team={chosen} {tr} wide selected />
      <div class="sum-side">
        <section class="panel">
          <header><h2>{tr.t('career.summary.you')}</h2></header>
          <div class="body">
            <table class="kvt">
              <tbody>
                <tr><th>{tr.t('shell.start.given')}</th><td>{name}</td></tr>
                <tr><th>{tr.t('career.you.country')}</th><td>{#if hasFlag(you.nationality)}<Flag code={you.nationality} />{/if}{countryName(tr, you.nationality)}</td></tr>
                <tr><th>{tr.t('shell.start.tilt')}</th><td>{tr.t(TILT_KEYS[you.tilt as keyof typeof TILT_KEYS])}</td></tr>
              </tbody>
            </table>
          </div>
        </section>
        <section class="panel">
          <header><h2>{tr.t('career.summary.world')}</h2>{#if custom}<Status text={tr.t('career.world.custom')} tone="hi" />{/if}</header>
          <div class="body">
            <table class="kvt">
              <tbody>
                <tr><th>{tr.t('shell.start.year')}</th><td class="num">{setup.year}</td></tr>
                <tr><th>{tr.t('shell.start.preset')}</th><td>{tr.t(PRESET_KEYS[setup.preset as keyof typeof PRESET_KEYS])}</td></tr>
                <tr><th>{tr.t('career.axis.people')}</th><td>{tr.t(`career.people.${setup.people}`)}</td></tr>
                <tr><th>{tr.t('career.axis.ai')}</th><td>{tr.t(`career.ai.${setup.ai}`)}</td></tr>
                <tr><th>{tr.t('career.axis.rules')}</th><td>{tr.t(`career.rules.${setup.rules}`)}</td></tr>
                <tr><th>{tr.t('career.axis.history')}</th><td class="num">{setup.history}</td></tr>
                <tr><th>{tr.t('career.axis.randomness')}</th><td class="num">{setup.randomness}</td></tr>
              </tbody>
            </table>
          </div>
        </section>
      </div>
    </div>
  {/if}

  {#if loadFault}<p class="bad">{tr.t(loadFault.key, loadFault.parameters)}</p>{/if}
  {#if error}<p class="bad">{error}</p>{/if}

  <div class="wiz-nav">
    {#if at > 0}
      <button class="btn" type="button" onclick={back}>{@html icon(ICON.back, 17)}<span>{tr.t('career.back')}</span></button>
    {/if}
    <button class="btn primary" type="button" disabled={!canForward || busy} onclick={forward}>
      <span>{step === 'summary' ? tr.t('career.start') : tr.t('career.next')}</span>{@html icon(step === 'summary' ? ICON.check : ICON.arrow, 17)}
    </button>
  </div>
</div>
