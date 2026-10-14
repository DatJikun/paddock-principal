<script lang="ts">
  import type { MarketPersonView } from '../lib/api/types.generated';
  import PersonCell from '../lib/components/PersonCell.svelte';
  import Stars from '../lib/components/Stars.svelte';
  import Status from '../lib/components/Status.svelte';
  import Tabs from '../lib/components/Tabs.svelte';
  import ContractEnd from '../lib/components/ContractEnd.svelte';
  import { formatDay } from '../lib/date.mjs';
  import { formatMoney } from '../lib/money.mjs';
  import { sortRows } from '../lib/people.mjs';
  import { formatAge, profileHref, subjectKind } from '../lib/person.mjs';
  import type { MarketData } from '../lib/screens';
  import type { Tr } from '../lib/ui';

  let { data, tr }: { data: MarketData; tr: Tr } = $props();

  type Key = 'name' | 'age' | 'overall' | 'salary' | 'end' | 'team';

  /* "All" first, "Free" next to it; drivers and staff are two lists of one market. */
  let filter = $state('all');
  let kind = $state('driver');
  let sortKey = $state<Key>('overall');
  let direction = $state<'asc' | 'desc'>('desc');

  let all = $derived([...data.market.freeAgents, ...data.market.contracted]);
  let ofKind = $derived(all.filter((person) => (kind === 'driver' ? person.kind === 'driver' : person.kind !== 'driver')));
  let shown = $derived(filter === 'free' ? ofKind.filter((person) => person.freeAgent) : ofKind);
  let rows = $derived(
    sortRows(shown, (person: MarketPersonView) => {
      switch (sortKey) {
        case 'age':
          return person.age;
        case 'overall':
          return person.overall;
        case 'salary':
          return person.salary;
        case 'team':
          return person.organizationName ?? (person.freeAgent ? '' : null);
        case 'end':
          return person.contractEnd;
        default:
          return person.name;
      }
    }, direction),
  );
  let open = $derived(data.negotiations.items.filter((item) => !['Agreed', 'Refused', 'WalkedAway', 'Lost', 'Lapsed'].includes(item.status)));
  let today = $derived(data.today);

  function sortBy(key: Key) {
    if (sortKey === key) direction = direction === 'asc' ? 'desc' : 'asc';
    else {
      sortKey = key;
      direction = key === 'name' || key === 'team' || key === 'end' ? 'asc' : 'desc';
    }
  }

  const sortClass = (key: Key) => (sortKey === key ? `sorted ${direction}` : '');
  const href = (person: MarketPersonView) => profileHref(person.kind === 'driver' ? 'driver' : 'staff', person.personId);
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.market')}</h1>
  <div class="fields">
    <div class="fld"><span class="meta">{tr.t('market.free')}</span><span class="v num">{ofKind.filter((person) => person.freeAgent).length}</span></div>
    <div class="fld"><span class="meta">{tr.t('market.talks')}</span><span class="v num">{open.length}</span></div>
  </div>
  <div class="tools">
    <Tabs
      group="market-kind"
      items={[
        { value: 'driver', label: tr.t('market.kind.driver') },
        { value: 'staff', label: tr.t('market.kind.staff') },
      ]}
      bind:value={kind}
    />
    <Tabs
      group="market-filter"
      items={[
        { value: 'all', label: tr.t('market.filter.all') },
        { value: 'free', label: tr.t('market.filter.free') },
      ]}
      bind:value={filter}
    />
  </div>
</div>

<div class="market-wrap">
  <section class="panel tbl market">
    <table class="table fit">
      <thead>
        <tr>
          <th data-sort class={sortClass('name')} onclick={() => sortBy('name')}><span>{tr.t(kind === 'driver' ? 'shell.col.driver' : 'staff.person')}</span></th>
          {#if kind === 'staff'}<th>{tr.t('staff.role')}</th>{/if}
          <th data-sort class={`c ${sortClass('age')}`} onclick={() => sortBy('age')}><span>{tr.t('drivers.age')}</span></th>
          <th data-sort class={`c ${sortClass('overall')}`} onclick={() => sortBy('overall')}><span>{tr.t('shell.col.overall')}</span></th>
          <th data-sort class={`c ${sortClass('salary')}`} onclick={() => sortBy('salary')}><span>{tr.t('market.salary')}</span></th>
          <th data-sort class={`c ${sortClass('end')}`} onclick={() => sortBy('end')}><span>{tr.t('drivers.contract')}</span></th>
          <th data-sort class={sortClass('team')} onclick={() => sortBy('team')}><span>{tr.t('shell.col.team')}</span></th>
        </tr>
      </thead>
      <tbody>
        {#each rows as person (person.personId)}
          <tr class="go-row" onclick={() => (location.hash = href(person))}>
            <td><PersonCell name={person.name} href={href(person)} nationality={person.nationality} /></td>
            {#if kind === 'staff'}<td>{tr.t(`staff.role.${person.kind}`)}</td>{/if}
            <td class="c num">{formatAge(person.age)}</td>
            <td class="c"><Stars overall={person.overall} /></td>
            <td class="c num">{person.salary > 0 ? formatMoney(person.salary * 100, tr.lang) : '—'}</td>
            <td class="c">{#if person.contractEnd && !person.freeAgent}<ContractEnd {tr} end={person.contractEnd} {today} />{/if}</td>
            <td>{#if person.freeAgent}<Status text={tr.t('driver.free')} tone="hi" />{:else}{person.organizationName ?? ''}{/if}</td>
          </tr>
        {/each}
      </tbody>
    </table>
  </section>

  <section class="panel talks">
    <header><h2>{tr.t('market.talks')}</h2></header>
    {#if data.negotiations.items.length > 0}
      <div class="body talk-list">
        {#each data.negotiations.items as item (item.id)}
          <a class="talk" href={`#/negocjacja/${encodeURIComponent(item.id)}`}>
            <PersonCell name={item.personName} href={profileHref(subjectKind(item.subject.kind), item.person)} nationality={item.nationality} sub={tr.t('negotiation.roundOf', { used: String(item.roundsUsed), max: String(item.maxRounds) })} />
            <div class="talk-st">
              <Status text={tr.tMsg(item.statusText)} tone={item.status === 'Agreed' ? 'good' : ['Refused', 'WalkedAway', 'Lost', 'Lapsed'].includes(item.status) ? 'bad' : item.status === 'Countered' || item.status === 'PersonAgreed' ? 'hi' : ''} />
              <small class="muted num">{formatDay(item.deadline, tr.lang)}</small>
            </div>
          </a>
        {/each}
      </div>
    {:else}
      <div class="empty"><Status text={tr.t('market.noTalks')} /></div>
    {/if}
  </section>
</div>
