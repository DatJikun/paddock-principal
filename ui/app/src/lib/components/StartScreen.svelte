<script lang="ts">
  import type { SaveListItem, TeamOptionView } from '../api/types.generated';
  import { formatDate } from '../date.mjs';
  import Tabs from './Tabs.svelte';

  export type CareerForm = {
    teamId: string;
    givenName: string;
    familyName: string;
    nationality: string;
    tilt: string;
    preset: string;
    year: number;
  };

  let {
    year = $bindable(),
    teams,
    saves,
    busy,
    lang,
    t,
    onStart,
    onLoad,
  }: {
    year: number;
    teams: TeamOptionView[];
    saves: SaveListItem[];
    busy: boolean;
    lang: string;
    t: (key: string) => string;
    onStart: (form: CareerForm) => void;
    onLoad: (name: string) => void;
  } = $props();

  const tilts = [
    { value: 'none', key: 'shell.tilt.none' },
    { value: 'negotiation', key: 'shell.tilt.negotiation' },
    { value: 'people_management', key: 'shell.tilt.peopleManagement' },
    { value: 'politics', key: 'shell.tilt.politics' },
    { value: 'business', key: 'shell.tilt.business' },
  ];
  const presets = [
    { value: 'MostHistorical', key: 'shell.preset.mostHistorical' },
    { value: 'Balanced', key: 'shell.preset.balanced' },
    { value: 'Chaos', key: 'shell.preset.chaos' },
  ];

  let mode = $state('new');
  let teamId = $state('');
  let given = $state('');
  let family = $state('');
  let nationality = $state('');
  let tilt = $state('none');
  let preset = $state('Chaos');
  let saveName = $state('');

  let ready = $derived(
    mode === 'load'
      ? saveName.length > 0
      : teamId.length > 0 && given.trim().length > 0 && family.trim().length > 0 && nationality.trim().length > 0,
  );

  function confirm() {
    if (!ready || busy) return;
    if (mode === 'load') {
      onLoad(saveName);
      return;
    }
    onStart({
      teamId,
      givenName: given.trim(),
      familyName: family.trim(),
      nationality: nationality.trim(),
      tilt,
      preset,
      year,
    });
  }
</script>

<div class="start">
  <Tabs
    group="career-mode"
    items={[
      { value: 'new', label: t('shell.start.new') },
      { value: 'load', label: t('shell.start.load') },
    ]}
    bind:value={mode}
  />
  {#if mode === 'new'}
    <div class="fields boxed">
      <label class="fld">
        <span class="meta">{t('shell.start.year')}</span>
        <input class="text num" type="number" min="1950" max="2026" bind:value={year} />
      </label>
      <label class="fld">
        <span class="meta">{t('shell.start.given')}</span>
        <input class="text" type="text" bind:value={given} autocomplete="off" />
      </label>
      <label class="fld">
        <span class="meta">{t('shell.start.family')}</span>
        <input class="text" type="text" bind:value={family} autocomplete="off" />
      </label>
      <label class="fld">
        <span class="meta">{t('shell.start.nationality')}</span>
        <input class="text" type="text" bind:value={nationality} autocomplete="off" maxlength="3" />
      </label>
    </div>
    <div class="fld">
      <span class="meta">{t('shell.start.preset')}</span>
      <Tabs group="preset" items={presets.map((item) => ({ value: item.value, label: t(item.key) }))} bind:value={preset} />
    </div>
    <div class="fld">
      <span class="meta">{t('shell.start.tilt')}</span>
      <Tabs group="tilt" items={tilts.map((item) => ({ value: item.value, label: t(item.key) }))} bind:value={tilt} />
    </div>
    <div class="panel">
      <table class="table fit">
        <thead>
          <tr><th>{t('shell.start.team')}</th></tr>
        </thead>
        <tbody>
          {#each teams as team (team.id)}
            <tr class="go-row" class:sel={teamId === team.id} onclick={() => (teamId = team.id)}>
              <td>{team.name}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {:else}
    <div class="panel">
      <table class="table fit">
        <thead>
          <tr>
            <th>{t('shell.start.team')}</th>
            <th>{t('shell.col.date')}</th>
            <th>{t('shell.save.name')}</th>
          </tr>
        </thead>
        <tbody>
          {#each saves as save (save.name)}
            <tr class="go-row" class:sel={saveName === save.name} onclick={() => (saveName = save.name)}>
              <td>{save.teamId}</td>
              <td class="num">{formatDate(save.date, lang)}</td>
              <td>{save.name}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  {/if}
  <div class="confirm">
    <button class="btn primary" type="button" disabled={!ready || busy} onclick={confirm}>{t('shell.confirm')}</button>
  </div>
</div>
