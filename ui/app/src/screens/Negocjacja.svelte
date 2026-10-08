<script lang="ts">
  import type { BridgeCommandName, OfferTerms } from '../lib/api/types.generated';
  import Confirmation from '../lib/components/Confirmation.svelte';
  import OfferForm, { type OfferInput } from '../lib/components/OfferForm.svelte';
  import PersonCell from '../lib/components/PersonCell.svelte';
  import Status from '../lib/components/Status.svelte';
  import { formatDate, formatDay } from '../lib/date.mjs';
  import { formatMoney } from '../lib/money.mjs';
  import type { MarketData } from '../lib/screens';
  import { icon, ICON, type Tr } from '../lib/ui';

  let {
    data,
    tr,
    id,
    busy,
    act,
  }: {
    data: MarketData;
    tr: Tr;
    id: string;
    busy: boolean;
    act: (name: BridgeCommandName, args: Record<string, unknown>) => Promise<boolean>;
  } = $props();

  let item = $derived(data.negotiations.items.find((entry) => entry.id === id) ?? null);
  let asking = $state<'accept' | 'walk' | null>(null);

  const CLOSED = ['Agreed', 'Refused', 'WalkedAway', 'Lost', 'Lapsed'];
  let closed = $derived(item ? CLOSED.includes(item.status) : true);
  let canOffer = $derived(item !== null && (item.status === 'Open' || item.status === 'Countered'));
  let canSign = $derived(item !== null && (item.status === 'Countered' || item.status === 'PersonAgreed'));
  let tone = $derived(
    !item ? '' : item.status === 'Agreed' ? 'good' : ['Refused', 'WalkedAway', 'Lost', 'Lapsed'].includes(item.status) ? 'bad' : canSign ? 'hi' : '',
  );

  function money(value: number) {
    return formatMoney(value * 100, tr.lang);
  }

  function terms(value: OfferTerms) {
    return [
      { label: tr.t('offer.salary'), value: money(value.salary) },
      { label: tr.t('offer.years'), value: String(value.years) },
      { label: tr.t('offer.seat'), value: value.seat ? tr.t(`seat.${value.seat}`) : '—' },
      { label: tr.t('offer.winBonus'), value: money(value.winBonus) },
      { label: tr.t('offer.titleBonus'), value: money(value.titleBonus) },
      { label: tr.t('offer.option'), value: value.option ? `${tr.t(`offer.option.${value.option.holder}`)} +${value.option.extraYears}` : tr.t('offer.option.none') },
      { label: tr.t('offer.exit'), value: value.exit ? `P${value.exit.positionWorseThan}` : '—' },
    ];
  }

  async function send(offer: OfferInput) {
    if (!item) return;
    await act('submitOffer', { negotiationId: item.id, ...offer });
  }

  async function accept() {
    asking = null;
    if (item) await act('acceptCounter', { negotiationId: item.id });
  }

  async function walk() {
    asking = null;
    if (item) await act('walkAway', { negotiationId: item.id });
  }
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.market')}</h1>
  <div class="tools">
    <a class="btn" href="#/rynek">{@html icon(ICON.back, 17)}<span>{tr.t('shell.nav.market')}</span></a>
  </div>
</div>

{#if !item}
  <Status text={tr.t('negotiation.unknown')} tone="warn" />
{:else}
  <div class="neg">
    <section class="panel neg-head">
      <PersonCell name={item.personName} href={`#/${item.subject.kind === 'DriverSeat' ? 'kierowca' : 'osoba'}/${encodeURIComponent(item.person)}`} nationality={item.nationality} />
      <div class="fields">
        <div class="fld"><span class="meta">{tr.t('negotiation.state')}</span><span class="v"><Status text={tr.tMsg(item.statusText)} {tone} /></span></div>
        <div class="fld"><span class="meta">{tr.t('negotiation.interest')}</span><span class="v">{tr.tMsg(item.interest)}</span></div>
        <div class="fld"><span class="meta">{tr.t('negotiation.rounds')}</span><span class="v num">{tr.t('negotiation.roundOf', { used: String(item.roundsUsed), max: String(item.maxRounds) })}</span></div>
        <div class="fld"><span class="meta">{tr.t('negotiation.deadline')}</span><span class="v num" class:bad={!closed}>{formatDay(item.deadline, tr.lang)}</span></div>
        {#if item.startsOn && !closed}
          <div class="fld"><span class="meta">{tr.t('negotiation.startsOn')}</span><span class="v num">{formatDay(item.startsOn, tr.lang)}</span></div>
        {/if}
        {#if item.respondOn}
          <div class="fld"><span class="meta">{tr.t('negotiation.respondOn')}</span><span class="v num">{formatDay(item.respondOn, tr.lang)}</span></div>
        {/if}
      </div>
    </section>

    <div class="neg-grid">
      <div class="col">
        {#if !closed}
          <section class="panel">
            <header><h2>{canOffer ? tr.t('negotiation.yourOffer') : tr.t('negotiation.waiting')}</h2></header>
            <div class="body">
              {#if canOffer}
                <OfferForm {tr} initial={item.counter ?? item.offer} guide={item.salaryGuide} {busy} label={tr.t('negotiation.send')} onSubmit={send} />
              {/if}
              {#if asking === 'accept'}
                <Confirmation {tr} {busy} ask={tr.t('negotiation.accept.ask', { name: item.personName })} onCancel={() => (asking = null)} onConfirm={accept} />
              {:else if asking === 'walk'}
                <Confirmation {tr} {busy} ask={tr.t('negotiation.walk.ask', { name: item.personName })} onCancel={() => (asking = null)} onConfirm={walk} />
              {:else}
                <div class="confirm">
                  <button class="btn" type="button" disabled={busy} onclick={() => (asking = 'walk')}>{tr.t('negotiation.walk')}</button>
                  {#if canSign}
                    <button class="btn primary" type="button" disabled={busy} onclick={() => (asking = 'accept')}>{tr.t(item.status === 'PersonAgreed' ? 'negotiation.sign' : 'negotiation.accept')}</button>
                  {/if}
                </div>
              {/if}
            </div>
          </section>
        {/if}

        {#if item.history.length > 0}
          <section class="panel tbl">
            <header><h2>{tr.t('negotiation.history')}</h2></header>
            <table class="table tight">
              <thead>
                <tr>
                  <th class="c">#</th>
                  <th></th>
                  <th class="c">{tr.t('shell.col.date')}</th>
                  <th class="r">{tr.t('offer.salary')}</th>
                  <th class="c">{tr.t('offer.years')}</th>
                </tr>
              </thead>
              <tbody>
                {#each item.history as round (round.number)}
                  <tr>
                    <td class="c num">{round.number}</td>
                    <td>{tr.t(`negotiation.round.${round.kind}`)}</td>
                    <td class="c num">{formatDate(round.on, tr.lang)}</td>
                    <td class="r num">{round.terms ? money(round.terms.salary) : ''}</td>
                    <td class="c num">{round.terms?.years ?? ''}</td>
                  </tr>
                {/each}
              </tbody>
            </table>
          </section>
        {/if}
      </div>
      <div class="col">
        {#if item.counter}
          <section class="panel">
            <header><h2>{tr.t('negotiation.counter')}</h2></header>
            <div class="body"><div class="fields terms">{#each terms(item.counter) as field (field.label)}<div class="fld"><span class="meta">{field.label}</span><span class="v num">{field.value}</span></div>{/each}</div></div>
          </section>
        {/if}
        {#if item.offer}
          <section class="panel">
            <header><h2>{tr.t('negotiation.offer')}</h2></header>
            <div class="body"><div class="fields terms">{#each terms(item.offer) as field (field.label)}<div class="fld"><span class="meta">{field.label}</span><span class="v num">{field.value}</span></div>{/each}</div></div>
          </section>
        {/if}
        {#if item.reasons.length > 0}
          <section class="panel">
            <header><h2>{tr.t('negotiation.reasons')}</h2></header>
            <div class="body reasons">{#each item.reasons as reason, index (index)}<p>{tr.tMsg(reason)}</p>{/each}</div>
          </section>
        {/if}
        {#if item.signedContract}
          <section class="panel"><div class="body"><Status text={tr.t('negotiation.signed')} tone="good" solid /></div></section>
        {/if}
      </div>

    </div>
  </div>
{/if}
