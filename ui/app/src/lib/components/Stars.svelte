<script lang="ts">
  import { starsOf } from '../people.mjs';

  /** The overall on the 1-20 scale of the attributes; shown as 0-5 stars in halves (PP-040). */
  let { overall, label = '' }: { overall: number | null; label?: string } = $props();

  const PATH = 'M12 3l2.6 5.3 5.9.9-4.3 4.1 1 5.8L12 16.4 6.8 19.1l1-5.8L3.5 9.2l5.9-.9z';
  let stars = $derived(overall === null ? null : starsOf(overall));
</script>

{#if stars !== null}
  <span class="stars" role="img" aria-label={`${label} ${stars}/5`.trim()} title={String(stars)}>
    {#each [0, 1, 2, 3, 4] as index (index)}
      {@const fill = Math.min(1, Math.max(0, stars - index))}
      <span class="star">
        <svg viewBox="0 0 24 24" aria-hidden="true"><path class="e" d={PATH} /></svg>
        {#if fill > 0}
          <svg viewBox="0 0 24 24" aria-hidden="true" style={`clip-path: inset(0 ${(1 - fill) * 100}% 0 0)`}><path class="f" d={PATH} /></svg>
        {/if}
      </span>
    {/each}
  </span>
{/if}
