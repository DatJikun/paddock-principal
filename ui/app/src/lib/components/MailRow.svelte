<script lang="ts">
  import type { InboxItemView } from '../api/types.generated';
  import { formatDay } from '../date.mjs';
  import { inboxArea } from '../protocol.mjs';
  import { areaIcon, icon, type Tr } from '../ui';

  let { item, tr, selected = false }: { item: InboxItemView; tr: Tr; selected?: boolean } = $props();

  let area = $derived(inboxArea(item.kind));
  let open = $derived(item.status === 'Open');
  let deciding = $derived(open && item.needsDecision);
</script>

<a
  class="mail"
  class:decision={deciding}
  class:read={!open}
  class:sel={selected}
  href={`#/skrzynka/${encodeURIComponent(item.id)}`}
  aria-current={selected ? 'true' : undefined}
>
  <span class="av">{@html icon(areaIcon(area), 17)}</span>
  <div>
    <div class="from">
      {tr.t(area)}
      {#if deciding && item.validUntil}<span class="st bad solid">{formatDay(item.validUntil, tr.lang)}</span>{/if}
    </div>
    <div class="t">{tr.tMsg(item.subject)}</div>
  </div>
  <span class="when">{formatDay(item.created, tr.lang)}</span>
</a>
