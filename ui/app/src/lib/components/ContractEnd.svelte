<script lang="ts">
  import { formatDate } from '../date.mjs';
  import { endsThisSeason } from '../people.mjs';
  import type { Tr } from '../ui';

  /* The end of a contract: the date turns red in the last season (HANDOFF 3.1). `quiet` keeps it plain where the date is not the
     viewer's to worry about (somebody else's contract, a renewal already signed). */
  let { tr, end, today, quiet = false }: { tr: Tr; end: string | null | undefined; today: string; quiet?: boolean } = $props();
</script>

<span class="num" class:bad={!quiet && endsThisSeason(end, today)}>{formatDate(end ?? '', tr.lang)}</span>
