<script lang="ts">
  import { untrack } from 'svelte';
  import type { BridgeCommandName, SponsorOfferView } from '../api/types.generated';
  import { formatDate } from '../date.mjs';
  import { formatMoney } from '../money.mjs';
  import type { Tr } from '../ui';
  import Confirmation from './Confirmation.svelte';
  import SponsorExtras from './SponsorExtras.svelte';
  import TermsPicker from './TermsPicker.svelte';

  /** A sponsor's offer to renew a deal. The player answers it, or asks other terms while the sponsor still gives rounds. */
  let {
    offer,
    tr,
    teamId,
    busy,
    act,
  }: {
    offer: SponsorOfferView;
    tr: Tr;
    teamId: string;
    busy: boolean;
    act: (name: BridgeCommandName, args: Record<string, unknown>) => Promise<boolean>;
  } = $props();

  type Ask = 'accept' | 'decline' | 'counter';

  let years = $state(untrack(() => String(offer.years)));
  let ambition = $state(untrack(() => offer.ambition));
  let asking = $state<Ask | null>(null);
  let negotiating = $state(false);
  let askMilli = $state('0');

  let changed = $derived(years !== String(offer.years) || (offer.ambitionOpen && ambition !== offer.ambition) || askMilli !== '0');
  let chosen = $derived(offer.quotes.find((item) => String(item.years) === years && item.ambition === (offer.ambitionOpen ? ambition : 'standard')) ?? null);
  const level = (key: string) => tr.t(`sponsor.ambition.${key}`).toLowerCase();
  const summary = (y: string, a: string) =>
    offer.ambitionOpen ? tr.t('sponsor.terms.summary', { years: tr.t(`sponsor.terms.years.${y}`), level: level(a) }) : tr.t(`sponsor.terms.years.${y}`);
  /* The raise over the deal that ends, as the sponsor would say it. Zero when the old amount is unknown. */
  let raise = $derived(offer.previousAnnualCents > 0 ? Math.round((offer.annualCents / offer.previousAnnualCents - 1) * 100) : null);

  let asked = $derived(chosen?.asks.find((item) => String(item.milli) === askMilli) ?? null);

  const askText = (ask: Ask) =>
    ask === 'counter'
      ? tr.t('sponsor.ask.counter', { name: offer.sponsorName, terms: summary(years, ambition), amount: formatMoney(asked?.annualCents ?? chosen?.annualCents ?? 0, tr.lang) })
      : tr.t(ask === 'accept' ? 'sponsor.ask.accept' : 'sponsor.ask.decline', { name: offer.sponsorName });

  async function run() {
    const ask = asking;
    asking = null;
    if (ask === 'counter') {
      if (await act('counterSponsorOffer', { organizationId: teamId, offerId: offer.id, years: Number(years), ambition: offer.ambitionOpen ? ambition : 'standard', askMilli: Number(askMilli) })) {
        negotiating = false;
        askMilli = '0';
      }
    } else if (ask) await act('respondToSponsorOffer', { organizationId: teamId, offerId: offer.id, accept: ask === 'accept' });
  }
</script>

<div class="sp-offer">
  <div class="sp-offer-head">
    <h3>{offer.sponsorName}</h3>
    <div class="fields mid">
      <div class="fld">
        <span class="meta">{tr.t('sponsor.amount')}</span>
        <span class="v num">{formatMoney(offer.annualCents, tr.lang)}{#if raise !== null && raise !== 0}<small class="good">{raise > 0 ? '+' : ''}{raise}%</small>{/if}</span>
      </div>
      {#if offer.previousAnnualCents > 0}
        <div class="fld"><span class="meta">{tr.t('sponsor.was')}</span><span class="v num">{formatMoney(offer.previousAnnualCents, tr.lang)}</span></div>
      {/if}
      <div class="fld"><span class="meta">{tr.t('sponsor.length')}</span><span class="v">{tr.t(`sponsor.terms.years.${offer.years}`)}</span></div>
      {#if offer.ambitionOpen}
        <div class="fld"><span class="meta">{tr.t('sponsor.level')}</span><span class="v">{tr.t(`sponsor.ambition.${offer.ambition}`)}</span></div>
      {/if}
      <div class="fld"><span class="meta">{tr.t('driver.contract.until')}</span><span class="v num">{formatDate(offer.validUntil, tr.lang)}</span></div>
      {#if offer.roundsLeft > 0}
        <div class="fld"><span class="meta">{tr.t('sponsor.rounds')}</span><span class="v num">{offer.roundsLeft}</span></div>
      {/if}
    </div>
  </div>
  <SponsorExtras {tr} wish={offer.wish} industry={offer.industryBonus} />
  {#if negotiating}
    <TermsPicker {tr} group={`offer-${offer.id}`} quotes={offer.quotes} ambitionOpen={offer.ambitionOpen} bind:years bind:ambition partnership={offer.partnership} askable bind:askMilli />
  {/if}
  {#if asking}
    <Confirmation {tr} {busy} ask={askText(asking)} onCancel={() => (asking = null)} onConfirm={run} />
  {:else}
    <div class="ask-btns">
      <button class="btn" type="button" disabled={busy} onclick={() => (asking = 'decline')}>{tr.t('sponsor.decline')}</button>
      {#if offer.roundsLeft > 0 && !negotiating}
        <button class="btn" type="button" disabled={busy} onclick={() => (negotiating = true)}>{tr.t('sponsor.change')}</button>
      {/if}
      {#if negotiating && changed}
        <button class="btn primary" type="button" disabled={busy || chosen === null} onclick={() => (asking = 'counter')}>{tr.t('sponsor.propose')}</button>
      {:else}
        <button class="btn primary" type="button" disabled={busy} onclick={() => (asking = 'accept')}>{tr.t('sponsor.accept')}</button>
      {/if}
    </div>
  {/if}
</div>
