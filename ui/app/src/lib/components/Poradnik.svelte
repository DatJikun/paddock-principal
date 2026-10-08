<script lang="ts" module>
  /* Which page of the book was open last, kept while the app runs so coming back from the menu does not lose the place. */
  let lastPage = 'wstep';
</script>

<script lang="ts">
  import { onMount } from 'svelte';
  import { pageAround, pageById, pageNumber, pagesOf } from '../guide.mjs';
  import { icon, ICON, type Tr } from '../ui';
  import Status from './Status.svelte';

  type Chapter = { id: string; number: number | null; title: string; html: string };
  type Book = { lang: string; title: string; intro: string; chapters: Chapter[] };

  let { tr }: { tr: Tr } = $props();

  let book = $state<Book | null>(null);
  let failed = $state(false);
  let currentId = $state(lastPage);
  let textEl = $state<HTMLElement | null>(null);

  const pages = $derived(pagesOf(book, tr.t('guide.intro')));
  const page = $derived(pageById(pages, currentId));
  const before = $derived(page ? pageAround(pages, page.id, -1) : null);
  const after = $derived(page ? pageAround(pages, page.id, 1) : null);

  onMount(async () => {
    try {
      /* A separate chunk: the book is read from the main menu only, so the game itself does not carry it. */
      const module = await import('../guide.generated.json');
      book = module.default as Book;
    } catch {
      failed = true;
    }
  });

  function open(id: string) {
    currentId = id;
    lastPage = id;
    textEl?.scrollTo({ top: 0 });
    document.getElementById('view')?.scrollTo({ top: 0 });
  }
</script>

<div class="guide">
  {#if tr.lang !== 'pl'}
    <div class="guide-lang"><Status text={tr.t('guide.polishOnly')} tone="warn" /></div>
  {/if}
  {#if failed}
    <p class="bad">{tr.t('guide.failed')}</p>
  {:else if !page}
    <p class="muted">{tr.t('guide.loading')}</p>
  {:else}
    <div class="guide-grid">
      <nav class="panel guide-toc" aria-label={tr.t('guide.contents')}>
        <header><h2>{tr.t('guide.contents')}</h2></header>
        <ol class="body">
          {#each pages as entry (entry.id)}
            <li>
              <button type="button" class:on={entry.id === page.id} aria-current={entry.id === page.id ? 'page' : undefined} onclick={() => open(entry.id)}>
                <span class="num">{pageNumber(entry)}</span>
                <span class="t">{entry.title}</span>
              </button>
            </li>
          {/each}
        </ol>
      </nav>
      <article class="panel guide-page" lang={book?.lang ?? 'pl'}>
        <header>
          {#if pageNumber(page)}<span class="guide-no num" aria-hidden="true">{pageNumber(page)}</span>{/if}
          <h2>{page.title}</h2>
        </header>
        <div class="guide-text" bind:this={textEl}>
          {#key page.id}
            {@html page.html}
          {/key}
        </div>
        <footer class="guide-pager">
          {#if before}
            <button class="btn" type="button" onclick={() => open(before.id)}>
              {@html icon(ICON.back, 17)}<span>{tr.t('guide.prev')}</span><small class="muted">{before.title}</small>
            </button>
          {/if}
          {#if after}
            <button class="btn primary next" type="button" onclick={() => open(after.id)}>
              <small>{after.title}</small><span>{tr.t('guide.next')}</span>{@html icon(ICON.arrow, 17)}
            </button>
          {/if}
        </footer>
      </article>
    </div>
  {/if}
</div>
