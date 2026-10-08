<script lang="ts">
  import type { TrackPointView } from '../api/types.generated';
  import { trackOutline } from '../track.mjs';

  let { points, cls = '', label = '', pit = false }: { points: TrackPointView[] | null | undefined; cls?: string; label?: string; pit?: boolean } = $props();

  let outline = $derived(trackOutline(points ?? []));
</script>

{#if outline}
  <svg class="trk {cls}" viewBox={outline.viewBox} role="img" aria-label={label}>
    <path class="road" d={outline.d} />
    <path class="line" d={outline.d} />
    {#if pit}<path class="pit" d={outline.pit} />{/if}
    <path class="sf" d={outline.tick} />
  </svg>
{/if}
