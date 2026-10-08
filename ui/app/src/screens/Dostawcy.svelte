<script lang="ts">
  import type { BridgeCommandName } from '../lib/api/types.generated';
  import { teamLabel } from '../lib/career.mjs';
  import Confirmation from '../lib/components/Confirmation.svelte';
  import Status from '../lib/components/Status.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { formatMoney } from '../lib/money.mjs';
  import type { SupplyData } from '../lib/screens';
  import type { Tr } from '../lib/ui';

  let {
    data,
    tr,
    teamId,
    busy,
    act,
  }: {
    data: SupplyData;
    tr: Tr;
    teamId: string;
    busy: boolean;
    act: (name: BridgeCommandName, args: Record<string, unknown>) => Promise<boolean>;
  } = $props();

  let asking = $state<{ id: string; accept: boolean } | null>(null);

  const lc = (text: string) => text.charAt(0).toLowerCase() + text.slice(1);
  /* A supplier id is "supplier:ferrari" or a team id; the world names both from the id. */
  const supplier = (id: string) => teamLabel(id.includes(':') ? id.slice(id.indexOf(':') + 1) : id);
  const num = (value: number) => new Intl.NumberFormat(tr.lang === 'en' ? 'en-GB' : 'pl-PL', { minimumFractionDigits: 1, maximumFractionDigits: 1 }).format(value);
  const band = (low: number, high: number) => `${num(low)}–${num(high)}`;
  let season = $derived(Number(data.today.slice(0, 4)));

  async function respond() {
    const ask = asking;
    asking = null;
    if (ask) await act('respondToSupply', { organizationId: teamId, negotiationId: ask.id, accept: ask.accept });
  }
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.suppliers')}</h1>
  <div class="fields">
    <div class="fld"><span class="meta">{tr.t('supply.deals')}</span><span class="v num">{data.supply.deals.length}</span></div>
  </div>
</div>

<div class="sup-list">
  {#each data.supply.deals as deal (deal.dealId)}
    <section class="panel sup">
      <div class="sup-id"><span class="meta">{tr.t(`supply.item.${lc(deal.item)}`)}</span><b>{supplier(deal.supplierId)}</b></div>
      <div class="fields">
        <div class="fld"><span class="meta">{tr.t('supply.agreement')}</span><span class="v"><Status text={tr.t(`supply.kind.${lc(deal.kind)}`)} tone={deal.kind === 'Works' ? 'team' : deal.kind === 'Partner' ? 'hi' : ''} /></span></div>
        <div class="fld"><span class="meta">{tr.t('supply.seasons')}</span><span class="v num" class:bad={deal.lastSeason <= season}>{deal.firstSeason}–{deal.lastSeason}</span></div>
        <div class="fld"><span class="meta">{tr.t('supply.price')}</span><span class="v num">{deal.kind === 'Works' ? tr.t('supply.price.none') : formatMoney(deal.annualPriceCents, tr.lang)}</span></div>
        {#if deal.exclusive}<div class="fld"><span class="meta">{tr.t('supply.exclusive')}</span><span class="v">{tr.t('supply.yes')}</span></div>{/if}
      </div>
      {#if deal.engine}
        <div class="fields sup-params">
          <div class="fld"><span class="meta">{tr.t('dev.area.power')}</span><span class="v num">{band(deal.engine.power.low, deal.engine.power.high)}</span></div>
          <div class="fld"><span class="meta">{tr.t('dev.area.reliability')}</span><span class="v num">{band(deal.engine.reliability.low, deal.engine.reliability.high)}</span></div>
          <div class="fld"><span class="meta">{tr.t('supply.version')}</span><span class="v num">{deal.engine.versionSeason}</span></div>
          <div class="fld"><span class="meta">{tr.t('supply.lag')}</span><span class="v num">{deal.engine.lagSeasons}</span></div>
        </div>
      {/if}
    </section>
  {/each}

  {#each data.supply.talks as talk (talk.negotiationId)}
    <section class="panel sup talk-sup">
      <div class="sup-id"><span class="meta">{tr.t(`supply.item.${lc(talk.item)}`)} · {tr.t(`supply.kind.${lc(talk.kind)}`)}</span><b>{supplier(talk.supplierId)}</b></div>
      <div class="fields">
        <div class="fld"><span class="meta">{tr.t('negotiation.state')}</span><span class="v"><Status text={tr.t(`negotiation.status.${lc(talk.status)}`)} tone={talk.status === 'Countered' ? 'hi' : ''} /></span></div>
        <div class="fld"><span class="meta">{tr.t('negotiation.offer')}</span><span class="v num">{formatMoney(talk.offerCents, tr.lang)}</span></div>
        {#if talk.counterCents !== null}
          <div class="fld"><span class="meta">{tr.t('negotiation.counter')}</span><span class="v num">{formatMoney(talk.counterCents, tr.lang)}</span></div>
        {/if}
        <div class="fld"><span class="meta">{tr.t('negotiation.rounds')}</span><span class="v num">{talk.roundsLeft}</span></div>
        <div class="fld"><span class="meta">{tr.t('negotiation.deadline')}</span><span class="v num">{formatDate(talk.deadline, tr.lang)}</span></div>
      </div>
      {#if talk.reasons.length > 0}
        <div class="reasons">{#each talk.reasons as reason (reason)}<p>{tr.t(reason)}</p>{/each}</div>
      {/if}
      {#if talk.status === 'Countered'}
        {#if asking && asking.id === talk.negotiationId}
          <Confirmation
            {tr}
            {busy}
            ask={tr.t(asking.accept ? 'supply.ask.accept' : 'supply.ask.decline', { name: supplier(talk.supplierId) })}
            onCancel={() => (asking = null)}
            onConfirm={respond}
          />
        {:else}
          <div class="confirm">
            <button class="btn" type="button" disabled={busy} onclick={() => (asking = { id: talk.negotiationId, accept: false })}>{tr.t('sponsor.decline')}</button>
            <button class="btn primary" type="button" disabled={busy} onclick={() => (asking = { id: talk.negotiationId, accept: true })}>{tr.t('supply.accept')}</button>
          </div>
        {/if}
      {/if}
    </section>
  {/each}

  {#if data.supply.deals.length === 0 && data.supply.talks.length === 0}
    <Status text={tr.t('supply.none')} />
  {/if}
</div>
