<script lang="ts">
  import { untrack } from 'svelte';
  import type { DriverProfileView } from '../lib/api/types.generated';
  import Nationality from '../lib/components/Nationality.svelte';
  import Status from '../lib/components/Status.svelte';
  import Tabs from '../lib/components/Tabs.svelte';
  import { bandOf, bandText, careerTotals, DRIVER_ATTRS, seasonRow } from '../lib/people.mjs';
  import { driverHref, formatAge } from '../lib/person.mjs';
  import type { CompareData } from '../lib/screens';
  import { icon, ICON, initials, type Tr } from '../lib/ui';

  let { data, tr }: { data: CompareData; tr: Tr } = $props();

  let a = $derived(data.a);
  let b = $derived(data.b);
  let rated = $derived(a.attributes.length > 0 && b.attributes.length > 0);
  let tab = $state(untrack(() => (data.a.attributes.length > 0 && data.b.attributes.length > 0 ? 'attr' : 'season')));

  type Line = { label: string; left: number | string | null; right: number | string | null; better: 'hi' | 'lo' | 'none' };

  function winner(line: Line) {
    if (line.better === 'none' || typeof line.left !== 'number' || typeof line.right !== 'number' || line.left === line.right) return null;
    return (line.better === 'hi' ? line.left > line.right : line.left < line.right) ? 'a' : 'b';
  }

  function stats(profile: DriverProfileView, season: number) {
    const row = seasonRow(profile, season);
    return { starts: row?.starts ?? 0, wins: row?.wins ?? 0, podiums: row?.podiums ?? 0, retirements: row?.retirements ?? 0, best: row?.best ?? null };
  }

  let seasonLines = $derived.by(() => {
    const left = stats(a, data.season);
    const right = stats(b, data.season);
    return [
      { label: tr.t('drivers.starts'), left: left.starts, right: right.starts, better: 'hi' },
      { label: tr.t('shell.col.wins'), left: left.wins, right: right.wins, better: 'hi' },
      { label: tr.t('drivers.podiums'), left: left.podiums, right: right.podiums, better: 'hi' },
      { label: tr.t('driver.retired'), left: left.retirements, right: right.retirements, better: 'lo' },
      { label: tr.t('driver.best'), left: left.best, right: right.best, better: 'lo' },
    ] as Line[];
  });

  let careerLines = $derived.by(() => {
    const left = careerTotals(a);
    const right = careerTotals(b);
    return [
      { label: tr.t('drivers.starts'), left: left.starts, right: right.starts, better: 'hi' },
      { label: tr.t('shell.col.wins'), left: left.wins, right: right.wins, better: 'hi' },
      { label: tr.t('drivers.podiums'), left: left.podiums, right: right.podiums, better: 'hi' },
      { label: tr.t('driver.retired'), left: left.retirements, right: right.retirements, better: 'lo' },
      { label: tr.t('driver.best'), left: left.best, right: right.best, better: 'lo' },
    ] as Line[];
  });

  let tabs = $derived([
    ...(rated ? [{ value: 'attr', label: tr.t('driver.attributes') }] : []),
    { value: 'season', label: tr.t('compare.season', { season: String(data.season) }) },
    { value: 'career', label: tr.t('compare.career') },
  ]);
  let shown = $derived(tabs.some((item) => item.value === tab) ? tab : (tabs[0]?.value ?? 'season'));
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('drivers.compare')}</h1>
  <div class="tools">
    <a class="btn" href="#/kierowcy">{@html icon(ICON.back, 17)}<span>{tr.t('driver.squad')}</span></a>
  </div>
</div>

{#if !a.found || !b.found}
  <Status text={tr.t('driver.unknown')} tone="warn" />
{:else}
  {#snippet side(profile: DriverProfileView, cls: string)}
    <a class="cmp-side {cls}" href={driverHref(profile.personId)}>
      <span class="face">{initials(profile.name)}</span>
      <div>
        <h2>{profile.name}</h2>
        <div class="fields">
          <div class="fld">
            <span class="meta">{tr.t('career.you.country')}</span>
            <span class="v"><Nationality {tr} code={profile.nationality} /></span>
          </div>
          <div class="fld"><span class="meta">{tr.t('drivers.age')}</span><span class="v num">{formatAge(profile.age)}</span></div>
        </div>
      </div>
    </a>
  {/snippet}

  <section class="panel cmp">
    <div class="cmp-top">
      {@render side(a, 'l')}
      <div class="cmp-mid"><Tabs group="compare" items={tabs} bind:value={tab} /></div>
      {@render side(b, 'r')}
    </div>
    <div class="cmp-body">
      {#if shown === 'attr'}
        {#each DRIVER_ATTRS as key (key)}
          {@const left = bandOf(a.attributes, key)}
          {@const right = bandOf(b.attributes, key)}
          {#if left && right}
            <div class="cmprow bars">
              <span class="num v" class:win={left.low + left.high > right.low + right.high}>{bandText(left.low, left.high)}</span>
              <div class="bar thin rev"><i style="width:{((left.low + left.high) / 2) * 5}%"></i></div>
              <span class="lbl">{tr.t(`attr.${key}`)}</span>
              <div class="bar thin"><i style="width:{((right.low + right.high) / 2) * 5}%;background:var(--t2)"></i></div>
              <span class="num v" class:win={right.low + right.high > left.low + left.high}>{bandText(right.low, right.high)}</span>
            </div>
          {/if}
        {/each}
      {:else if (shown === 'season' ? seasonLines : careerLines).every((line) => !line.left && !line.right)}
        <div class="empty"><Status text={tr.t('driver.noRaces')} /></div>
      {:else}
        {#each shown === 'season' ? seasonLines : careerLines as line (line.label)}
          {@const won = winner(line)}
          <div class="cmprow">
            <span class="num v" class:win={won === 'a'}>{line.left ?? '—'}</span>
            <span class="lbl">{line.label}</span>
            <span class="num v" class:win={won === 'b'}>{line.right ?? '—'}</span>
          </div>
        {/each}
      {/if}
    </div>
  </section>
{/if}
