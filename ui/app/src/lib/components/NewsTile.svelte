<script lang="ts">
  import type { HeadlineView } from '../api/types.generated';
  import { formatDay } from '../date.mjs';
  import { headlineHref } from '../news.mjs';
  import type { Tr } from '../ui';

  let { item, tr }: { item: HeadlineView; tr: Tr } = $props();
</script>

<a class="news" class:mine={item.mine} href={headlineHref(item.link)}>
  <div class="from">
    <span>{tr.tMsg(item.label)}</span>
    <span class="when">{formatDay(item.date, tr.lang)}</span>
  </div>
  <div class="t">{tr.tMsg(item.title)}</div>
  {#if item.line}<div class="sub">{tr.tMsg(item.line)}</div>{/if}
</a>
