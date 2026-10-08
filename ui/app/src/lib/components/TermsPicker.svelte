<script lang="ts">
  import type { SponsorQuoteView } from '../api/types.generated';
  import { formatMoney } from '../money.mjs';
  import type { Tr } from '../ui';
  import Status from './Status.svelte';
  import Tabs from './Tabs.svelte';

  /**
   * The terms of a sponsor deal: how long and how hard the condition is. Every number on it is read from the table of quotes the backend
   * made with the same functions the commands sign with; this component only picks a row.
   */
  let {
    tr,
    group,
    quotes,
    ambitionOpen,
    years = $bindable(),
    ambition = $bindable(),
    cap = false,
  }: {
    tr: Tr;
    group: string;
    quotes: SponsorQuoteView[];
    ambitionOpen: boolean;
    years: string;
    ambition: string;
    /** True in open talks, where waiting raises the price up to a cap that is worth showing. */
    cap?: boolean;
  } = $props();

  let quote = $derived(quotes.find((item) => String(item.years) === years && item.ambition === (ambitionOpen ? ambition : 'standard')) ?? null);
</script>

<div class="terms">
  <div class="fld">
    <span class="meta">{tr.t('sponsor.terms.length')}</span>
    <Tabs {group} items={['1', '2', '3'].map((value) => ({ value, label: tr.t(`sponsor.terms.years.${value}`) }))} bind:value={years} />
  </div>
  {#if ambitionOpen}
    <div class="fld">
      <span class="meta">{tr.t('sponsor.terms.level')}</span>
      <Tabs
        group={`${group}-level`}
        items={['lighter', 'standard', 'harder'].map((value) => ({ value, label: tr.t(`sponsor.ambition.${value}`) }))}
        bind:value={ambition}
      />
    </div>
  {/if}
  {#if quote}
    <div class="fields terms-quote">
      <div class="fld"><span class="meta">{tr.t('sponsor.terms.yearly')}</span><span class="v num">{formatMoney(quote.annualCents, tr.lang)}</span></div>
      {#if cap}
        <div class="fld"><span class="meta">{tr.t('sponsor.cap')}</span><span class="v num">{formatMoney(quote.capCents, tr.lang)}</span></div>
      {/if}
      <div class="fld"><span class="meta">{tr.t('sponsor.terms.total')}</span><span class="v num">{formatMoney(quote.annualCents * quote.years, tr.lang)}</span></div>
    </div>
    {#if quote.condition}
      <div class="sp-goal">
        <span class="meta">{tr.t('sponsor.goal')}</span>
        <b>{tr.tMsg(quote.condition)}</b>
        <span class="muted">{tr.t('sponsor.terms.bonus')}: <span class="num">{formatMoney(quote.bonusCents, tr.lang)}</span></span>
      </div>
    {:else}
      <Status text={tr.t('sponsor.terms.none')} />
    {/if}
  {/if}
</div>
