<script lang="ts">
  import Status from '../lib/components/Status.svelte';
  import { formatMoney } from '../lib/money.mjs';
  import type { FinanceData } from '../lib/screens';
  import type { Tr } from '../lib/ui';

  let { data, tr }: { data: FinanceData; tr: Tr } = $props();

  let view = $derived('cashCents' in data.finance ? data.finance : null);
  let unknown = $derived('reason' in data.finance ? data.finance.reason : null);
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.finance')}</h1>
</div>

{#if view}
  <div class="fin-grid">
    <section class="panel fin-tile main">
      <span class="meta">{tr.t('shell.cash')}</span>
      <b class="num big" class:bad={view.cashCents < 0}>{formatMoney(view.cashCents, tr.lang)}</b>
    </section>
    <section class="panel fin-tile">
      <span class="meta">{tr.t('finance.certain')}</span>
      <b class="num big good">{formatMoney(view.certainIncomeCents, tr.lang)}</b>
    </section>
    <section class="panel fin-tile">
      <span class="meta">{tr.t('finance.obligations')}</span>
      <b class="num big bad">{formatMoney(view.obligationsCents, tr.lang)}</b>
    </section>
    <section class="panel fin-tile">
      <span class="meta">{tr.t('finance.forecast')}</span>
      <b class="num big" class:bad={view.forecastCashCents < 0}>{formatMoney(view.forecastCashCents, tr.lang)}</b>
      <small class="muted">{tr.tMsg(view.forecastNote)}</small>
    </section>
  </div>
  {#if view.loanOffers > 0}
    <div class="fin-loans"><Status text={tr.t('finance.loans', { count: String(view.loanOffers) })} tone="hi" /></div>
  {/if}
{:else if unknown}
  <Status text={tr.tMsg(unknown)} tone="warn" />
{/if}
