<script lang="ts">
  import { hasFlag } from '../flags.mjs';
  import { countryName, type Tr } from '../ui';
  import Flag from './Flag.svelte';

  /* The nationality of a person: the flag and the name of the country, the same on every screen. */
  let { tr, code, size = 'md' }: { tr: Tr; code: string; size?: string } = $props();
</script>

{#if hasFlag(code)}<Flag {code} {size} />{/if}{countryName(tr, code)}
