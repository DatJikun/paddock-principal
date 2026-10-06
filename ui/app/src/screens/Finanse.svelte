<script lang="ts">
  import Status from '../lib/components/Status.svelte';
  import Tabs from '../lib/components/Tabs.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { formatMoney } from '../lib/money.mjs';
  import type { FinanceData } from '../lib/screens';
  import type { Tr } from '../lib/ui';

  let { data, tr }: { data: FinanceData; tr: Tr } = $props();

  let view = $derived('cashCents' in data.finance ? data.finance : null);
  let unknown = $derived('reason' in data.finance ? data.finance.reason : null);
  let filter = $state('all');
  let lines = $derived(
    (view?.ledger ?? []).filter((line) => (filter === 'income' ? line.amountCents > 0 : filter === 'costs' ? line.amountCents < 0 : true)),
  );
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
  <section class="panel tbl ledger">
    <header>
      <h2>{tr.t('finance.ledger')}</h2>
      <Tabs
        group="ledger-filter"
        items={[
          { value: 'all', label: tr.t('finance.ledger.all') },
          { value: 'income', label: tr.t('finance.ledger.income') },
          { value: 'costs', label: tr.t('finance.ledger.costs') },
        ]}
        bind:value={filter}
      />
    </header>
    {#if lines.length > 0}
      <div class="tbl-scroll">
        <table class="table tight fit">
          <thead>
            <tr>
              <th>{tr.t('shell.col.date')}</th>
              <th>{tr.t('finance.col.category')}</th>
              <th>{tr.t('finance.col.what')}</th>
              <th class="r">{tr.t('finance.col.amount')}</th>
            </tr>
          </thead>
          <tbody>
            {#each lines as line, index (index)}
              <tr>
                <td class="num">{formatDate(line.on, tr.lang)}</td>
                <td>{tr.t(`finance.category.${line.category}`)}</td>
                <td class="wrap">{tr.tMsg(line.reason)}</td>
                <td class="r num" class:good={line.amountCents > 0} class:bad={line.amountCents < 0}>{formatMoney(line.amountCents, tr.lang)}</td>
              </tr>
            {/each}
          </tbody>
        </table>
      </div>
    {:else}
      <div class="empty"><Status text={tr.t('finance.ledger.empty')} /></div>
    {/if}
  </section>
{:else if unknown}
  <Status text={tr.tMsg(unknown)} tone="warn" />
{/if}
