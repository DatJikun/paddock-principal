<script lang="ts">
  import type { SquadData } from '../lib/screens';
  import AttrRow from '../lib/components/AttrRow.svelte';
  import Stars from '../lib/components/Stars.svelte';
  import ContractEnd from '../lib/components/ContractEnd.svelte';
  import Nationality from '../lib/components/Nationality.svelte';
  import { bandOf, careerTotals, DRIVER_ATTRS, seasonRow } from '../lib/people.mjs';
  import { driverHref, formatAge } from '../lib/person.mjs';
  import { initials, type Tr } from '../lib/ui';

  let { data, tr, today }: { data: SquadData; tr: Tr; today: string } = $props();

  let rows = $derived(
    data.drivers.own.map((driver) => {
      const view = data.profiles.find((item) => item.personId === driver.personId) ?? null;
      return { driver, view, season: view ? seasonRow(view, data.season) : null, career: view ? careerTotals(view) : null };
    }),
  );
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.drivers')}</h1>
  <div class="fields">
    <div class="fld"><span class="meta">{tr.t('drivers.count')}</span><span class="v num">{rows.length}</span></div>
  </div>
</div>

<div class="squad-cards">
  {#each rows as row (row.driver.personId)}
    <a class="panel dcard" href={driverHref(row.driver.personId)}>
      <div class="dcard-top">
        <span class="av-big">{initials(row.driver.name)}</span>
        <div class="dcard-name">
          <h2>{row.driver.name}</h2>
          <div class="fields mid">
            <div class="fld">
              <span class="meta">{tr.t('career.you.country')}</span>
              <span class="v"><Nationality {tr} code={row.driver.nationality} /></span>
            </div>
            <div class="fld"><span class="meta">{tr.t('drivers.age')}</span><span class="v num">{formatAge(row.view?.age)}</span></div>
            <div class="fld"><span class="meta">{tr.t('drivers.seat')}</span><span class="v">{tr.t(`seat.${row.driver.seat}`)}</span></div>
            {#if row.view?.overall != null}
              <div class="fld"><span class="meta">{tr.t('shell.col.overall')}</span><span class="v"><Stars overall={row.view.overall} /></span></div>
            {/if}
            <div class="fld">
              <span class="meta">{tr.t('drivers.contract')}</span>
              <span class="v"><ContractEnd {tr} end={row.view?.contractEnd ?? row.driver.end} {today} quiet={!!row.view?.upcoming} /></span>
            </div>
          </div>
        </div>
      </div>

      {#if row.career && row.career.starts > 0}
        <div class="fields boxed eq center">
          <div class="fld"><span class="meta">{tr.t('drivers.starts')}</span><span class="v num">{row.career.starts}</span></div>
          <div class="fld"><span class="meta">{tr.t('shell.col.wins')}</span><span class="v num">{row.career.wins}</span></div>
          <div class="fld"><span class="meta">{tr.t('drivers.podiums')}</span><span class="v num">{row.career.podiums}</span></div>
          <div class="fld"><span class="meta">{tr.t('driver.best')}</span><span class="v num">{row.career.best ?? '—'}</span></div>
        </div>
      {/if}

      {#if (row.view?.attributes.length ?? 0) > 0}
        <div class="attrs two">
          {#each DRIVER_ATTRS as key (key)}
            {@const band = bandOf(row.view?.attributes, key)}
            {#if band}<AttrRow label={tr.t(`attr.${key}`)} low={band.low} high={band.high} />{/if}
          {/each}
        </div>
      {/if}
    </a>
  {/each}
</div>
