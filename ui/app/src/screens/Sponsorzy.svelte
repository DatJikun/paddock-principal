<script lang="ts">
  import type { BridgeCommandName, ObjectiveItemView } from '../lib/api/types.generated';
  import Confirmation from '../lib/components/Confirmation.svelte';
  import Status from '../lib/components/Status.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { formatMoney } from '../lib/money.mjs';
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

  type Ask =
    | { kind: 'begin'; slot: number; sponsorId: string; name: string }
    | { kind: 'sign'; talkId: string; name: string }
    | { kind: 'leave'; talkId: string; name: string }
    | { kind: 'offer'; offerId: string; accept: boolean; name: string };

  let view = $derived('slots' in data.sponsors ? data.sponsors : null);
  let unknown = $derived('reason' in data.sponsors ? data.sponsors.reason : null);
  let asking = $state<Ask | null>(null);
  let listing = $state<number | null>(null);
  let freeSlots = $derived(view ? view.slots.filter((slot) => !slot.dealId && !slot.talkId) : []);
  let listed = $derived(freeSlots.find((slot) => slot.slot === listing) ?? freeSlots[0] ?? null);

  let yearly = $derived(view ? view.deals.reduce((sum, deal) => sum + deal.annualCents, 0) : 0);
  const objectiveOf = (id: string | null): ObjectiveItemView | null => view?.objectives.find((item) => item.id === id) ?? null;

  const askText = (ask: Ask) =>
    ask.kind === 'begin'
      ? tr.t('sponsor.ask.begin', { name: ask.name })
      : ask.kind === 'sign'
        ? tr.t('sponsor.ask.sign', { name: ask.name })
        : ask.kind === 'leave'
          ? tr.t('sponsor.ask.leave', { name: ask.name })
          : tr.t(ask.accept ? 'sponsor.ask.accept' : 'sponsor.ask.decline', { name: ask.name });

  async function run() {
    const ask = asking;
    asking = null;
    if (!ask) return;
    if (ask.kind === 'begin') await act('beginSponsorTalks', { organizationId: teamId, sponsorId: ask.sponsorId, slot: ask.slot });
    else if (ask.kind === 'sign') await act('signSponsor', { organizationId: teamId, talkId: ask.talkId });
    else if (ask.kind === 'leave') await act('walkAwayFromTalks', { organizationId: teamId, talkId: ask.talkId });
    else await act('respondToSponsorOffer', { organizationId: teamId, offerId: ask.offerId, accept: ask.accept });
  }

  const same = (a: Ask | null, b: Ask) => a !== null && JSON.stringify(a) === JSON.stringify(b);
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
          {@const accept = { kind: 'offer', offerId: offer.id, accept: true, name: offer.sponsorName } as const}
          {@const decline = { kind: 'offer', offerId: offer.id, accept: false, name: offer.sponsorName } as const}
          <div class="sp-offer">
            <b>{offer.sponsorName}</b>
            <span class="num">{formatMoney(offer.annualCents, tr.lang)}<small class="muted"> {tr.t('sponsor.perYear')}</small></span>
            <span class="muted">{tr.t('sponsor.until', { date: formatDate(offer.validUntil, tr.lang) })}</span>
            {#if same(asking, accept) || same(asking, decline)}
              {#if asking}<Confirmation {tr} {busy} ask={askText(asking)} onCancel={() => (asking = null)} onConfirm={run} />{/if}
            {:else}
              <div class="ask-btns">
                <button class="btn sm" type="button" disabled={busy} onclick={() => (asking = decline)}>{tr.t('sponsor.decline')}</button>
                <button class="btn sm primary" type="button" disabled={busy} onclick={() => (asking = accept)}>{tr.t('sponsor.accept')}</button>
              </div>
            {/if}
          </div>
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
          {:else if talk}
            <h3>{talk.sponsorName}</h3>
            <div class="fields mid">
              <div class="fld"><span class="meta">{tr.t('sponsor.now')}</span><span class="v num">{formatMoney(talk.currentAnnualCents, tr.lang)}</span></div>
              <div class="fld"><span class="meta">{tr.t('sponsor.cap')}</span><span class="v num">{formatMoney(talk.cappedAnnualCents, tr.lang)}</span></div>
            </div>
            <p class="muted">{tr.tMsg(talk.note)}</p>
            {#if talk.objective}<div class="sp-goal"><span class="meta">{tr.t('sponsor.goal')}</span><b>{tr.tMsg(talk.objective)}</b></div>{/if}
            {#if talk.rivalKnown}<Status text={tr.t('sponsor.rival')} tone="warn" />{/if}
            {@const sign = { kind: 'sign', talkId: talk.id, name: talk.sponsorName } as const}
            {@const leave = { kind: 'leave', talkId: talk.id, name: talk.sponsorName } as const}
            {#if same(asking, sign) || same(asking, leave)}
              {#if asking}<Confirmation {tr} {busy} ask={askText(asking)} onCancel={() => (asking = null)} onConfirm={run} />{/if}
            {:else}
              <div class="confirm">
                <button class="btn" type="button" disabled={busy} onclick={() => (asking = leave)}>{tr.t('sponsor.leave')}</button>
                <button class="btn primary" type="button" disabled={busy} onclick={() => (asking = sign)}>{tr.t('sponsor.sign')}</button>
              </div>
            {/if}
          {:else}
            <button class="btn" type="button" class:primary={listed?.slot === slot.slot} onclick={() => (listing = slot.slot)}>{tr.t('sponsor.choose')}</button>
          {/if}
        </div>
      </section>
    {/each}
  </div>

  {#if listed}
    <section class="panel tbl cand">
      <header><h2>{tr.t('sponsor.candidates')}</h2><span class="meta">{tr.tMsg(listed.kind)}</span></header>
      <table class="table tight">
        <thead><tr><th>{tr.t('sponsor.company')}</th><th>{tr.t('sponsor.industry')}</th><th class="r">{tr.t('sponsor.indicative')}</th><th></th></tr></thead>
        <tbody>
          {#each listed.candidates as candidate (candidate.sponsorId)}
            {@const begin = { kind: 'begin', slot: listed.slot, sponsorId: candidate.sponsorId, name: candidate.sponsorName } as const}
            <tr>
              <td><b>{candidate.sponsorName}</b></td>
              <td class="muted">{tr.tMsg(candidate.industry)}</td>
              <td class="r num">{formatMoney(candidate.indicativeAnnualCents, tr.lang)}</td>
              <td>
                {#if candidate.blocked}
                  <Status text={tr.tMsg(candidate.blocked)} tone="bad" />
                {:else if !same(asking, begin)}
                  <button class="btn sm" type="button" disabled={busy} onclick={() => (asking = begin)}>{tr.t('sponsor.begin')}</button>
                {/if}
              </td>
            </tr>
            {#if same(asking, begin) && asking}
              <tr><td colspan="4"><Confirmation {tr} {busy} ask={askText(asking)} onCancel={() => (asking = null)} onConfirm={run} /></td></tr>
            {/if}
          {/each}
        </tbody>
      </table>
    </section>
  {/if}
{/if}
