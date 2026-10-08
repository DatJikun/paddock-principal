<script lang="ts">
  import { teamLabel } from '../lib/career.mjs';
  import PersonCell from '../lib/components/PersonCell.svelte';
  import Stars from '../lib/components/Stars.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { endsThisSeason } from '../lib/people.mjs';
  import type { StaffData } from '../lib/screens';
  import type { Tr } from '../lib/ui';

  let { data, tr, teamId }: { data: StaffData; tr: Tr; teamId: string } = $props();

  let own = $derived(data.staff.people.filter((person) => person.ownTeam));
  /* The driver column is for race engineers only: they are the ones paired with a driver. */
  let engineers = $derived(own.some((person) => person.role === 'RaceEngineer'));

  const driverName = (id: string | null) => data.drivers.own.find((driver) => driver.personId === id)?.name ?? null;
  const open = (id: string) => (location.hash = `#/osoba/${encodeURIComponent(id)}`);
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.staff')}</h1>
  <div class="fields">
    <div class="fld"><span class="meta">{tr.t('staff.key')}</span><span class="v num">{own.length}</span></div>
  </div>
  <div class="tools">
    <a class="btn primary" href="#/rynek">{tr.t('staff.hire')}</a>
  </div>
</div>

<section class="panel tbl staff-own">
  <header><h2>{teamLabel(teamId)}</h2></header>
  <div class="tbl-scroll">
    <table class="table fit">
      <thead>
        <tr>
          <th>{tr.t('staff.person')}</th>
          <th>{tr.t('staff.role')}</th>
          <th class="c">{tr.t('shell.col.overall')}</th>
          {#if engineers}<th>{tr.t('staff.driver')}</th>{/if}
          <th class="c">{tr.t('driver.contract.until')}</th>
        </tr>
      </thead>
      <tbody>
        {#each own as person (person.personId)}
          <tr class="go-row" onclick={() => open(person.personId)}>
            <td><PersonCell name={person.name} href={`#/osoba/${encodeURIComponent(person.personId)}`} nationality={person.nationality} sub={tr.t('team.card.age', { age: String(person.age) })} /></td>
            <td>{tr.t(`staff.role.${person.role}`)}</td>
            <td class="c"><Stars overall={person.overall} /></td>
            {#if engineers}
              <td>
                {#if person.role === 'RaceEngineer' && person.driverId}
                  {driverName(person.driverId) ?? person.driverId}{#if person.relationship !== null}<small class="muted num"> {person.relationship}</small>{/if}
                {/if}
              </td>
            {/if}
            <td class="c num" class:bad={endsThisSeason(person.contractEnd, data.today)}>{person.contractEnd ? formatDate(person.contractEnd, tr.lang) : ''}</td>
          </tr>
        {/each}
      </tbody>
    </table>
  </div>
</section>
