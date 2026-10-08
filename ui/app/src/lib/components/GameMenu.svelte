<script lang="ts">
  import type { SaveListItem } from '../api/types.generated';
  import { newestFirst, sameSave, saveLabel } from '../career.mjs';
  import { formatDate } from '../date.mjs';
  import type { Language } from '../i18n';
  import { icon, ICON, type Tr } from '../ui';
  import SettingsPanel from './SettingsPanel.svelte';
  import LoadList from './LoadList.svelte';
  import Status from './Status.svelte';

  let {
    tr,
    saves,
    savedName,
    unsavedSince,
    busy,
    error,
    lang = $bindable(),
    settings = $bindable(),
    onClear,
    onSave,
    onLoad,
    onToMenu,
    onClose,
  }: {
    tr: Tr;
    saves: SaveListItem[];
    /** The file this career was last saved to or loaded from, or null when it has none yet. */
    savedName: string | null;
    /** The career date of the last save when later days have been played since, else null. */
    unsavedSince: string | null;
    busy: boolean;
    error: string;
    lang: Language;
    settings: { autoAdvance: boolean; daySeconds: number; menuMotion: boolean };
    onClear: () => void;
    onSave: (name: string) => void;
    onLoad: (name: string) => void;
    onToMenu: () => void;
    onClose: () => void;
  } = $props();

  type View = 'list' | 'saveAs' | 'load' | 'settings' | 'toMenu';

  let view = $state<View>('list');
  let draft = $state('');
  let picked = $state('');

  let ordered = $derived(newestFirst(saves));
  let exists = $derived(draft.trim().length > 0 && saves.some((save) => sameSave(save.name, draft.trim())));

  function open(next: View) {
    onClear();
    if (next === 'saveAs') draft = savedName ? saveLabel(savedName) : '';
    if (next === 'load') picked = '';
    view = next;
  }

  function save() {
    if (savedName) onSave(savedName);
    else open('saveAs');
  }

  function submitName() {
    const name = draft.trim();
    if (name && !busy) onSave(name);
  }

  function key(event: KeyboardEvent) {
    if (event.key !== 'Escape') return;
    event.preventDefault();
    event.stopPropagation();
    if (view === 'list') onClose();
    else view = 'list';
  }

  const items: { id: View | 'save'; key: string }[] = [
    { id: 'save', key: 'game.menu.save' },
    { id: 'saveAs', key: 'game.menu.saveAs' },
    { id: 'load', key: 'game.menu.load' },
    { id: 'settings', key: 'game.menu.settings' },
    { id: 'toMenu', key: 'game.menu.toMenu' },
  ];
</script>

<svelte:window onkeydown={key} />

<div class="scrim" role="presentation" onclick={(event) => event.target === event.currentTarget && onClose()}>
  <div class="panel gmenu" role="dialog" aria-modal="true" aria-label={tr.t('game.menu.title')}>
    <header>
      <h2>{tr.t(view === 'list' ? 'game.menu.title' : `game.menu.${view}`)}</h2>
      <button class="btn sm" type="button" aria-label={tr.t('game.menu.resume')} onclick={onClose}>{@html icon(ICON.back, 16)}<span>{tr.t('game.menu.resume')}</span></button>
    </header>
    <div class="body">
      {#if view === 'list'}
        <div class="gm-list">
          {#each items as item (item.id)}
            <button
              type="button"
              class="gm-item"
              disabled={item.id === 'load' && saves.length === 0}
              onclick={() => (item.id === 'save' ? save() : open(item.id))}
            >
              <b>{tr.t(item.key)}</b>
              {#if item.id === 'save' && savedName}<small>{saveLabel(savedName)}</small>{/if}
              {#if item.id === 'load' && saves.length === 0}<small>{tr.t('menu.noSaves')}</small>{/if}
              {@html icon(ICON.arrow, 18)}
            </button>
          {/each}
        </div>
      {:else if view === 'saveAs'}
        <div class="gm-form">
          <label class="fld">
            <span class="meta">{tr.t('save.name')}</span>
            <!-- svelte-ignore a11y_autofocus -->
            <input class="text" type="text" bind:value={draft} autocomplete="off" autofocus onkeydown={(event) => event.key === 'Enter' && submitName()} />
          </label>
          {#if ordered.length > 0}
            <div class="fld">
              <span class="meta">{tr.t('save.existing')}</span>
              <div class="gm-saves">
                {#each ordered as save (save.name)}
                  <button type="button" class="gm-save" class:sel={sameSave(save.name, draft)} onclick={() => (draft = saveLabel(save.name))}>
                    <b>{saveLabel(save.name)}</b>
                    <small class="num">{formatDate(save.date, tr.lang)}</small>
                  </button>
                {/each}
              </div>
            </div>
          {/if}
          {#if exists}<Status text={tr.t('save.exists')} tone="warn" />{/if}
          {#if error}<p class="bad">{error}</p>{/if}
          <div class="confirm">
            <button class="btn" type="button" onclick={() => (view = 'list')}>{@html icon(ICON.back, 17)}<span>{tr.t('career.back')}</span></button>
            <button class="btn primary" type="button" disabled={!draft.trim() || busy} onclick={submitName}>
              {@html icon(ICON.check, 17)}<span>{exists ? tr.t('save.overwrite') : tr.t('game.menu.save')}</span>
            </button>
          </div>
        </div>
      {:else if view === 'load'}
        <div class="gm-form">
          <LoadList saves={ordered} {tr} selected={picked} onSelect={(name) => (picked = name)} />
          {#if unsavedSince}<Status text={tr.t('game.menu.unsaved', { date: formatDate(unsavedSince, tr.lang) })} tone="warn" />{/if}
          {#if error}<p class="bad">{error}</p>{/if}
          <div class="confirm">
            <button class="btn" type="button" onclick={() => (view = 'list')}>{@html icon(ICON.back, 17)}<span>{tr.t('career.back')}</span></button>
            <button class="btn primary" type="button" disabled={!picked || busy} onclick={() => onLoad(picked)}>
              {@html icon(ICON.check, 17)}<span>{tr.t('game.menu.load')}</span>
            </button>
          </div>
        </div>
      {:else if view === 'settings'}
        <div class="gm-form">
          <SettingsPanel {tr} bind:lang bind:settings />
          <div class="confirm">
            <button class="btn" type="button" onclick={() => (view = 'list')}>{@html icon(ICON.back, 17)}<span>{tr.t('career.back')}</span></button>
          </div>
        </div>
      {:else}
        <div class="gm-form">
          <p class="gm-ask">{tr.t('game.menu.toMenu.ask')}</p>
          {#if unsavedSince}<Status text={tr.t('game.menu.unsaved', { date: formatDate(unsavedSince, tr.lang) })} tone="warn" />{/if}
          <div class="confirm">
            <button class="btn" type="button" onclick={() => (view = 'list')}>{tr.t('game.menu.cancel')}</button>
            <button class="btn primary" type="button" disabled={busy} onclick={onToMenu}>{@html icon(ICON.check, 17)}<span>{tr.t('shell.confirm')}</span></button>
          </div>
        </div>
      {/if}
    </div>
  </div>
</div>
