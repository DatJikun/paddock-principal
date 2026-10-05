<script lang="ts">
  import type { SquadData } from '../lib/screens';
  import PersonCell from '../lib/components/PersonCell.svelte';
  import { bandOf, bandText, DRIVER_ATTRS, endsThisSeason, seasonRow } from '../lib/people.mjs';
  import { formatDate } from '../lib/date.mjs';
  import { icon, ICON, type Tr } from '../lib/ui';

  let { data, tr, today }: { data: SquadData; tr: Tr; today: string } = $props();

  let picked = $state<string[]>([]);

  let rows = $derived(
    data.drivers.own.map((driver) => {
      const view = data.profiles.find((item) => item.personId === driver.personId) ?? null;
      return { driver, view, season: view ? seasonRow(view, data.season) : null };
    }),
  );
  /* Ratings are columns only once the team has any: a bridge that knows none (a fresh career) shows no empty grid. */
  let rated = $derived(rows.some((row) => (row.view?.attributes.length ?? 0) > 0));
  let raced = $derived(rows.some((row) => (row.season?.starts ?? 0) > 0));
  let compare = $derived(picked.length === 2 ? `#/porownaj/${encodeURIComponent(picked[0] ?? '')}/${encodeURIComponent(picked[1] ?? '')}` : null);

  function toggle(id: string) {
    if (picked.includes(id)) picked = picked.filter((item) => item !== id);
    else picked = [...picked.slice(-1), id];
  }
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.drivers')}</h1>
  <div class="fields">
    <div class="fld"><span class="meta">{tr.t('drivers.count')}</span><span class="v num">{rows.length}</span></div>
  </div>
  <div class="tools">
    {#if compare}
      <a class="btn" href={compare}>{tr.t('drivers.compare')}</a>
    {:else}
      <button class="btn" type="button" disabled>{tr.t('drivers.compare')}<span class="count">{picked.length}/2</span></button>
    {/if}
    <a class="btn primary" href="#/rynek">{@html icon(ICON.scout, 17)}<span>{tr.t('drivers.market')}</span></a>
  </div>
</div>

<section class="panel tbl">
  <div class="tbl-scroll">
    <table class="table squad">
      <thead>
        <tr>
          <th class="c"><span class="sr">{tr.t('drivers.compare')}</span></th>
          <th>{tr.t('shell.col.driver')}</th>
          <th class="c">{tr.t('drivers.age')}</th>
          <th>{tr.t('drivers.seat')}</th>
          {#if raced}
            <th class="c">{tr.t('drivers.starts')}</th>
            <th class="c">{tr.t('drivers.podiums')}</th>
            <th class="c">{tr.t('shell.col.wins')}</th>
          {/if}
          {#if rated}
            {#each DRIVER_ATTRS as key (key)}<th class="c attr-h">{tr.t(`attr.${key}`)}</th>{/each}
          {/if}
          <th class="c">{tr.t('drivers.contract')}</th>
        </tr>
      </thead>
      <tbody>
        {#each rows as row (row.driver.personId)}
          <tr class:sel={picked.includes(row.driver.personId)}>
            <td class="c">
              <input type="checkbox" class="pick" checked={picked.includes(row.driver.personId)} aria-label={row.driver.name} onchange={() => toggle(row.driver.personId)} />
            </td>
            <td><PersonCell name={row.driver.name} href={`#/kierowca/${encodeURIComponent(row.driver.personId)}`} nationality={row.driver.nationality} /></td>
            <td class="c num">{row.view?.age ?? ''}</td>
            <td>{tr.t(`seat.${row.driver.seat}`)}</td>
            {#if raced}
              <td class="c num">{row.season?.starts || ''}</td>
              <td class="c num">{row.season?.podiums || ''}</td>
              <td class="c num">{row.season?.wins || ''}</td>
            {/if}
            {#if rated}
              {#each DRIVER_ATTRS as key (key)}
                {@const band = bandOf(row.view?.attributes, key)}
                <td class="c num">{band ? bandText(band.low, band.high) : ''}</td>
              {/each}
            {/if}
            <td class="c num" class:bad={endsThisSeason(row.driver.end, today)}>{formatDate(row.driver.end, tr.lang)}</td>
          </tr>
        {/each}
      </tbody>
    </table>
  </div>
</section>
