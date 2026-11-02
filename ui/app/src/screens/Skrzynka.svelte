<script lang="ts">
  import type { InboxItemView } from '../lib/api/types.generated';
  import MailRow from '../lib/components/MailRow.svelte';
  import Tabs from '../lib/components/Tabs.svelte';
  import { formatDate, formatDay } from '../lib/date.mjs';
  import { mailParts } from '../lib/mail.mjs';
  import { inboxArea } from '../lib/protocol.mjs';
  import type { InboxData } from '../lib/screens';
  import { areaIcon, icon, ICON, type Tr } from '../lib/ui';

  let {
    data,
    tr,
    selectedId,
    busy,
    onConfirm,
    onDismiss,
    onDismissMany,
    onRead,
  }: {
    data: InboxData;
    tr: Tr;
    selectedId: string | null;
    busy: boolean;
    onConfirm: (itemId: string, optionId: string) => void;
    onDismiss: (itemId: string) => void;
    onDismissMany: (itemIds: string[]) => void;
    onRead: (itemId: string) => void;
  } = $props();

  let filter = $state('all');
  let picked = $state<{ item: string; option: string } | null>(null);

  const isDecision = (item: InboxItemView) => item.status === 'Open' && item.needsDecision;
  const isClosed = (item: InboxItemView) => item.status !== 'Open';
  /* A mail the player has deleted leaves the main list and stays in the closed one. */
  const isDeleted = (item: InboxItemView) => item.deleted;
  const isNotice = (item: InboxItemView) => item.status === 'Open' && !item.needsDecision;
  /* An open decision is answered, not deleted; everything else the player can delete. */
  const canDelete = (item: InboxItemView) => !item.deleted && !isDecision(item);

  /* The bridge lists items by number, oldest first; the mailbox shows the newest on top. */
  let items = $derived([...data.inbox.items].reverse());
  let shown = $derived(
    items.filter((item) => (filter === 'decisions' ? isDecision(item) : filter === 'closed' ? isClosed(item) : !isDeleted(item))),
  );
  let notices = $derived(items.filter(isNotice));
  let current = $derived(
    items.find((item) => item.id === selectedId) ??
      items.find((item) => isDecision(item)) ??
      items.find((item) => item.status === 'Open') ??
      items[0] ??
      null,
  );
  let optionId = $derived(picked && current && picked.item === current.id ? picked.option : null);
  let chosen = $derived(current?.options.find((option) => option.id === (current?.chosenOptionId ?? optionId)) ?? null);
  let area = $derived(current ? inboxArea(current.kind) : 'inbox.area.other');
  let parts = $derived(current ? mailParts(tr.tMsg(current.subject)) : null);

  /* The mail in the reader has been seen. */
  $effect(() => {
    if (current && !current.read) onRead(current.id);
  });
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.inbox')}</h1>
  <div class="tools">
    <Tabs
      group="inbox-filter"
      items={[
        { value: 'all', label: tr.t('inbox.tab.all', { count: String(items.filter((item) => !isDeleted(item)).length) }) },
        { value: 'decisions', label: tr.t('inbox.tab.decisions', { count: String(items.filter(isDecision).length) }) },
        { value: 'closed', label: tr.t('inbox.tab.closed', { count: String(items.filter(isClosed).length) }) },
      ]}
      bind:value={filter}
    />
    {#if notices.length > 1}
      <button class="btn sm" type="button" disabled={busy} onclick={() => onDismissMany(notices.map((item) => item.id))}>{tr.t('inbox.deleteNotices')}</button>
    {/if}
  </div>
</div>

<div class="mailbox instant">
  <section class="panel list">
    {#each shown as item (item.id)}
      <div><MailRow {item} {tr} selected={item.id === current?.id} onDelete={canDelete(item) ? onDismiss : null} /></div>
    {:else}
      <p class="muted empty">{tr.t('inbox.empty')}</p>
    {/each}
  </section>
  {#if current}
    <section class="panel reader">
      <div class="rhead">
        <span class="av big">{@html icon(areaIcon(area), 24)}</span>
        <div>
          <div class="muted">{formatDate(current.created, tr.lang)}</div>
          <h2>{tr.t(area)}</h2>
        </div>
        {#if current.status !== 'Open'}<span class="st rstatus">{tr.t(`inbox.status.${current.status}`)}</span>{/if}
      </div>
      {#if parts}
        <div class="letter">
          <p class="lead">{parts.lead}</p>
          {#if parts.fields.length > 0}
            <div class="fields boxed mail-fields">
              {#each parts.fields as field, index (index)}
                <div class="fld">
                  <span class="meta">{field.label}</span>
                  {#if field.items}
                    <span class="v list">{#each field.items as entry, at (at)}<span>{entry}</span>{/each}</span>
                  {:else}
                    <span class="v num">{field.value}</span>
                  {/if}
                </div>
              {/each}
            </div>
          {/if}
          {#each parts.paragraphs as paragraph, index (index)}
            <p>{paragraph}</p>
          {/each}
        </div>
      {/if}
      {#if current.options.length > 0}
        <div class="choices" role="radiogroup" aria-label={tr.t('inbox.choice')}>
          {#each current.options as option (option.id)}
            <button
              type="button"
              class="choice"
              class:chosen={current.chosenOptionId === option.id}
              role="radio"
              aria-checked={(current.chosenOptionId ?? optionId) === option.id}
              disabled={current.status !== 'Open'}
              onclick={() => (picked = { item: current!.id, option: option.id })}
            >
              <span class="ch"><b>{tr.tMsg(option.label)}</b><span class="rd">{#if (current.chosenOptionId ?? optionId) === option.id}{@html icon(ICON.check, 14)}{/if}</span></span>
              {#if option.consequence.key}
                <span class="fx"><span class="row o"><b>·</b><span>{tr.tMsg(option.consequence)}</span></span></span>
              {/if}
            </button>
          {/each}
        </div>
        {#if current.status === 'Open'}
          <div class="confirm">
            <div class="fields">
              {#if current.validUntil}
                <div class="fld"><span class="meta">{tr.t('inbox.deadline')}</span><span class="v bad">{formatDay(current.validUntil, tr.lang)}</span></div>
              {/if}
              <div class="fld"><span class="meta">{tr.t('inbox.choice')}</span><span class="v">{chosen ? tr.tMsg(chosen.label) : '—'}</span></div>
            </div>
            <button class="btn primary" type="button" disabled={!optionId || busy} onclick={() => optionId && onConfirm(current!.id, optionId)}>
              {@html icon(ICON.check, 17)}<span>{tr.t('shell.confirm')}</span>
            </button>
          </div>
        {:else if chosen}
          <div class="stamp">
            <span class="st good">{tr.t('inbox.decided')}</span>
            <b>{tr.tMsg(chosen.label)}</b>
            {#if current.closedOn}<span class="muted">{formatDate(current.closedOn, tr.lang)}</span>{/if}
          </div>
        {/if}
      {/if}
      {#if canDelete(current)}
        <div class="confirm erase">
          <button class="btn" type="button" disabled={busy} onclick={() => onDismiss(current!.id)}>{@html icon(ICON.trash, 17)}<span>{tr.t('inbox.delete')}</span></button>
        </div>
      {/if}
    </section>
  {/if}
</div>
