<script lang="ts">
  import type { MarketPersonView } from '../lib/api/types.generated';
  import PersonCell from '../lib/components/PersonCell.svelte';
  import Status from '../lib/components/Status.svelte';
  import Tabs from '../lib/components/Tabs.svelte';
  import { formatDate, formatDay } from '../lib/date.mjs';
  import { sortRows } from '../lib/people.mjs';
  import type { MarketData } from '../lib/screens';
  import type { Tr } from '../lib/ui';

  let { data, tr }: { data: MarketData; tr: Tr } = $props();

  type Key = 'name' | 'age' | 'team' | 'seat' | 'end';

  let filter = $state('free');
  let sortKey = $state<Key>('name');
  let direction = $state<'asc' | 'desc'>('asc');

  let all = $derived([...data.market.freeAgents, ...data.market.contracted]);
  let shown = $derived(filter === 'free' ? data.market.freeAgents : filter === 'contracted' ? data.market.contracted : all);
  let rows = $derived(
    sortRows(shown, (person: MarketPersonView) => {
      switch (sortKey) {
        case 'age':
          return person.age;
        case 'team':
          return person.organizationName;
        case 'seat':
          return person.seat ? tr.t(`seat.${person.seat}`) : null;
        case 'end':
          return person.contractEnd;
        default:
          return person.name;
      }
    }, direction),
  );
  /* A list of free agents has no team, seat or contract to show, so those columns only appear next to someone who has them. */
  let employed = $derived(rows.some((person) => !person.freeAgent));
  let open = $derived(data.negotiations.items.filter((item) => !['Agreed', 'Refused', 'WalkedAway', 'Lost', 'Lapsed'].includes(item.status)));

  function sortBy(key: Key) {
    if (sortKey === key) direction = direction === 'asc' ? 'desc' : 'asc';
    else {
      sortKey = key;
      direction = 'asc';
    }
  }

  const sortClass = (key: Key) => (sortKey === key ? `sorted ${direction}` : '');
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.market')}</h1>
  <div class="fields">
    <div class="fld"><span class="meta">{tr.t('market.free')}</span><span class="v num">{data.market.freeAgents.length}</span></div>
    <div class="fld"><span class="meta">{tr.t('market.talks')}</span><span class="v num">{open.length}</span></div>
  </div>
  <div class="tools">
    <Tabs
      group="market-filter"
      items={[
        { value: 'free', label: tr.t('market.filter.free') },
        { value: 'contracted', label: tr.t('market.filter.contracted') },
        { value: 'all', label: tr.t('market.filter.all') },
      ]}
      bind:value={filter}
    />
  </div>
</div>

<div class="market-wrap">
  <section class="panel tbl market">
    <table class="table">
      <thead>
        <tr>
          <th data-sort class={sortClass('name')} onclick={() => sortBy('name')}><span>{tr.t('shell.col.driver')}</span></th>
          <th data-sort class={`c ${sortClass('age')}`} onclick={() => sortBy('age')}><span>{tr.t('drivers.age')}</span></th>
          {#if employed}
            <th data-sort class={sortClass('team')} onclick={() => sortBy('team')}><span>{tr.t('shell.col.team')}</span></th>
            <th data-sort class={sortClass('seat')} onclick={() => sortBy('seat')}><span>{tr.t('drivers.seat')}</span></th>
            <th data-sort class={`c ${sortClass('end')}`} onclick={() => sortBy('end')}><span>{tr.t('drivers.contract')}</span></th>
          {/if}
        </tr>
      </thead>
      <tbody>
        {#each rows as person (person.personId)}
          <tr class="go-row" onclick={() => (location.hash = `#/kierowca/${encodeURIComponent(person.personId)}`)}>
            <td><PersonCell name={person.name} href={`#/kierowca/${encodeURIComponent(person.personId)}`} nationality={person.nationality} /></td>
            <td class="c num">{person.age}</td>
            {#if employed}
              <td>{#if person.freeAgent}<Status text={tr.t('driver.free')} tone="hi" />{:else}{person.organizationName ?? ''}{/if}</td>
              <td>{person.seat ? tr.t(`seat.${person.seat}`) : ''}</td>
              <td class="c num">{person.contractEnd ? formatDate(person.contractEnd, tr.lang) : ''}</td>
            {/if}
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
            <PersonCell name={item.personName} nationality={item.nationality} sub={tr.t('negotiation.roundOf', { used: String(item.roundsUsed), max: String(item.maxRounds) })} />
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
