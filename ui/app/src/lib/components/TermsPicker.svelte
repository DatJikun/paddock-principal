<script lang="ts">
  import type { SponsorPartnershipView, SponsorQuoteView } from '../api/types.generated';
  import { formatMoney } from '../money.mjs';
  import type { Tr } from '../ui';
  import Status from './Status.svelte';
  import Tabs from './Tabs.svelte';

  /**
   * The terms of a sponsor deal: how long and how hard the condition is, how open the sponsor is to a long partnership, and, when the deal can
   * still be asked for more, a small ask. Every number on it is read from the table of quotes the backend made with the same functions the
   * commands sign with; this component only picks a row and a step.
   */
  let {
    tr,
    group,
    quotes,
    ambitionOpen,
    years = $bindable(),
    ambition = $bindable(),
    cap = false,
    partnership = null,
    askable = false,
    askMilli = $bindable('0'),
  }: {
    tr: Tr;
    group: string;
    quotes: SponsorQuoteView[];
    ambitionOpen: boolean;
    years: string;
    ambition: string;
    /** True in open talks, where waiting raises the price up to a cap that is worth showing. */
    cap?: boolean;
    /** How open the sponsor is to a long partnership, in words; shown while the player chooses the length. */
    partnership?: SponsorPartnershipView | null;
    /** True when the player can still ask for a little more than the quote (open talks, a renewal being negotiated). */
    askable?: boolean;
    /** The ask, in thousandths above the quote, as the text of the chosen tab. */
    askMilli?: string;
  } = $props();

  let quote = $derived(quotes.find((item) => String(item.years) === years && item.ambition === (ambitionOpen ? ambition : 'standard')) ?? null);
  let answer = $derived(quote?.asks.find((item) => String(item.milli) === askMilli) ?? null);
  let askItems = $derived(
    (quote?.asks ?? []).map((item) => ({
      value: String(item.milli),
      label: item.milli === 0 ? tr.t('sponsor.ask.none') : tr.t('sponsor.ask.step', { percent: String(item.milli / 10) }),
    })),
  );
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
  {#if partnership}
    <div class="sp-partner {partnership.band}">
      <span class="meta">{tr.t('sponsor.partnership.title')}</span>
      <b>{tr.t(`sponsor.partnership.${partnership.band}`)}</b>
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
    {#if askable && askItems.length > 1}
      <div class="fld">
        <span class="meta">{tr.t('sponsor.ask.title')}</span>
        <Tabs group={`${group}-ask`} items={askItems} bind:value={askMilli} />
      </div>
      {#if answer && answer.milli > 0}
        <Status
          text={tr.t(answer.outcome === 'accepted' ? 'sponsor.ask.accepted' : 'sponsor.ask.countered', { amount: formatMoney(answer.annualCents, tr.lang) })}
          tone={answer.outcome === 'accepted' ? '' : 'warn'}
        />
      {/if}
    {/if}
  {/if}
</div>
