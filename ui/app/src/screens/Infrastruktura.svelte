<script lang="ts">
  import type { BridgeCommandName, FacilityEffectView, OwnFacilityView } from '../lib/api/types.generated';
  import Confirmation from '../lib/components/Confirmation.svelte';
  import Status from '../lib/components/Status.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { formatMoney } from '../lib/money.mjs';
  import type { InfraData } from '../lib/screens';
  import { countryName, percent, quality, type Tr } from '../lib/ui';

  let {
    data,
    tr,
    busy,
    act,
  }: {
    data: InfraData;
    tr: Tr;
    busy: boolean;
    act: (name: BridgeCommandName, args: Record<string, unknown>) => Promise<boolean>;
  } = $props();

  let team = $derived(data.infra.own[0] ?? null);
  let asking = $state<string | null>(null);

  async function upgrade(kind: string) {
    if (!team) return;
    await act('upgradeFacility', { organizationId: team.organizationId, kind });
    asking = null;
  }

  async function rent() {
    if (!team) return;
    await act('bookTest', { organizationId: team.organizationId });
    asking = null;
  }

  async function cancelTest(testOn: string) {
    if (!team) return;
    await act('cancelTest', { organizationId: team.organizationId, testOn });
    asking = null;
  }

  const fill = (facility: OwnFacilityView) => Math.max(0, Math.min(100, facility.relativeQuality * 100));

  /* A multiplier reads as a share of the baseline ("91%"); a bonus as a share added to it ("+2,4%"). One decimal for a bonus, which is small. */
  const number = (value: number, digits: number) =>
    new Intl.NumberFormat(tr.lang === 'en' ? 'en-GB' : 'pl-PL', { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(value);
  const show = (effect: FacilityEffectView, value: number) => (effect.mode === 'bonus' ? `+${number(value * 100, 1)}%` : percent(tr, value));
  const range = (effect: FacilityEffectView) => `${show(effect, effect.atZero)} – ${show(effect, effect.atFull)}`;
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.infrastructure')}</h1>
</div>

{#if team}
  <div class="infra">
    {#each team.facilities as facility (facility.kind)}
      <section class="panel fac" class:off={!facility.unlocked}>
        <header>
          <h2>{tr.t(`infrastructure.kind.${facility.kind}`)}</h2>
          {#if !facility.unlocked}
            <Status text={tr.t('infra.unavailable', { year: String(facility.unlockYear) })} />
          {:else if facility.building && facility.buildEnds}
            <Status text={tr.t('infra.building', { date: formatDate(facility.buildEnds, tr.lang) })} tone="hi" />
          {/if}
        </header>
        <div class="body">
          {#if facility.unlocked}
            <div class="fields eq">
              <div class="fld"><span class="meta">{tr.t('infra.own')}</span><span class="v num">{quality(tr, facility.qualityMilli)}</span></div>
              <div class="fld"><span class="meta">{tr.t('infra.frontier')}</span><span class="v num">{quality(tr, facility.frontierMilli)}</span></div>
              <div class="fld"><span class="meta">{tr.t('infra.relative')}</span><span class="v num">{percent(tr, facility.relativeQuality)}</span></div>
            </div>
            <div class="bar fac-bar"><i style="width:{fill(facility)}%"></i></div>
          {/if}
          <table class="table tight effects">
            <thead>
              <tr>
                <th>{tr.t('infra.gives')}</th>
                {#if facility.unlocked}<th class="r">{tr.t('infra.now')}</th><th class="r">{tr.t('infra.after')}</th>{/if}
                <th class="r">{tr.t('infra.range')}</th>
              </tr>
            </thead>
            <tbody>
              {#each facility.effects as effect (effect.key)}
                <tr>
                  <td>{tr.t(`infra.effect.${effect.key}`)}</td>
                  {#if facility.unlocked}
                    <td class="r num">{show(effect, effect.now)}</td>
                    <td class="r num">{show(effect, effect.afterUpgrade)}</td>
                  {/if}
                  <td class="r num muted">{range(effect)}</td>
                </tr>
              {/each}
            </tbody>
          </table>
          {#if facility.unlocked}
            {#if asking === facility.kind}
              <Confirmation
                {tr}
                {busy}
                ask={tr.t('infra.upgrade.ask', { name: tr.t(`infrastructure.kind.${facility.kind}`), cost: formatMoney(facility.upgradeCostCents, tr.lang), days: tr.tCount('shell.days', facility.upgradeDays) })}
                onCancel={() => (asking = null)}
                onConfirm={() => upgrade(facility.kind)}
              />
            {:else}
              <div class="confirm">
                <div class="fields">
                  <div class="fld"><span class="meta">{tr.t('infra.cost')}</span><span class="v num">{formatMoney(facility.upgradeCostCents, tr.lang)}</span></div>
                  <div class="fld"><span class="meta">{tr.t('infra.duration')}</span><span class="v num">{tr.tCount('shell.days', facility.upgradeDays)}</span></div>
                </div>
                <button class="btn primary" type="button" disabled={busy || facility.building || !facility.eligible} onclick={() => (asking = facility.kind)}>{tr.t('infra.upgrade')}</button>
              </div>
            {/if}
          {/if}
        </div>
      </section>
    {/each}

    <section class="panel fac">
      <header>
        <h2>{tr.t('infra.tests')}</h2>
        {#if !team.tests.allowed && team.tests.cap === 0}<Status text={tr.t('infra.tests.banned')} tone="warn" />{/if}
      </header>
      <div class="body">
        <div class="fields eq">
          <div class="fld"><span class="meta">{tr.t('infra.tests.used')}</span><span class="v num">{team.tests.used} / {team.tests.cap}</span></div>
          <div class="fld"><span class="meta">{tr.t('infra.tests.cost')}</span><span class="v num">{formatMoney(team.tests.costCents, tr.lang)}</span></div>
        </div>
        {#if asking === 'test'}
          <Confirmation
            {tr}
            {busy}
            ask={tr.t('infra.tests.ask', { cost: formatMoney(team.tests.costCents, tr.lang), date: formatDate(team.tests.nextDate, tr.lang) })}
            onCancel={() => (asking = null)}
            onConfirm={rent}
          />
        {:else}
          <div class="confirm">
            <button class="btn primary" type="button" disabled={busy || !team.tests.allowed} onclick={() => (asking = 'test')}>{tr.t('infra.tests.rent')}</button>
          </div>
        {/if}
        {#each team.tests.booked as testOn (testOn)}
          {#if asking === `cancel:${testOn}`}
            <Confirmation
              {tr}
              {busy}
              ask={tr.t('infra.tests.cancel.ask', { date: formatDate(testOn, tr.lang) })}
              onCancel={() => (asking = null)}
              onConfirm={() => cancelTest(testOn)}
            />
          {:else}
            <div class="confirm">
              <div class="fields">
                <div class="fld"><span class="meta">{tr.t('infra.tests.booked')}</span><span class="v num">{formatDate(testOn, tr.lang)}</span></div>
              </div>
              <button class="btn" type="button" disabled={busy} onclick={() => (asking = `cancel:${testOn}`)}>{tr.t('infra.tests.cancel')}</button>
            </div>
          {/if}
        {/each}
      </div>
    </section>

    {#if team.nextTransport}
      <section class="panel fac">
        <header><h2>{tr.t('infra.transport')}</h2></header>
        <div class="body">
          <div class="fields eq">
            <div class="fld"><span class="meta">{tr.t('infra.transport.to')}</span><span class="v">{countryName(tr, team.nextTransport.circuitCountry)}</span></div>
            <div class="fld"><span class="meta">{tr.t('infra.transport.mode')}</span><span class="v">{tr.t(`infra.transport.${team.nextTransport.mode}`)}</span></div>
            <div class="fld"><span class="meta">{tr.t('infra.duration')}</span><span class="v num">{tr.tCount('shell.days', team.nextTransport.days)}</span></div>
            <div class="fld"><span class="meta">{tr.t('infra.cost')}</span><span class="v num">{formatMoney(team.nextTransport.costCents, tr.lang)}</span></div>
          </div>
        </div>
      </section>
    {/if}
  </div>
{/if}
