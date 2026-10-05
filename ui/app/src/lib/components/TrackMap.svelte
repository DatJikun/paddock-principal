<script lang="ts">
  import type { TrackPointView } from '../api/types.generated';
  import { trackOutline } from '../track.mjs';

  let { points, cls = '', label = '' }: { points: TrackPointView[] | null | undefined; cls?: string; label?: string } = $props();

  let outline = $derived(trackOutline(points ?? []));
</script>

{#if outline}
  <svg class="trk {cls}" viewBox={outline.viewBox} role="img" aria-label={label}>
    <path class="road" d={outline.d} />
    <path class="line" d={outline.d} />
    <path class="sf" d={outline.tick} />
  </svg>
{/if}
