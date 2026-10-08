<script lang="ts">
  import { emblemOf } from '../emblem.mjs';
  import { livery } from '../livery.mjs';

  let { id, name, size = 52 }: { id: string; name: string; size?: number } = $props();

  let colours = $derived(livery(id));
  let mark = $derived(emblemOf(id, name));
</script>

<svg class="emblem" width={size} height={size} viewBox="0 0 48 48" aria-hidden="true">
  {#if mark.shape === 'circle'}
    <circle cx="24" cy="24" r="21" fill={colours.main} stroke={colours.accent} stroke-width="3.5" />
  {:else if mark.shape === 'roundel'}
    <circle cx="24" cy="24" r="21.5" fill={colours.accent} />
    <circle cx="24" cy="24" r="15.5" fill={colours.main} />
  {:else if mark.shape === 'shield'}
    <path d="M7 5h34v20c0 10-8 16-17 19C15 41 7 35 7 25z" fill={colours.main} stroke={colours.accent} stroke-width="3.5" stroke-linejoin="round" />
  {:else if mark.shape === 'diamond'}
    <path d="M24 3l21 21-21 21L3 24z" fill={colours.main} stroke={colours.accent} stroke-width="3.5" stroke-linejoin="round" />
  {:else if mark.shape === 'hex'}
    <path d="M24 3l18 10.5v21L24 45 6 34.5v-21z" fill={colours.main} stroke={colours.accent} stroke-width="3.5" stroke-linejoin="round" />
  {:else}
    <rect x="5" y="5" width="38" height="38" rx="6" fill={colours.main} stroke={colours.accent} stroke-width="3.5" />
  {/if}
  <text x="24" y="30" text-anchor="middle" font-family="Big Shoulders Display, sans-serif" font-weight="900" font-size={mark.letters.length > 1 ? 19 : 24} fill={colours.on}>{mark.letters}</text>
</svg>
