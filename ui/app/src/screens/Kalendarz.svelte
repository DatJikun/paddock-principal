<script lang="ts">
  import Flag from '../lib/components/Flag.svelte';
  import TrackMap from '../lib/components/TrackMap.svelte';
  import { formatDay } from '../lib/date.mjs';
  import { hasFlag } from '../lib/flags.mjs';
  import type { CalendarData } from '../lib/screens';
  import { countryName, type Tr } from '../lib/ui';

  let { data, tr }: { data: CalendarData; tr: Tr } = $props();
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('calendar.title', { season: String(data.calendar.season) })}</h1>
  <div class="fields">
    <div class="fld"><span class="meta">{tr.t('calendar.rounds')}</span><span class="v num">{data.calendar.rounds.length}</span></div>
  </div>
</div>

<div class="cal">
  {#each data.calendar.rounds as round (round.round)}
    <a
      class="rnd"
      class:past={round.finished}
      class:next={round.round === data.next.round && round.season === data.next.season}
      href={`#/wyscig/${round.round}`}
    >
      <span class="rno">{round.round}</span>
      <div class="rinfo">
        <span class="rdate">{round.race ? formatDay(round.race, tr.lang) : ''}</span>
        <b>{#if hasFlag(round.country)}<Flag code={round.country} size="md" />{/if}<span>{countryName(tr, round.country)}</span></b>
        <small>{round.circuitName}</small>
      </div>
      <TrackMap points={data.tracks[round.layoutId]?.points} label={round.circuitName} />
    </a>
  {/each}
</div>
