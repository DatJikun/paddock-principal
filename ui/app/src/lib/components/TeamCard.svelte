<script lang="ts">
  import type { TeamOptionView } from '../api/types.generated';
  import { hasFlag } from '../flags.mjs';
  import { livery } from '../livery.mjs';
  import { countryName, type Tr } from '../ui';
  import Flag from './Flag.svelte';

  let {
    team,
    tr,
    selected = false,
    wide = false,
    onPick,
  }: {
    team: TeamOptionView;
    tr: Tr;
    selected?: boolean;
    wide?: boolean;
    onPick?: (id: string) => void;
  } = $props();

  const TIERS: Record<string, number> = { low: 1, typical: 2, top: 3 };

  let colours = $derived(livery(team.id));
  let level = $derived(team.budget ? (TIERS[team.budget] ?? 0) : 0);
  /* A season's roster can run to eight names; the grid shows the first four, the summary all of them. */
  let shown = $derived(wide ? team.drivers : team.drivers.slice(0, 4));
  let engineKind = $derived(team.engine ? `team.engine.${team.engine.supplyType}` : '');
</script>

{#snippet body()}
  <span class="tc-band">
    <b>{team.name}</b>
    {#if selected}<span class="tc-chosen">{tr.t('team.card.chosen')}</span>{/if}
  </span>
  <span class="tc-body">
    <span class="tc-block">
      <span class="meta">{tr.t('team.card.drivers')}</span>
      {#if team.drivers.length > 0}
        <span class="tc-drivers">
          {#each shown as driver (driver.name)}
            <span class="tc-driver">
              {#if hasFlag(driver.nationality)}<Flag code={driver.nationality} />{/if}
              <b>{driver.name}</b>
              <small class="num" title={countryName(tr, driver.nationality)}>{tr.t('team.card.age', { age: String(driver.age) })}</small>
            </span>
          {/each}
          {#if shown.length < team.drivers.length}<span class="muted tc-more">{tr.t('team.card.more', { count: String(team.drivers.length - shown.length) })}</span>{/if}
        </span>
      {:else}
        <span class="muted">{tr.t('team.card.noDrivers')}</span>
      {/if}
    </span>
    <span class="tc-block">
      <span class="meta">{tr.t('team.card.engine')}</span>
      {#if team.engine}
        <span class="tc-engine">
          <b>{team.engine.name}</b>
          <small>{tr.t(engineKind)}{team.engine.supplier && team.engine.supplyType !== 'works' ? ` · ${team.engine.supplier}` : ''}</small>
        </span>
      {:else}
        <span class="muted">{tr.t('team.card.noData')}</span>
      {/if}
    </span>
    <span class="tc-facts">
      <span class="fld">
        <span class="meta">{tr.t('team.card.budget')}</span>
        {#if team.budget}
          <span class="v"><span class="pips" aria-hidden="true">{#each [1, 2, 3] as pip (pip)}<i class:on={pip <= level}></i>{/each}</span>{tr.t(`team.budget.${team.budget}`)}</span>
        {:else}
          <span class="v muted">{tr.t('team.card.noData')}</span>
        {/if}
      </span>
      <span class="fld">
        <span class="meta">{tr.t('team.card.lastSeason')}</span>
        {#if team.lastSeason !== null}
          <span class="v num">P{team.lastSeason}</span>
        {:else}
          <span class="v muted">{tr.t('team.card.noData')}</span>
        {/if}
      </span>
      <span class="fld">
        <span class="meta">{tr.t('team.card.board')}</span>
        {#if team.expected !== null}
          <span class="v num">P{team.expected}{#if team.fieldSize !== null}<small class="muted">{tr.t('team.card.of', { total: String(team.fieldSize) })}</small>{/if}</span>
        {:else}
          <span class="v muted">{tr.t('team.card.noData')}</span>
        {/if}
      </span>
    </span>
  </span>
{/snippet}

{#if onPick}
  <button
    type="button"
    class="tcard"
    class:sel={selected}
    class:wide
    aria-pressed={selected}
    style="--c1:{colours.main};--c2:{colours.accent};--con:{colours.on}"
    onclick={() => onPick(team.id)}
  >{@render body()}</button>
{:else}
  <div class="tcard" class:wide style="--c1:{colours.main};--c2:{colours.accent};--con:{colours.on}">{@render body()}</div>
{/if}
