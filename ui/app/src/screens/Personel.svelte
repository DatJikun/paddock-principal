<script lang="ts">
  import { teamLabel } from '../lib/career.mjs';
  import PersonCell from '../lib/components/PersonCell.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { bandText, endsThisSeason } from '../lib/people.mjs';
  import type { StaffData } from '../lib/screens';
  import type { Tr } from '../lib/ui';

  let { data, tr, teamId }: { data: StaffData; tr: Tr; teamId: string } = $props();

  let own = $derived(data.staff.people.filter((person) => person.ownTeam));
  let rivals = $derived(data.staff.people.filter((person) => !person.ownTeam));
  let teams = $derived([...new Set(rivals.map((person) => person.organizationId))].sort());
  let picked = $state('');
  let team = $derived(teams.includes(picked) ? picked : (teams[0] ?? ''));
  let crew = $derived(rivals.filter((person) => person.organizationId === team));

  const driverName = (id: string | null) => data.drivers.own.find((driver) => driver.personId === id)?.name ?? null;
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.staff')}</h1>
  <div class="fields">
    <div class="fld"><span class="meta">{tr.t('staff.key')}</span><span class="v num">{own.length}</span></div>
  </div>
</div>

<div class="staff-grid">
  <section class="panel tbl">
    <header><h2>{teamLabel(teamId)}</h2></header>
    <div class="tbl-scroll">
      <table class="table">
        <thead>
          <tr>
            <th>{tr.t('staff.person')}</th>
            <th>{tr.t('staff.attributes')}</th>
            <th>{tr.t('staff.driver')}</th>
            <th class="c">{tr.t('driver.contract.until')}</th>
          </tr>
        </thead>
        <tbody>
          {#each own as person (person.personId)}
            <tr>
              <td>
                <PersonCell
                  name={person.name}
                  href={`#/osoba/${encodeURIComponent(person.personId)}`}
                  nationality={person.nationality}
                  sub={`${tr.t(`staff.role.${person.role}`)} · ${tr.t('team.card.age', { age: String(person.age) })}`}
                />
              </td>
              <td>
                <div class="sattrs">
                  {#each person.attributes ?? [] as attribute (attribute.key)}
                    <span>{tr.t(`attr.${attribute.key}`)} <b class="num">{bandText(attribute.low, attribute.high)}</b></span>
                  {/each}
                </div>
              </td>
              <td>
                {#if person.driverId}
                  <a class="plain" href={`#/kierowca/${encodeURIComponent(person.driverId)}`}>{driverName(person.driverId) ?? person.driverId}</a>
                  {#if person.relationship !== null}<small class="muted num"> {person.relationship}</small>{/if}
                {/if}
              </td>
              <td class="c num" class:bad={endsThisSeason(person.contractEnd, data.today)}>{person.contractEnd ? formatDate(person.contractEnd, tr.lang) : ''}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  </section>

  <section class="panel tbl">
    <header>
      <h2>{tr.t('staff.rivals')}</h2>
      <select class="text sm" aria-label={tr.t('staff.rivals')} value={team} onchange={(event) => (picked = event.currentTarget.value)}>
        {#each teams as id (id)}<option value={id}>{teamLabel(id)}</option>{/each}
      </select>
    </header>
    <div class="tbl-scroll">
      <table class="table tight">
        <thead>
          <tr><th>{tr.t('staff.person')}</th><th>{tr.t('staff.role')}</th></tr>
        </thead>
        <tbody>
          {#each crew as person (person.personId)}
            <tr>
              <td><PersonCell name={person.name} href={`#/osoba/${encodeURIComponent(person.personId)}`} nationality={person.nationality} sub={tr.t('team.card.age', { age: String(person.age) })} /></td>
              <td>{tr.t(`staff.role.${person.role}`)}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  </section>
</div>
