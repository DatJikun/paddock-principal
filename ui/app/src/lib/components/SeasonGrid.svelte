<script lang="ts">
  import { onMount } from 'svelte';
  import type { OverviewResultView, OverviewRowView, SeasonOverviewView } from '../api/types.generated';
  import { hasFlag } from '../flags.mjs';
  import { livery } from '../livery.mjs';
  import { carsOrdered, resultText, resultTone } from '../overview.mjs';
  import { icon, ICON, points, type Tr } from '../ui';
  import Flag from './Flag.svelte';
  import PersonName from './PersonName.svelte';
  import Tabs from './Tabs.svelte';

  let { overview, tr, teamId, onClose }: { overview: SeasonOverviewView; tr: Tr; teamId: string; onClose: () => void } = $props();

  let table = $state('drivers');
  let closeButton: HTMLButtonElement | undefined = $state();
  let rows = $derived(table === 'constructors' && overview.hasConstructorTitle ? overview.constructors : overview.drivers);
  let isTeams = $derived(table === 'constructors' && overview.hasConstructorTitle);

  onMount(() => {
    closeButton?.focus();
    const leave = () => onClose();
    window.addEventListener('hashchange', leave);
    return () => window.removeEventListener('hashchange', leave);
  });

  function keydown(event: KeyboardEvent) {
    if (event.key === 'Escape') onClose();
  }

  const mine = (row: OverviewRowView) => row.id === teamId || row.teamId === teamId;
  const stripe = (row: OverviewRowView) => livery(isTeams ? row.id : (row.teamId ?? '')).main;
  const cars = (row: OverviewRowView, at: number): OverviewResultView[] => carsOrdered(row.cells[at]?.results ?? []);
</script>

<svelte:window onkeydown={keydown} />

<!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_static_element_interactions -->
<div class="ov-back" onclick={(event) => event.target === event.currentTarget && onClose()}>
  <div class="ov-card" role="dialog" aria-modal="true" aria-label={tr.t('overview.title', { season: String(overview.season) })}>
    <header class="ov-head">
      <h2>{tr.t('overview.title', { season: String(overview.season) })}</h2>
      {#if overview.hasConstructorTitle}
        <Tabs
          group="overview-table"
          items={[
            { value: 'drivers', label: tr.t('shell.standings.drivers') },
            { value: 'constructors', label: tr.t('shell.standings.constructors') },
          ]}
          bind:value={table}
        />
      {/if}
      <button bind:this={closeButton} type="button" class="ov-x" aria-label={tr.t('overview.close')} title={tr.t('overview.close')} onclick={onClose}>{@html icon('<path d="M6 6l12 12M18 6L6 18"/>', 20)}</button>
    </header>
    <div class="ov-scroll">
      <table class="ov">
        <thead>
          <tr>
            <th class="ov-pos">{tr.t('shell.col.position')}</th>
            <th class="ov-name">{isTeams ? tr.t('shell.col.team') : tr.t('shell.col.driver')}</th>
            {#each overview.rounds as round (round.round)}
              <th class="ov-round" class:todo={!round.finished}>
                <a href={`#/wyscig/${round.round}`} title={`${tr.t('overview.round', { round: String(round.round) })} · ${round.circuitName}`}>
                  {#if hasFlag(round.country)}<Flag code={round.country} size="md" />{:else}<span class="ov-code">{round.country}</span>{/if}
                  <span class="num">{round.round}</span>
                </a>
              </th>
            {/each}
            <th class="ov-pts">{tr.t('shell.col.points')}</th>
          </tr>
        </thead>
        <tbody>
          {#each rows as row (row.id)}
            <tr class:mine={mine(row)}>
              <td class="ov-pos num">{row.position > 0 ? row.position : ''}</td>
              <td class="ov-name" style={`--stripe:${stripe(row)}`}>
                <span class="ov-who">
                  {#if isTeams}<b>{row.name}</b>{:else}<PersonName name={row.name} id={row.id} nationality={row.nationality} />{/if}
                </span>
                {#if !isTeams && row.teamName}<small>{row.teamName}</small>{/if}
              </td>
              {#each overview.rounds as round, at (round.round)}
                {@const results = cars(row, at)}
                <td class="ov-cell">
                  {#if results.length > 0}
                    <span class="ov-cars">
                      {#each results as result, index (index)}
                        <span class="ov-res tone-{resultTone(result)}">{resultText(result, tr.t('overview.ret'))}</span>
                      {/each}
                    </span>
                  {/if}
                </td>
              {/each}
              <td class="ov-pts num"><b>{points(tr, row.points)}</b></td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  </div>
</div>
