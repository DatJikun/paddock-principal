<script lang="ts">
  import type { BridgeCommandName } from '../lib/api/types.generated';
  import AttrRow from '../lib/components/AttrRow.svelte';
  import Confirmation from '../lib/components/Confirmation.svelte';
  import ContractEnd from '../lib/components/ContractEnd.svelte';
  import Nationality from '../lib/components/Nationality.svelte';
  import OfferForm, { type OfferInput } from '../lib/components/OfferForm.svelte';
  import Status from '../lib/components/Status.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { formatMoney } from '../lib/money.mjs';
  import { bandOf, careerTotals, DRIVER_ATTRS } from '../lib/people.mjs';
  import { formatAge } from '../lib/person.mjs';
  import type { DriverData } from '../lib/screens';
  import { icon, ICON, initials, type Tr } from '../lib/ui';

  let {
    data,
    tr,
    today,
    teamId,
    busy,
    act,
  }: {
    data: DriverData;
    tr: Tr;
    today: string;
    teamId: string;
    busy: boolean;
    act: (name: BridgeCommandName, args: Record<string, unknown>) => Promise<boolean>;
  } = $props();

  let profile = $derived(data.profile);
  let others = $derived(data.squad.own.filter((driver) => driver.personId !== profile.personId));
  let talks = $derived(data.negotiations.items.find((item) => item.person === profile.personId && !['Agreed', 'Refused', 'WalkedAway', 'Lost', 'Lapsed'].includes(item.status)) ?? null);
  let totals = $derived(careerTotals(profile));
  let rated = $derived(profile.attributes.length > 0);
  let asking = $state<'talk' | 'option' | null>(null);
  let renewing = $state(false);

  async function talk() {
    asking = null;
    if (await act('openNegotiation', { organizationId: teamId, personId: profile.personId, subject: 'driver', deadline: null })) {
      location.hash = '#/rynek';
    }
  }

  async function exerciseOption() {
    asking = null;
    if (profile.contract) await act('renewContract', { contractId: profile.contract.contractId, exerciseOption: true });
  }

  async function renew(offer: OfferInput) {
    if (!profile.contract) return;
    if (await act('renewContract', { contractId: profile.contract.contractId, exerciseOption: false, ...offer })) renewing = false;
  }
</script>

{#if !profile.found}
  <div class="screen-head"><h1 class="screen">{tr.t('shell.nav.drivers')}</h1></div>
  <Status text={tr.t('driver.unknown')} tone="warn" />
{:else}
  <div class="profile">
    <section class="panel hero" class:foreign={!profile.own}>
      <div class="num-big"><span>{initials(profile.name)}</span></div>
      <div class="hero-main">
        <h1 class="screen">{profile.name}</h1>
        <div class="fields mid">
          <div class="fld">
            <span class="meta">{tr.t('career.you.country')}</span>
            <span class="v"><Nationality {tr} code={profile.nationality} /></span>
          </div>
          <div class="fld"><span class="meta">{tr.t('drivers.age')}</span><span class="v num">{formatAge(profile.age)}</span></div>
          <div class="fld">
            <span class="meta">{tr.t('shell.col.team')}</span>
            <span class="v">{#if profile.freeAgent}<Status text={tr.t('driver.free')} tone="hi" />{:else}{profile.organizationName ?? '—'}{/if}</span>
          </div>
          {#if profile.overall !== null}
            <div class="fld"><span class="meta">{tr.t('shell.col.overall')}</span><span class="v num">{profile.overall}</span></div>
          {/if}
          {#if profile.seat}
            <div class="fld"><span class="meta">{tr.t('drivers.seat')}</span><span class="v">{tr.t(`seat.${profile.seat}`)}</span></div>
          {/if}
        </div>
      </div>
      <div class="hero-side">
        <div class="tools">
          {#if profile.own && others.length > 0}
            <a class="btn" href={`#/porownaj/${encodeURIComponent(profile.personId)}/${encodeURIComponent(others[0]?.personId ?? '')}`}>{tr.t('drivers.compare')}</a>
          {/if}
          {#if profile.own}
            <a class="btn" href="#/kierowcy">{@html icon(ICON.back, 17)}<span>{tr.t('driver.squad')}</span></a>
          {:else}
            {#if talks}
              <a class="btn primary" href={`#/negocjacja/${encodeURIComponent(talks.id)}`}>{tr.t('driver.talks')}</a>
            {:else}
              <button class="btn primary" type="button" disabled={busy || asking === 'talk'} onclick={() => (asking = 'talk')}>{tr.t('driver.talk')}</button>
            {/if}
            <a class="btn" href="#/rynek">{@html icon(ICON.back, 17)}<span>{tr.t('shell.nav.market')}</span></a>
          {/if}
        </div>
      </div>
    </section>

    {#if asking === 'talk'}
      <Confirmation {tr} {busy} ask={tr.t('driver.talk.ask', { name: profile.name })} onCancel={() => (asking = null)} onConfirm={talk} />
    {/if}

    <div class="prof-grid f">
      <section class="panel">
        <header><h2>{tr.t('driver.attributes')}</h2></header>
        <div class="body">
          {#if rated}
            <div class="attrs">
              {#each DRIVER_ATTRS as key (key)}
                {@const band = bandOf(profile.attributes, key)}
                {#if band}<AttrRow label={tr.t(`attr.${key}`)} low={band.low} high={band.high} />{/if}
              {/each}
            </div>
            {#if profile.potential}
              <div class="persona fields boxed eq">
                <div class="fld"><span class="meta">{tr.t('driver.potential')}</span><span class="v num">{profile.potential.low === profile.potential.high ? profile.potential.low : `${profile.potential.low}–${profile.potential.high}`}</span></div>
              </div>
            {/if}
          {:else}
            <Status text={tr.t('driver.noRatings')} />
          {/if}
        </div>
      </section>

      <div class="col">
        <section class="panel">
          <header><h2>{tr.t('driver.contract')}</h2></header>
          <div class="body">
            {#if profile.contract}
              <div class="fields">
                <div class="fld"><span class="meta">{tr.t('driver.contract.until')}</span><span class="v"><ContractEnd {tr} end={profile.contract.end} {today} /></span></div>
                {#if profile.contract.salary > 0}
                  <div class="fld"><span class="meta">{tr.t('offer.salary')}</span><span class="v num">{formatMoney(profile.contract.salary * 100, tr.lang)}</span></div>
                {/if}
                {#if profile.contract.optionYears}
                  <div class="fld">
                    <span class="meta">{tr.t('offer.option')}</span>
                    <span class="v num">+{profile.contract.optionYears}{#if profile.contract.optionDeadline}<small class="muted">{formatDate(profile.contract.optionDeadline, tr.lang)}</small>{/if}</span>
                  </div>
                {/if}
                {#if profile.contract.releaseAmount !== null}
                  <div class="fld"><span class="meta">{tr.t('driver.release')}</span><span class="v num">{formatMoney(profile.contract.releaseAmount * 100, tr.lang)}</span></div>
                {/if}
              </div>
              {#if profile.upcoming}
                <div class="fields next">
                  <div class="fld"><span class="meta">{tr.t('driver.contract.from')}</span><span class="v num">{formatDate(profile.upcoming.start, tr.lang)}</span></div>
                  <div class="fld"><span class="meta">{tr.t('driver.contract.until')}</span><span class="v"><ContractEnd {tr} end={profile.upcoming.end} {today} quiet /></span></div>
                  {#if profile.upcoming.salary > 0}
                    <div class="fld"><span class="meta">{tr.t('offer.salary')}</span><span class="v num">{formatMoney(profile.upcoming.salary * 100, tr.lang)}</span></div>
                  {/if}
                </div>
              {/if}
              {#if asking === 'option'}
                <Confirmation {tr} {busy} ask={tr.t('driver.option.ask', { name: profile.name })} onCancel={() => (asking = null)} onConfirm={exerciseOption} />
              {:else if renewing}
                <OfferForm {tr} initial={null} guide={profile.salaryGuide} {busy} label={tr.t('driver.renew.send')} onSubmit={renew} />
                <div class="confirm"><button class="btn sm" type="button" onclick={() => (renewing = false)}>{tr.t('game.menu.cancel')}</button></div>
              {:else}
                <div class="confirm">
                  {#if profile.contract.optionYears}
                    <button class="btn" type="button" disabled={busy} onclick={() => (asking = 'option')}>{tr.t('driver.option.use')}</button>
                  {/if}
                  <button class="btn primary" type="button" disabled={busy} onclick={() => (renewing = true)}>{tr.t('driver.renew')}</button>
                </div>
              {/if}
            {:else if profile.contractEnd}
              <div class="fields">
                <div class="fld"><span class="meta">{tr.t('driver.contract.until')}</span><span class="v"><ContractEnd {tr} end={profile.contractEnd} {today} /></span></div>
                {#if profile.upcomingStart}
                  <div class="fld"><span class="meta">{tr.t('driver.contract.from')}</span><span class="v">{formatDate(profile.upcomingStart, tr.lang)}{#if profile.upcomingOrganizationName}<small class="muted"> {profile.upcomingOrganizationName}</small>{/if}</span></div>
                {/if}
              </div>
            {:else}
              <Status text={tr.t('driver.free')} tone="hi" />
            {/if}
          </div>
        </section>

        <section class="panel tbl">
          <header><h2>{tr.t('driver.career')}</h2></header>
          {#if profile.seasons.length > 0}
            <table class="table tight">
              <thead>
                <tr>
                  <th class="c">{tr.t('driver.season')}</th>
                  <th>{tr.t('shell.col.team')}</th>
                  <th class="c">{tr.t('drivers.starts')}</th>
                  <th class="c">{tr.t('shell.col.wins')}</th>
                  <th class="c">{tr.t('drivers.podiums')}</th>
                  <th class="c">{tr.t('driver.retired')}</th>
                  <th class="c">{tr.t('driver.best')}</th>
                </tr>
              </thead>
              <tbody>
                {#each profile.seasons as row (row.season)}
                  <tr>
                    <td class="c num">{row.season}</td>
                    <td>{row.teamName}</td>
                    <td class="c num">{row.starts}</td>
                    <td class="c num">{row.wins || ''}</td>
                    <td class="c num">{row.podiums || ''}</td>
                    <td class="c num">{row.retirements || ''}</td>
                    <td class="c num">{row.best ?? ''}</td>
                  </tr>
                {/each}
                {#if profile.seasons.length > 1}
                  <tr class="mine">
                    <td class="c">Σ</td>
                    <td></td>
                    <td class="c num">{totals.starts}</td>
                    <td class="c num">{totals.wins || ''}</td>
                    <td class="c num">{totals.podiums || ''}</td>
                    <td class="c num">{totals.retirements || ''}</td>
                    <td class="c num">{totals.best ?? ''}</td>
                  </tr>
                {/if}
              </tbody>
            </table>
          {:else}
            <div class="empty"><Status text={tr.t('driver.noRaces')} /></div>
          {/if}
        </section>
      </div>
    </div>
  </div>
{/if}
