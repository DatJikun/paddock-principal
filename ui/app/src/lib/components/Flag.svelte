<script lang="ts">
  import { flagInner, flagName } from '../flags.mjs';

  let { code, size = '' }: { code: string; size?: string } = $props();
</script>

<span class="flag {size}" title={flagName(code)}>{@html flagInner(code)}</span>
