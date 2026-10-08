<script lang="ts">
  import { untrack } from 'svelte';
  import type { BridgeCommandName, ObjectiveItemView, SponsorMarketRow, SponsorQuoteView } from '../lib/api/types.generated';
  import Confirmation from '../lib/components/Confirmation.svelte';
  import SponsorExtras from '../lib/components/SponsorExtras.svelte';
  import SponsorOffer from '../lib/components/SponsorOffer.svelte';
  import SponsorTalk from '../lib/components/SponsorTalk.svelte';
  import Status from '../lib/components/Status.svelte';
  import TermsPicker from '../lib/components/TermsPicker.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { formatMoney } from '../lib/money.mjs';
  import { sortRows } from '../lib/people.mjs';
  import type { SponsorData } from '../lib/screens';
  import type { Tr } from '../lib/ui';

  let {
    data,
    tr,
    teamId,
    busy,
    act,
  }: {
    data: SponsorData;
    tr: Tr;
    teamId: string;
    busy: boolean;
    act: (name: BridgeCommandName, args: Record<string, unknown>) => Promise<boolean>;
  } = $props();

  type Key = 'name' | 'industry' | 'slot' | 'value';

  let view = $derived('slots' in data.sponsors ? data.sponsors : null);
  let unknown = $derived('reason' in data.sponsors ? data.sponsors.reason : null);

  let yearly = $derived(view ? view.deals.reduce((sum, deal) => sum + deal.annualCents, 0) : 0);
  const objectiveOf = (id: string | null): ObjectiveItemView | null => view?.objectives.find((item) => item.id === id) ?? null;

  /* The market: one list, sortable by what a person compares sponsors by. The best-paying come first until the player chooses otherwise. */
  let sortKey = $state<Key>('value');
  let direction = $state<'asc' | 'desc'>('desc');
  const sortValue = (row: SponsorMarketRow) =>
    sortKey === 'name' ? row.sponsorName : sortKey === 'industry' ? tr.tMsg(row.industry) : sortKey === 'slot' ? row.slot : row.indicativeAnnualCents;
  let rows = $derived(view ? sortRows(view.market, sortValue, direction) : []);
  const sortClass = (key: Key) => (sortKey === key ? `sorted ${direction}` : '');
  function sortBy(key: Key) {
    if (sortKey === key) direction = direction === 'asc' ? 'desc' : 'asc';
    else {
      sortKey = key;
      direction = key === 'value' ? 'desc' : 'asc';
    }
  }

  let picked = $state('');
  let years = $state('1');
  let ambition = $state('standard');
  let asking = $state(false);
  let current = $derived(rows.find((row) => row.sponsorId === picked) ?? rows[0] ?? null);
  let quote = $derived.by(() => {
    const row = current;
    return row?.quotes.find((item: SponsorQuoteView) => String(item.years) === years && item.ambition === (row.ambitionOpen ? ambition : 'standard')) ?? null;
  });

  /* The terms belong to the sponsor on show: another sponsor starts from the standard one-year deal. */
  $effect(() => {
    current?.sponsorId;
    untrack(() => {
      years = '1';
      ambition = 'standard';
      asking = false;
    });
  });

  const termsText = () =>
    current?.ambitionOpen
      ? tr.t('sponsor.terms.summary', { years: tr.t(`sponsor.terms.years.${years}`), level: tr.t(`sponsor.ambition.${ambition}`).toLowerCase() })
      : tr.t(`sponsor.terms.years.${years}`);

  async function begin() {
    const row = current;
    asking = false;
    if (!row) return;
    if (await act('beginSponsorTalks', { organizationId: teamId, sponsorId: row.sponsorId, slot: row.slot, years: Number(years), ambition: row.ambitionOpen ? ambition : 'standard' })) picked = '';
  }
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.sponsors')}</h1>
  {#if view}
    <div class="fields">
      <div class="fld"><span class="meta">{tr.t('sponsor.yearly')}</span><span class="v num good">{formatMoney(yearly, tr.lang)}</span></div>
      <div class="fld"><span class="meta">{tr.t('sponsor.slots')}</span><span class="v num">{view.slots.filter((slot) => slot.dealId).length} / {view.slots.length}</span></div>
    </div>
  {/if}
</div>

{#if unknown}
  <Status text={tr.tMsg(unknown)} tone="warn" />
{:else if view}
  {#if view.offers.length > 0}
    <section class="panel sp-offers">
      <header><h2>{tr.t('sponsor.offers')}</h2></header>
      <div class="body">
        {#each view.offers as offer (offer.id)}
          <SponsorOffer {offer} {tr} {teamId} {busy} {act} />
        {/each}
      </div>
    </section>
  {/if}

  <div class="spon-grid">
    {#each view.slots as slot (slot.slot)}
      {@const deal = view.deals.find((item) => item.id === slot.dealId) ?? null}
      {@const talk = view.talks.find((item) => item.id === slot.talkId) ?? null}
      <section class="panel spon">
        <header><span class="meta">{tr.tMsg(slot.kind)}</span>{#if deal}<Status text={tr.t('sponsor.signed')} tone="good" />{:else if talk}<Status text={tr.t('sponsor.talking')} tone="hi" />{:else}<Status text={tr.t('sponsor.free')} />{/if}</header>
        <div class="body">
          {#if deal}
            {@const goal = objectiveOf(deal.objectiveId)}
            <h3>{deal.sponsorName}</h3>
            <span class="muted">{tr.tMsg(deal.industry)}</span>
            <div class="fields mid">
              <div class="fld"><span class="meta">{tr.t('sponsor.amount')}</span><span class="v num">{formatMoney(deal.annualCents, tr.lang)}</span></div>
              <div class="fld"><span class="meta">{tr.t('sponsor.length')}</span><span class="v">{tr.t(`sponsor.terms.years.${deal.years}`)}</span></div>
              <div class="fld"><span class="meta">{tr.t('driver.contract.until')}</span><span class="v num" class:bad={deal.end.slice(0, 4) <= data.today.slice(0, 4)}>{formatDate(deal.end, tr.lang)}</span></div>
              <div class="fld"><span class="meta">{tr.t('sponsor.trust')}</span><span class="v num">{deal.trust}</span></div>
            </div>
            {#if goal}
              <div class="sp-goal">
                <span class="meta">{tr.t('sponsor.goal')}</span>
                <b>{tr.tMsg(goal.requirement)}</b>
                <small class="muted">{tr.tMsg(goal.onMet)}</small>
                {#if goal.forecast}<Status text={tr.tMsg(goal.forecast.message)} />{/if}
              </div>
            {/if}
            <SponsorExtras {tr} wish={deal.wish} industry={deal.industryBonus} />
          {:else if talk}
            <SponsorTalk {talk} {tr} {teamId} {busy} {act} />
          {/if}
        </div>
      </section>
    {/each}
  </div>

  {#if rows.length > 0}
    <div class="acad cand">
      <section class="panel tbl market">
        <header><h2>{tr.t('sponsor.market')}</h2></header>
        <table class="table tight">
          <thead>
            <tr>
              <th data-sort class={sortClass('name')} onclick={() => sortBy('name')}><span>{tr.t('sponsor.company')}</span></th>
              <th data-sort class={sortClass('industry')} onclick={() => sortBy('industry')}><span>{tr.t('sponsor.industry')}</span></th>
              <th data-sort class={sortClass('slot')} onclick={() => sortBy('slot')}><span>{tr.t('sponsor.col.slot')}</span></th>
              <th data-sort class={`r ${sortClass('value')}`} onclick={() => sortBy('value')}><span>{tr.t('sponsor.col.value')}</span></th>
            </tr>
          </thead>
          <tbody>
            {#each rows as row (row.sponsorId)}
              <tr class="go-row" class:sel={row.sponsorId === current?.sponsorId} onclick={() => (picked = row.sponsorId)}>
                <td><b>{row.sponsorName}</b></td>
                <td class="muted">{tr.tMsg(row.industry)}</td>
                <td class="muted">{tr.tMsg(row.kind)}</td>
                <td class="r num">{formatMoney(row.indicativeAnnualCents, tr.lang)}</td>
              </tr>
            {/each}
          </tbody>
        </table>
      </section>

      <section class="panel acad-detail">
        {#if current}
          <header><h2>{current.sponsorName}</h2><Status text={tr.tMsg(current.industry)} /></header>
          <div class="body">
            {#if current.blocked}
              <Status text={tr.tMsg(current.blocked)} tone="bad" />
            {:else}
              <TermsPicker {tr} group="market" quotes={current.quotes} ambitionOpen={current.ambitionOpen} bind:years bind:ambition />
              <SponsorExtras {tr} wish={current.wish} industry={current.industryBonus} />
              {#if asking}
                <Confirmation
                  {tr}
                  {busy}
                  ask={tr.t('sponsor.ask.begin', { name: current.sponsorName, terms: termsText() })}
                  onCancel={() => (asking = false)}
                  onConfirm={begin}
                />
              {:else}
                <div class="confirm">
                  <button class="btn primary" type="button" disabled={busy || quote === null} onclick={() => (asking = true)}>{tr.t('sponsor.begin')}</button>
                </div>
              {/if}
            {/if}
          </div>
        {/if}
      </section>
    </div>
  {/if}
{/if}
