<script lang="ts">
  import type { SaveListItem } from '../api/types.generated';
  import { saveLabel, teamLabel } from '../career.mjs';
  import { formatDate } from '../date.mjs';
  import { icon, ICON, type Tr } from '../ui';

  let {
    tr,
    latest,
    saveCount,
    busy,
    canQuit,
    onContinue,
    onOpen,
    onQuit,
  }: {
    tr: Tr;
    latest: SaveListItem | null;
    saveCount: number;
    busy: boolean;
    canQuit: boolean;
    onContinue: () => void;
    onOpen: (page: 'new' | 'load' | 'settings') => void;
    onQuit: () => void;
  } = $props();
</script>

<div class="title-screen">
  <div class="title-art">
    <h1 class="hero-title"><span>Paddock</span><span>Principal</span></h1>
    <i class="hero-stripes" aria-hidden="true"></i>
  </div>
  <nav class="title-menu" aria-label={tr.t('game.menu.title')}>
    {#if latest}
      <button type="button" class="tm tm-continue" disabled={busy} onclick={onContinue}>
        <span class="tm-icon">{@html icon(ICON.play, 26)}</span>
        <span class="tm-main">
          <b>{tr.t('menu.continue')}</b>
          <span class="tm-sub">
            <span><small class="meta">{tr.t('load.col.career')}</small>{latest.careerName}</span>
            <span><small class="meta">{tr.t('shell.start.team')}</small>{teamLabel(latest.teamId)}</span>
            <span><small class="meta">{tr.t('shell.col.date')}</small><span class="num">{formatDate(latest.date, tr.lang)}</span></span>
            <span><small class="meta">{tr.t('shell.save.name')}</small>{saveLabel(latest.name)}</span>
          </span>
        </span>
        {@html icon(ICON.arrow, 24)}
      </button>
    {/if}
    <button type="button" class="tm" onclick={() => onOpen('new')}>
      <span class="tm-icon">{@html icon(ICON.plus, 24)}</span><b>{tr.t('menu.new')}</b>{@html icon(ICON.arrow, 22)}
    </button>
    <button type="button" class="tm" disabled={saveCount === 0} onclick={() => onOpen('load')}>
      <span class="tm-icon">{@html icon(ICON.folder, 24)}</span><b>{tr.t('menu.load')}</b>
      {#if saveCount === 0}<small class="muted">{tr.t('menu.noSaves')}</small>{:else}<small class="count">{saveCount}</small>{/if}
      {@html icon(ICON.arrow, 22)}
    </button>
    <button type="button" class="tm" onclick={() => onOpen('settings')}>
      <span class="tm-icon">{@html icon(ICON.gear, 24)}</span><b>{tr.t('menu.settings')}</b>{@html icon(ICON.arrow, 22)}
    </button>
    {#if canQuit}
      <button type="button" class="tm" onclick={onQuit}>
        <span class="tm-icon">{@html icon(ICON.exit, 24)}</span><b>{tr.t('menu.quit')}</b>{@html icon(ICON.arrow, 22)}
      </button>
    {/if}
  </nav>
</div>
