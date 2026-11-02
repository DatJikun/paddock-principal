<script lang="ts">
  import { untrack } from 'svelte';
  import type { OfferTerms, SalaryGuideView } from '../api/types.generated';
  import { SEATS } from '../people.mjs';
  import { formatMoney } from '../money.mjs';
  import { icon, ICON, type Tr } from '../ui';
  import Confirmation from './Confirmation.svelte';
  import Tabs from './Tabs.svelte';

  /** What the bridge takes as an offer. Money is whole currency units; the option and the exit clause are empty when unused. */
  export type OfferInput = {
    salary: number;
    winBonus: number;
    titleBonus: number;
    years: number;
    seat: string | null;
    optionHolder: string | null;
    optionYears: number | null;
    exitWorseThan: number | null;
  };

  let {
    tr,
    initial,
    guide = null,
    staff = false,
    busy,
    label,
    onSubmit,
  }: {
    tr: Tr;
    initial: OfferTerms | null;
    /** What the sliders span and where the suggested salary range sits, from the bridge. Without it the salary is typed. */
    guide?: SalaryGuideView | null;
    /** A staff contract: the same flow, without a seat, bonuses or an exit clause (#318). */
    staff?: boolean;
    busy: boolean;
    /** The button that starts the confirm step ("Wyślij ofertę", "Zaproponuj nowy kontrakt"). */
    label: string;
    onSubmit: (offer: OfferInput) => void;
  } = $props();

  /* The form starts from the terms on the table, if any; nothing here is a suggestion, it only saves retyping. */
  let salary = $state<number | null>(untrack(() => initial?.salary ?? guide?.suggested ?? null));
  let years = $state(untrack(() => String(initial?.years ?? 1)));
  let seat = $state(untrack(() => initial?.seat ?? 'Equal'));
  let winBonus = $state<number>(untrack(() => initial?.winBonus ?? 0));
  let titleBonus = $state<number>(untrack(() => initial?.titleBonus ?? 0));
  let exitWorse = $state<number | null>(untrack(() => initial?.exit?.positionWorseThan ?? null));
  let asking = $state(false);

  /* Sliders move in round steps, so the number under them reads cleanly. */
  const stepFor = (max: number) => (max >= 2_000_000 ? 10_000 : max >= 200_000 ? 1_000 : max >= 20_000 ? 100 : 10);
  let ready = $derived(salary !== null && salary >= 0 && Number.isFinite(Number(salary)));
  const money = (value: number | null) => formatMoney(Math.trunc(Number(value ?? 0)) * 100, tr.lang);

  function offer(): OfferInput {
    return {
      salary: Math.trunc(Number(salary ?? 0)),
      winBonus: staff ? 0 : Math.trunc(Number(winBonus ?? 0)),
      titleBonus: staff ? 0 : Math.trunc(Number(titleBonus ?? 0)),
      years: Number(years),
      seat: staff ? null : seat,
      optionHolder: null,
      optionYears: null,
      exitWorseThan: staff || exitWorse === null || Number.isNaN(Number(exitWorse)) ? null : Math.trunc(Number(exitWorse)),
    };
  }

  let ask = $derived(tr.t('offer.ask', { salary: money(salary), years: String(years) }));
</script>

<div class="offer">
  <div class="offer-grid">
    <div class="fld salary">
      <span class="meta">{tr.t('offer.salary')}</span>
      {#if guide}
        <input type="range" min={guide.min} max={guide.max} step={stepFor(guide.max)} bind:value={salary} aria-label={tr.t('offer.salary')} />
        <div class="slide-v">
          <b class="num">{money(salary)}</b>
          <small class="muted num">{tr.t('offer.suggested', { low: money(guide.suggestedLow), high: money(guide.suggestedHigh) })}</small>
        </div>
      {:else}
        <input class="text num plain-num" type="number" min="0" step="1000" bind:value={salary} />
      {/if}
    </div>
    <div class="fld">
      <span class="meta">{tr.t('offer.years')}</span>
      <Tabs group="offer-years" items={['1', '2', '3', '4', '5'].map((value) => ({ value, label: value }))} bind:value={years} />
    </div>
    {#if !staff}
      <div class="fld">
        <span class="meta">{tr.t('offer.seat')}</span>
        <Tabs group="offer-seat" items={SEATS.map((value) => ({ value, label: tr.t(`seat.${value}`) }))} bind:value={seat} />
      </div>
      {#if guide && guide.winBonusMax > 0}
        <div class="fld">
          <span class="meta">{tr.t('offer.winBonus')}</span>
          <input type="range" min="0" max={guide.winBonusMax} step={stepFor(guide.winBonusMax)} bind:value={winBonus} aria-label={tr.t('offer.winBonus')} />
          <b class="num slide-n">{money(winBonus)}</b>
        </div>
        <div class="fld">
          <span class="meta">{tr.t('offer.titleBonus')}</span>
          <input type="range" min="0" max={guide.titleBonusMax} step={stepFor(guide.titleBonusMax)} bind:value={titleBonus} aria-label={tr.t('offer.titleBonus')} />
          <b class="num slide-n">{money(titleBonus)}</b>
        </div>
      {/if}
      <label class="fld">
        <span class="meta">{tr.t('offer.exit')}</span>
        <input class="text num plain-num" type="number" min="1" placeholder="—" bind:value={exitWorse} />
        <small class="muted hint">{tr.t('offer.exit.explain')}</small>
      </label>
    {/if}
  </div>

  {#if asking}
    <Confirmation
      {tr}
      {ask}
      {busy}
      onCancel={() => (asking = false)}
      onConfirm={() => {
        asking = false;
        onSubmit(offer());
      }}
    />
  {:else}
    <div class="confirm">
      <button class="btn primary" type="button" disabled={!ready || busy} onclick={() => (asking = true)}>
        {@html icon(ICON.arrow, 17)}<span>{label}</span>
      </button>
    </div>
  {/if}
</div>
