<script lang="ts">
  import { icon, ICON } from '../ui';

  export type Toast = { key: number; title: string; text: string; href: string | null; action: string };

  let { items, onClose }: { items: Toast[]; onClose: (key: number) => void } = $props();
</script>

<div class="toasts" aria-live="polite">
  {#each items as item (item.key)}
    <div class="toast" role="status">
      {#if item.href}
        <a class="tbody" href={item.href} onclick={() => onClose(item.key)}>
          <span class="meta">{item.title}</span>
          <span class="tx">{item.text}</span>
          <span class="go-on">{item.action}{@html icon(ICON.arrow, 15)}</span>
        </a>
      {:else}
        <div class="tbody">
          {#if item.title}<span class="meta">{item.title}</span>{/if}
          <span class="tx">{item.text}</span>
        </div>
      {/if}
      <button type="button" class="x" aria-label="×" onclick={() => onClose(item.key)}>×</button>
    </div>
  {/each}
</div>
