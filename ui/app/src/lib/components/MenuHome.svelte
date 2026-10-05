<script lang="ts">
  import type { SaveListItem } from '../api/types.generated';
  import { saveLabel, teamLabel } from '../career.mjs';
  import { formatDate } from '../date.mjs';
  import { icon, ICON, type Tr } from '../ui';

  let {
    tr,
    latest,
    busy,
    onContinue,
  }: {
    tr: Tr;
    latest: SaveListItem | null;
    busy: boolean;
    onContinue: () => void;
  } = $props();
</script>

<div class="hero">
  <h1 class="hero-title"><span>Paddock</span><span>Principal</span></h1>
  <i class="hero-stripes" aria-hidden="true"></i>
  {#if latest}
    <section class="panel hero-save">
      <header><h2>{teamLabel(latest.teamId)}</h2><span class="meta">{saveLabel(latest.name)}</span></header>
      <div class="body">
        <div class="fields">
          <div class="fld"><span class="meta">{tr.t('load.col.career')}</span><span class="v">{latest.careerName}</span></div>
          <div class="fld"><span class="meta">{tr.t('shell.col.date')}</span><span class="v num">{formatDate(latest.date, tr.lang)}</span></div>
        </div>
        <div class="confirm">
          <button class="btn primary" type="button" disabled={busy} onclick={onContinue}>
            {@html icon(ICON.play, 17)}<span>{tr.t('menu.continue')}</span>
          </button>
        </div>
      </div>
    </section>
  {/if}
</div>
