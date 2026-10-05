<script lang="ts">
  import type { Language } from '../i18n';
  import type { Tr } from '../ui';
  import Tabs from './Tabs.svelte';

  let { tr, value = $bindable() }: { tr: Tr; value: Language } = $props();
</script>

<div class="fields">
  <div class="fld">
    <span class="meta">{tr.t('shell.language')}</span>
    <Tabs
      group="lang"
      items={[
        { value: 'pl', label: tr.t('shell.lang.pl') },
        { value: 'en', label: tr.t('shell.lang.en') },
      ]}
      bind:value
    />
  </div>
</div>
