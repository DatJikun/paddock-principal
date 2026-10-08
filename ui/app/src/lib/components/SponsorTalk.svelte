<script lang="ts">
  import { untrack } from 'svelte';
  import type { BridgeCommandName, SponsorTalkView } from '../api/types.generated';
  import { formatMoney } from '../money.mjs';
  import type { Tr } from '../ui';
  import Confirmation from './Confirmation.svelte';
  import SponsorExtras from './SponsorExtras.svelte';
  import Status from './Status.svelte';
  import TermsPicker from './TermsPicker.svelte';

  let {
    talk,
    tr,
    teamId,
    busy,
    act,
  }: {
    talk: SponsorTalkView;
    tr: Tr;
    teamId: string;
    busy: boolean;
    act: (name: BridgeCommandName, args: Record<string, unknown>) => Promise<boolean>;
  } = $props();

  type Ask = 'sign' | 'leave' | 'terms';

  /* The picker starts on the terms that are on the table; changing it changes nothing until the player confirms. */
  let years = $state(untrack(() => String(talk.years)));
  let ambition = $state(untrack(() => talk.ambition));
  let asking = $state<Ask | null>(null);
  let askMilli = $state('0');

  let changed = $derived(years !== String(talk.years) || (talk.ambitionOpen && ambition !== talk.ambition));
  /* An ask belongs to signing what is on the table: choosing other terms starts again from the quote. */
  $effect(() => {
    if (changed) askMilli = '0';
  });
  let asked = $derived(talk.quotes.find((item) => String(item.years) === String(talk.years) && item.ambition === (talk.ambitionOpen ? talk.ambition : 'standard'))?.asks.find((item) => String(item.milli) === askMilli) ?? null);
  let chosen = $derived(talk.quotes.find((item) => String(item.years) === years && item.ambition === (talk.ambitionOpen ? ambition : 'standard')) ?? null);
  const termsText = () =>
    talk.ambitionOpen
      ? tr.t('sponsor.terms.summary', { years: tr.t(`sponsor.terms.years.${years}`), level: tr.t(`sponsor.ambition.${ambition}`).toLowerCase() })
      : tr.t(`sponsor.terms.years.${years}`);

  const askText = (ask: Ask) =>
    ask === 'sign'
      ? tr.t('sponsor.ask.sign', {
          name: talk.sponsorName,
          terms: tr.t('sponsor.terms.summary', { years: tr.t(`sponsor.terms.years.${talk.years}`), level: tr.t(`sponsor.ambition.${talk.ambition}`).toLowerCase() }),
          amount: formatMoney(asked?.annualCents ?? talk.currentAnnualCents, tr.lang),
        })
      : ask === 'leave'
        ? tr.t('sponsor.ask.leave', { name: talk.sponsorName })
        : tr.t('sponsor.ask.terms', { name: talk.sponsorName, terms: termsText() });

  async function run() {
    const ask = asking;
    asking = null;
    if (ask === 'sign') await act('signSponsor', { organizationId: teamId, talkId: talk.id, askMilli: Number(askMilli) });
    else if (ask === 'leave') await act('walkAwayFromTalks', { organizationId: teamId, talkId: talk.id });
    else if (ask === 'terms') await act('proposeSponsorTerms', { organizationId: teamId, talkId: talk.id, years: Number(years), ambition: talk.ambitionOpen ? ambition : 'standard' });
  }
</script>

<h3>{talk.sponsorName}</h3>
<TermsPicker {tr} group={`talk-${talk.id}`} quotes={talk.quotes} ambitionOpen={talk.ambitionOpen} bind:years bind:ambition cap partnership={talk.partnership} askable={!changed} bind:askMilli />
<SponsorExtras {tr} wish={talk.wish} industry={talk.industryBonus} />
{#if talk.rivalKnown}<Status text={tr.t('sponsor.rival')} tone="warn" />{/if}
{#if asking}
  <Confirmation {tr} {busy} ask={askText(asking)} onCancel={() => (asking = null)} onConfirm={run} />
{:else}
  <div class="confirm">
    <button class="btn" type="button" disabled={busy} onclick={() => (asking = 'leave')}>{tr.t('sponsor.leave')}</button>
    {#if changed}
      <button class="btn primary" type="button" disabled={busy || chosen === null} onclick={() => (asking = 'terms')}>{tr.t('sponsor.change')}</button>
    {:else}
      <button class="btn primary" type="button" disabled={busy} onclick={() => (asking = 'sign')}>{tr.t('sponsor.sign')}</button>
    {/if}
  </div>
{/if}
