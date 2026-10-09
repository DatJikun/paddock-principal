<script lang="ts">
  import type { SponsorIndustryBonusView, SponsorWishView } from '../api/types.generated';
  import { countryName, type Tr } from '../ui';
  import Status from './Status.svelte';

  /** What a sponsor adds on top of the price: a wish for a driver of one nationality (a bonus only) and the bonus of its industry. */
  let {
    tr,
    wish = null,
    industry = null,
  }: {
    tr: Tr;
    wish?: SponsorWishView | null;
    industry?: SponsorIndustryBonusView | null;
  } = $props();

  /* The backend sends thousandths of the annual amount; a whole percent is how a person reads them. */
  const share = (milli: number) => `${milli / 10}%`;
</script>

{#if wish}
  <div class="sp-goal">
    <span class="meta">{tr.t('sponsor.wish')}</span>
    <b>{tr.t(wish.raceSeat ? 'sponsor.wish.raceSeat' : 'sponsor.wish.reserve', { country: countryName(tr, wish.nationality), percent: share(wish.bonusMilli) })}</b>
    {#if wish.met !== null}<Status text={tr.t(wish.met ? 'sponsor.wish.met' : 'sponsor.wish.unmet')} tone={wish.met ? 'good' : ''} />{/if}
  </div>
{/if}
{#if industry}
  <div class="sp-goal">
    <span class="meta">{tr.t('sponsor.bonus')}</span>
    <b>{tr.t(`sponsor.bonus.${industry.kind}`, { percent: share(industry.milli) })}</b>
  </div>
{/if}
