<script lang="ts">
  import type { TeamOptionView } from '../api/types.generated';
  import { hasFlag } from '../flags.mjs';
  import { livery } from '../livery.mjs';
  import { formatMoney } from '../money.mjs';
  import { countryName, type Tr } from '../ui';
  import Emblem from './Emblem.svelte';
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

  const AREAS = ['car', 'infrastructure', 'drivers', 'staff'] as const;

  let colours = $derived(livery(team.id));
  /* Race seats are listed; reserves are only counted, so a long roster does not read as a line-up. */
  let race = $derived(team.drivers.filter((driver) => driver.seat !== 'Reserve'));
  let reserves = $derived(team.drivers.filter((driver) => driver.seat === 'Reserve'));
  /* A works engine is "factory"; every other engine is named by who builds it. */
  let works = $derived(team.engine?.supplyType === 'works');
</script>

{#snippet body()}
  <span class="tc-band">
    <Emblem id={team.id} name={team.name} size={58} />
    <b>{team.name}</b>
    {#if selected}<span class="tc-chosen">{tr.t('team.card.chosen')}</span>{/if}
  </span>
  <span class="tc-body">
    <span class="tc-facts">
      <span class="fld">
        <span class="meta">{tr.t('team.card.lastSeason')}</span>
        {#if team.lastSeason !== null}
          <span class="v num">P{team.lastSeason}</span>
        {:else}
          <span class="v">{tr.t('team.card.newcomer')}</span>
        {/if}
      </span>
      <span class="fld">
        <span class="meta">{tr.t('team.card.budget')}</span>
        {#if team.budgetCents !== null}
          <span class="v num">{formatMoney(team.budgetCents, tr.lang)}</span>
        {:else}
          <span class="v muted">{tr.t('team.card.noData')}</span>
        {/if}
      </span>
      <span class="fld">
        <span class="meta">{tr.t('team.card.engine')}</span>
        {#if team.engine}
          <span class="v" title={team.engine.name}>{works ? tr.t('team.engine.works') : team.engine.supplier}</span>
        {:else}
          <span class="v muted">{tr.t('team.card.noData')}</span>
        {/if}
      </span>
    </span>
    {#if team.levels}
      <span class="tc-levels" role="group" aria-label={tr.t('team.card.levels')}>
        {#each AREAS as area (area)}
          {@const level = team.levels[area]}
          <span class="lv" title={level === null ? '' : tr.t('team.level.title', { area: tr.t(`team.level.${area}`), level: String(level) })}>
            <span class="meta">{tr.t(`team.level.${area}`)}</span>
            <span class="pips" aria-hidden="true">{#each [1, 2, 3, 4, 5] as pip (pip)}<i class:on={level !== null && pip <= level}></i>{/each}</span>
          </span>
        {/each}
      </span>
    {/if}
    <span class="tc-block">
      <span class="meta">{tr.t('team.card.drivers')}</span>
      {#if race.length > 0}
        <span class="tc-drivers">
          {#each race as driver (driver.name)}
            <span class="tc-driver">
              {#if hasFlag(driver.nationality)}<Flag code={driver.nationality} />{/if}
              <b>{driver.name}</b>
              <small class="num" title={countryName(tr, driver.nationality)}>{tr.t('team.card.age', { age: String(driver.age) })}</small>
            </span>
          {/each}
        </span>
      {:else}
        <span class="muted">{tr.t('team.card.noRace')}</span>
      {/if}
      {#if reserves.length > 0}
        <span class="muted tc-more" title={reserves.map((driver) => driver.name).join(', ')}>{tr.t('team.card.reserve', { count: String(reserves.length) })}</span>
      {/if}
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
    style="--c1:{colours.main};--c2:{colours.accent};--con:{colours.on};--con2:{colours.onAccent}"
    onclick={() => onPick(team.id)}
  >{@render body()}</button>
{:else}
  <div class="tcard" class:wide style="--c1:{colours.main};--c2:{colours.accent};--con:{colours.on};--con2:{colours.onAccent}">{@render body()}</div>
{/if}
