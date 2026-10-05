<script lang="ts">
  import { hasFlag } from '../flags.mjs';
  import { initials } from '../ui';
  import Flag from './Flag.svelte';

  let {
    name,
    href,
    nationality,
    sub = '',
  }: {
    name: string;
    href?: string;
    nationality?: string;
    sub?: string;
  } = $props();
</script>

{#snippet inner()}
  <span class="av">{initials(name)}</span>
  <div>
    <b>{name}</b>
    <small>{#if nationality && hasFlag(nationality)}<Flag code={nationality} />{/if}{sub}</small>
  </div>
{/snippet}

{#if href}
  <a class="person" {href}>{@render inner()}</a>
{:else}
  <span class="person">{@render inner()}</span>
{/if}
