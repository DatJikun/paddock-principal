<script lang="ts">
  import type { BridgeCommandName } from '../lib/api/types.generated';
  import { teamLabel } from '../lib/career.mjs';
  import AttrRow from '../lib/components/AttrRow.svelte';
  import Confirmation from '../lib/components/Confirmation.svelte';
  import Flag from '../lib/components/Flag.svelte';
  import Status from '../lib/components/Status.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { hasFlag } from '../lib/flags.mjs';
  import { formatMoney } from '../lib/money.mjs';
  import { endsThisSeason } from '../lib/people.mjs';
  import type { StaffData } from '../lib/screens';
  import { countryName, icon, ICON, initials, type Tr } from '../lib/ui';

  let {
    data,
    tr,
    id,
    teamId,
    busy,
    act,
  }: {
    data: StaffData;
    tr: Tr;
    id: string;
    teamId: string;
    busy: boolean;
    act: (name: BridgeCommandName, args: Record<string, unknown>) => Promise<boolean>;
  } = $props();

  let asking = $state(false);

  /* A person of the team or of a rival comes from the staff list; a free agent or someone on another roster, from the market. */
  let listed = $derived(data.staff.people.find((item) => item.personId === id) ?? null);
  let market = $derived([...data.market.freeAgents, ...data.market.contracted].find((item) => item.personId === id) ?? null);
  let person = $derived(
    listed
      ? {
          name: listed.name,
          nationality: listed.nationality,
          age: listed.age,
          role: listed.role,
          teamName: teamLabel(listed.organizationId),
          own: listed.ownTeam,
          free: false,
          overall: listed.overall,
          attributes: listed.attributes,
          contractEnd: listed.contractEnd,
          salary: listed.salary,
          expected: 0,
          female: listed.female,
        }
      : market && market.kind !== 'driver'
        ? {
            name: market.name,
            nationality: market.nationality,
            age: market.age,
            role: market.kind,
            teamName: market.organizationName,
            own: false,
            free: market.freeAgent,
            overall: market.overall,
            attributes: market.attributes.length > 0 ? market.attributes : null,
            contractEnd: market.contractEnd,
            salary: 0,
            expected: market.expectedSalary,
            female: market.female,
          }
        : null,
  );
  let driver = $derived(listed?.driverId ? (data.drivers.own.find((item) => item.personId === listed.driverId) ?? null) : null);
  let talks = $derived(data.negotiations?.items?.find((item) => item.person === id) ?? null);

  async function talk() {
    asking = false;
    if (person && (await act('openNegotiation', { organizationId: teamId, personId: id, subject: `staff:${person.role}`, deadline: null }))) {
      location.hash = '#/rynek';
    }
  }
</script>

{#if !person}
  <div class="screen-head"><h1 class="screen">{tr.t('shell.nav.staff')}</h1></div>
  <Status text={tr.t('staff.unknown')} tone="warn" />
{:else}
  <div class="profile">
    <section class="panel hero staff">
      <div class="num-big"><span>{initials(person.name)}</span></div>
      <div class="hero-main">
        <h1 class="screen">{person.name}</h1>
      </div>
      <div class="hero-side">
        <div class="tools">
          {#if !person.own}
            <button class="btn primary" type="button" disabled={busy || asking} onclick={() => (asking = true)}>{tr.t('driver.talk')}</button>
          {/if}
          <a class="btn" href={person.own ? '#/personel' : '#/rynek'}>{@html icon(ICON.back, 17)}<span>{person.own ? tr.t('shell.nav.staff') : tr.t('shell.nav.market')}</span></a>
        </div>
      </div>
    </section>

    {#if asking}
      <Confirmation {tr} {busy} ask={tr.t('driver.talk.ask', { name: person.name })} onCancel={() => (asking = false)} onConfirm={talk} />
    {/if}

    <div class="info-panels">
      <section class="panel"><div class="body"><span class="meta">{tr.t('career.you.country')}</span><b class="v">{#if hasFlag(person.nationality)}<Flag code={person.nationality} size="md" />{/if}{countryName(tr, person.nationality)}</b></div></section>
      <section class="panel"><div class="body"><span class="meta">{tr.t('drivers.age')}</span><b class="v num">{person.age}</b></div></section>
      <section class="panel"><div class="body"><span class="meta">{tr.t('staff.role')}</span><b class="v">{tr.t(`staff.role.${person.role}`)}</b></div></section>
      <section class="panel"><div class="body"><span class="meta">{tr.t('shell.col.team')}</span><b class="v">{#if person.free}<Status text={tr.t('driver.free')} tone="hi" />{:else}{person.teamName ?? '—'}{/if}</b></div></section>
      {#if person.overall !== null}
        <section class="panel"><div class="body"><span class="meta">{tr.t('shell.col.overall')}</span><b class="v num">{person.overall}</b></div></section>
      {/if}
    </div>

    <div class="prof-grid s">
      <section class="panel">
        <header><h2>{tr.t('driver.attributes')}</h2></header>
        <div class="body">
          {#if person.attributes}
            <div class="attrs">
              {#each person.attributes as attribute (attribute.key)}
                <AttrRow thick label={tr.t(`attr.${attribute.key}`)} low={attribute.low} high={attribute.high} />
              {/each}
            </div>
          {:else}
            <Status text={tr.t('driver.noRatings')} />
          {/if}
        </div>
      </section>
      <div class="col">
        <section class="panel">
          <header><h2>{tr.t('driver.contract')}</h2></header>
          <div class="body">
            <div class="fields">
              {#if person.contractEnd}
                <div class="fld"><span class="meta">{person.free ? tr.t('market.freeSince') : tr.t('driver.contract.until')}</span><span class="v num" class:bad={person.own && endsThisSeason(person.contractEnd, data.today)}>{formatDate(person.contractEnd, tr.lang)}</span></div>
              {/if}
              {#if person.salary > 0}
                <div class="fld"><span class="meta">{tr.t('offer.salary')}</span><span class="v num">{formatMoney(person.salary * 100, tr.lang)}</span></div>
              {/if}
              {#if person.expected > 0}
                <div class="fld"><span class="meta">{tr.t('market.expected')}</span><span class="v num">{formatMoney(person.expected * 100, tr.lang)}</span></div>
              {/if}
            </div>
            {#if talks}<a class="btn" href={`#/negocjacja/${encodeURIComponent(talks.id)}`}>{tr.t('driver.talks')}</a>{/if}
          </div>
        </section>
        {#if driver && person.role === 'RaceEngineer'}
          <section class="panel">
            <header><h2>{tr.t('staff.driver')}</h2></header>
            <div class="body">
              <div class="fields">
                <div class="fld"><span class="meta">{tr.t('shell.col.driver')}</span><a class="v plain" href={`#/kierowca/${encodeURIComponent(driver.personId)}`}>{driver.name}</a></div>
                {#if listed && listed.relationship !== null}
                  <div class="fld"><span class="meta">{tr.t('staff.relationship')}</span><span class="v num">{listed.relationship}</span></div>
                {/if}
              </div>
            </div>
          </section>
        {/if}
      </div>
    </div>
  </div>
{/if}
