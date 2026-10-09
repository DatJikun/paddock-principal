<script lang="ts">
  import { hasFlag } from '../flags.mjs';
  import { driverHref } from '../person.mjs';
  import Flag from './Flag.svelte';

  /* A driver named in a list of results or standings: flag, name, and the link to the shared profile when the list knows his id. */
  let {
    name,
    id = '',
    nationality = '',
    bold = true,
  }: {
    name: string;
    id?: string;
    nationality?: string;
    bold?: boolean;
  } = $props();
</script>

{#snippet inner()}{#if hasFlag(nationality)}<Flag code={nationality} />{/if}{#if bold}<b>{name}</b>{:else}{name}{/if}{/snippet}

{#if id}
  <a class="person" href={driverHref(id)}>{@render inner()}</a>
{:else}
  <span class="person">{@render inner()}</span>
{/if}
