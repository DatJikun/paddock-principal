<script lang="ts">
  import { flagInner } from '../flags.mjs';
  import { getLanguage, subscribeLanguage, type Language } from '../i18n';
  import { countryName, translator } from '../ui';

  let { code, size = '' }: { code: string; size?: string } = $props();

  let lang = $state<Language>(getLanguage());
  $effect(() =>
    subscribeLanguage(() => {
      lang = getLanguage();
    }),
  );
</script>

<span class="flag {size}" title={countryName(translator(lang), code)}>{@html flagInner(code)}</span>
