<script lang="ts">
  import { teamLabel } from '../lib/career.mjs';
  import PersonCell from '../lib/components/PersonCell.svelte';
  import Stars from '../lib/components/Stars.svelte';
  import ContractEnd from '../lib/components/ContractEnd.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { formatMoney } from '../lib/money.mjs';
  import { profileHref } from '../lib/person.mjs';
  import type { StaffData } from '../lib/screens';
  import { ageLabel, type Tr } from '../lib/ui';

  let { data, tr, teamId }: { data: StaffData; tr: Tr; teamId: string } = $props();

  let own = $derived(data.staff.people.filter((person) => person.ownTeam));
  /* The key of a row: one person can be on our roster twice for a moment (today's chair and one signed for later). */
  const rowKey = (person: { personId: string; contractStart: string | null }) => `${person.personId}:${person.contractStart ?? ''}`;
  /* The driver column is for race engineers only: they are the ones paired with a driver. */
  let engineers = $derived(own.some((person) => person.role === 'RaceEngineer'));

  const driverName = (id: string | null) => data.drivers.own.find((driver) => driver.personId === id)?.name ?? null;
  const open = (id: string) => (location.hash = profileHref('staff', id));
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
          <th class="c">{tr.t('offer.salary')}</th>
          {#if engineers}<th>{tr.t('staff.driver')}</th><th class="c">{tr.t('staff.relationship')}</th>{/if}
          <th class="c">{tr.t('driver.contract.until')}</th>
        </tr>
      </thead>
      <tbody>
        {#each own as person (rowKey(person))}
          <tr class="go-row" onclick={() => open(person.personId)}>
            <td><PersonCell name={person.name} href={profileHref('staff', person.personId)} nationality={person.nationality} sub={ageLabel(tr, person.age)} /></td>
            <td>{tr.t(`staff.role.${person.role}`)}</td>
            <td class="c"><Stars overall={person.overall} /></td>
            <td class="c num">{person.salary > 0 ? formatMoney(person.salary * 100, tr.lang) : '—'}</td>
            {#if engineers}
              <td>{#if person.role === 'RaceEngineer' && person.driverId}{driverName(person.driverId) ?? person.driverId}{/if}</td>
              <td class="c num">{person.role === 'RaceEngineer' && person.relationship !== null ? person.relationship : ''}</td>
            {/if}
            <td class="c">
              {#if person.contractStart && person.contractStart > data.today}
                <span class="num">{tr.t('driver.contract.from')} {formatDate(person.contractStart, tr.lang)}</span>
              {:else if person.contractEnd}<ContractEnd {tr} end={person.contractEnd} today={data.today} />{/if}
            </td>
          </tr>
        {/each}
      </tbody>
    </table>
  </div>
</section>
