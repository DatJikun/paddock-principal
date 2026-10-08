<script lang="ts">
  import { SPEEDS } from '../autoplay.mjs';
  import type { Language } from '../i18n';
  import type { Tr } from '../ui';
  import Tabs from './Tabs.svelte';

  type Settings = { autoAdvance: boolean; daySeconds: number; menuMotion: boolean };

  let {
    tr,
    lang = $bindable(),
    settings = $bindable(),
  }: { tr: Tr; lang: Language; settings: Settings } = $props();

  const number = (value: number) => new Intl.NumberFormat(tr.lang === 'en' ? 'en-GB' : 'pl-PL', { maximumFractionDigits: 2 }).format(value);

  let auto = $state(settings.autoAdvance ? 'on' : 'off');
  let speed = $state(String(settings.daySeconds));
  let motion = $state(settings.menuMotion ? 'on' : 'off');

  $effect(() => {
    const next = { autoAdvance: auto === 'on', daySeconds: Number(speed), menuMotion: motion === 'on' };
    if (next.autoAdvance !== settings.autoAdvance || next.daySeconds !== settings.daySeconds || next.menuMotion !== settings.menuMotion) settings = next;
  });
</script>

<div class="settings">
  <section class="panel">
    <header><h2>{tr.t('shell.language')}</h2></header>
    <div class="body">
      <Tabs
        group="lang"
        items={[
          { value: 'pl', label: tr.t('shell.lang.pl') },
          { value: 'en', label: tr.t('shell.lang.en') },
        ]}
        bind:value={lang}
      />
    </div>
  </section>
  <section class="panel">
    <header><h2>{tr.t('shell.next')}</h2></header>
    <div class="body form">
      <div class="fld">
        <span class="meta">{tr.t('settings.autoAdvance')}</span>
        <Tabs
          group="auto-advance"
          items={[
            { value: 'on', label: tr.t('career.toggle.On') },
            { value: 'off', label: tr.t('career.toggle.Off') },
          ]}
          bind:value={auto}
        />
      </div>
      <div class="fld" class:dim={auto !== 'on'}>
        <span class="meta">{tr.t('settings.speed')}</span>
        <Tabs
          group="day-speed"
          items={SPEEDS.map((value) => ({ value: String(value), label: tr.t('settings.speed.value', { seconds: number(value) }), disabled: auto !== 'on' }))}
          bind:value={speed}
        />
      </div>
    </div>
  </section>
  <section class="panel">
    <header><h2>{tr.t('game.menu.title')}</h2></header>
    <div class="body form">
      <div class="fld">
        <span class="meta">{tr.t('settings.menuMotion')}</span>
        <Tabs
          group="menu-motion"
          items={[
            { value: 'on', label: tr.t('career.toggle.On') },
            { value: 'off', label: tr.t('career.toggle.Off') },
          ]}
          bind:value={motion}
        />
      </div>
    </div>
  </section>
</div>
