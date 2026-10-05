<script lang="ts">
  import { icon, ICON, type Tr } from '../ui';

  let {
    tr,
    ask,
    busy = false,
    onConfirm,
    onCancel,
  }: {
    tr: Tr;
    /** What is about to happen, in words, so the confirm is never a blind click. */
    ask: string;
    busy?: boolean;
    onConfirm: () => void;
    onCancel: () => void;
  } = $props();
</script>

<div class="confirm ask" role="group" aria-label={ask}>
  <b>{ask}</b>
  <div class="ask-btns">
    <button class="btn" type="button" disabled={busy} onclick={onCancel}>{tr.t('game.menu.cancel')}</button>
    <button class="btn primary" type="button" disabled={busy} onclick={onConfirm}>{@html icon(ICON.check, 17)}<span>{tr.t('shell.confirm')}</span></button>
  </div>
</div>
