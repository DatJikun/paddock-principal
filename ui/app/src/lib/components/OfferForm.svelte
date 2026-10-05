<script lang="ts">
  import { untrack } from 'svelte';
  import type { OfferTerms } from '../api/types.generated';
  import { SEATS } from '../people.mjs';
  import { formatMoney } from '../money.mjs';
  import { icon, ICON, type Tr } from '../ui';
  import Confirmation from './Confirmation.svelte';
  import Tabs from './Tabs.svelte';

  /** What the bridge takes as an offer. Money is whole currency units; the option and the exit clause are empty when unused. */
  export type OfferInput = {
    salary: number;
    pointsBonus: number;
    winBonus: number;
    titleBonus: number;
    years: number;
    seat: string;
    optionHolder: string | null;
    optionYears: number | null;
    exitWorseThan: number | null;
  };

  let {
    tr,
    initial,
    busy,
    label,
    onSubmit,
  }: {
    tr: Tr;
    initial: OfferTerms | null;
    busy: boolean;
    /** The button that starts the confirm step ("Wyślij ofertę", "Zaproponuj nowy kontrakt"). */
    label: string;
    onSubmit: (offer: OfferInput) => void;
  } = $props();

  /* The form starts from the terms on the table, if any; nothing here is a suggestion, it only saves retyping. */
  let salary = $state<number | null>(untrack(() => initial?.salary ?? null));
  let years = $state(untrack(() => String(initial?.years ?? 1)));
  let seat = $state(untrack(() => initial?.seat ?? 'Equal'));
  let pointsBonus = $state<number | null>(untrack(() => initial?.pointsBonus ?? 0));
  let winBonus = $state<number | null>(untrack(() => initial?.winBonus ?? 0));
  let titleBonus = $state<number | null>(untrack(() => initial?.titleBonus ?? 0));
  let holder = $state(untrack(() => initial?.option?.holder ?? 'none'));
  let optionYears = $state<number | null>(untrack(() => initial?.option?.extraYears ?? 1));
  let exitWorse = $state<number | null>(untrack(() => initial?.exit?.positionWorseThan ?? null));
  let asking = $state(false);

  let ready = $derived(salary !== null && salary >= 0 && Number.isFinite(Number(salary)));

  function offer(): OfferInput {
    return {
      salary: Math.trunc(Number(salary ?? 0)),
      pointsBonus: Math.trunc(Number(pointsBonus ?? 0)),
      winBonus: Math.trunc(Number(winBonus ?? 0)),
      titleBonus: Math.trunc(Number(titleBonus ?? 0)),
      years: Number(years),
      seat,
      optionHolder: holder === 'none' ? null : holder,
      optionYears: holder === 'none' ? null : Math.trunc(Number(optionYears ?? 1)),
      exitWorseThan: exitWorse === null || Number.isNaN(Number(exitWorse)) ? null : Math.trunc(Number(exitWorse)),
    };
  }

  let ask = $derived(
    tr.t('offer.ask', {
      salary: formatMoney(Math.trunc(Number(salary ?? 0)) * 100, tr.lang),
      years: String(years),
    }),
  );
</script>

<div class="offer">
  <div class="offer-grid">
    <label class="fld">
      <span class="meta">{tr.t('offer.salary')}</span>
      <input class="text num" type="number" min="0" step="1000" bind:value={salary} />
    </label>
    <div class="fld">
      <span class="meta">{tr.t('offer.years')}</span>
      <Tabs group="offer-years" items={['1', '2', '3', '4', '5'].map((value) => ({ value, label: value }))} bind:value={years} />
    </div>
    <div class="fld">
      <span class="meta">{tr.t('offer.seat')}</span>
      <Tabs group="offer-seat" items={SEATS.map((value) => ({ value, label: tr.t(`seat.${value}`) }))} bind:value={seat} />
    </div>
    <label class="fld">
      <span class="meta">{tr.t('offer.pointsBonus')}</span>
      <input class="text num" type="number" min="0" step="100" bind:value={pointsBonus} />
    </label>
    <label class="fld">
      <span class="meta">{tr.t('offer.winBonus')}</span>
      <input class="text num" type="number" min="0" step="1000" bind:value={winBonus} />
    </label>
    <label class="fld">
      <span class="meta">{tr.t('offer.titleBonus')}</span>
      <input class="text num" type="number" min="0" step="1000" bind:value={titleBonus} />
    </label>
    <div class="fld">
      <span class="meta">{tr.t('offer.option')}</span>
      <Tabs
        group="offer-option"
        items={[
          { value: 'none', label: tr.t('offer.option.none') },
          { value: 'Team', label: tr.t('offer.option.Team') },
          { value: 'Person', label: tr.t('offer.option.Person') },
        ]}
        bind:value={holder}
      />
    </div>
    {#if holder !== 'none'}
      <label class="fld">
        <span class="meta">{tr.t('offer.optionYears')}</span>
        <input class="text num" type="number" min="1" max="5" bind:value={optionYears} />
      </label>
    {/if}
    <label class="fld">
      <span class="meta">{tr.t('offer.exit')}</span>
      <input class="text num" type="number" min="1" placeholder="—" bind:value={exitWorse} />
    </label>
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
