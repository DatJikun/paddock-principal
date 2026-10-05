<script lang="ts">
  import { bandText } from '../people.mjs';

  let { label, low, high, max = 20 }: { label: string; low: number; high: number; max?: number } = $props();

  let exact = $derived(low === high);
  let start = $derived(Math.max(0, ((low - 1) / max) * 100));
  let width = $derived(Math.max(2.5, ((high - low + 1) / max) * 100));
</script>

<div class="arow">
  <span>{label}</span>
  <span class="attr" class:band={!exact} class:a4={exact && low / max >= 0.85}>{bandText(low, high)}</span>
  <div class="bar thin" class:band={!exact}>
    {#if exact}<i style="width:{(low / max) * 100}%"></i>{:else}<i style="margin-left:{start}%;width:{width}%"></i>{/if}
  </div>
</div>
