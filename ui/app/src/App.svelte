<script lang="ts">
  import { onMount, tick } from 'svelte';
  import { BridgeError, command, connect, HUMAN_MANAGER_ID, query, ready } from './lib/api/client';
  import type { InboxItemView, InboxView, ShellView, TranslationMessage } from './lib/api/types.generated';
  import Fields from './lib/components/Fields.svelte';
  import Status from './lib/components/Status.svelte';
  import Tabs from './lib/components/Tabs.svelte';
  import { formatDate } from './lib/date.mjs';
  import { flagSprite } from './lib/flags.mjs';
  import { getLanguage, loadLanguage, setLanguage, subscribeLanguage, translate, type Language } from './lib/i18n';
  import { formatMoney } from './lib/money.mjs';
  import { afterAdvance, nextAction } from './lib/protocol.mjs';
  import { NAV, screenId, screenKey, SETTINGS } from './lib/shell-nav.mjs';
  import { startSmoke } from './lib/smoke';
  import { sweep } from './lib/sweep';

  const arrow = '<path d="M5 12h14M13 6l6 6-6 6"/>';

  let lang = $state<Language>('pl');
  let langChoice = $state<Language>('pl');
  let shell = $state<ShellView | null>(null);
  let inbox = $state<InboxView | null>(null);
  let screen = $state('pulpit');
  let fault = $state<BridgeError | null>(null);
  let busy = $state(false);
  let selected = $state<string | null>(null);
  let optionId = $state<string | null>(null);
  let moving = false;
  let queued: string | null = null;
  let refreshToken = 0;
  let gate = $state(true);
  let teams = $state<{ id: string; name: string }[]>([]);
  let saves = $state<{ name: string; date: string; teamId: string }[]>([]);
  let preset = $state('Balanced');
  let year = $state(1955);
  let fatality = $state('Off');
  let teamId = $state('');
  let givenName = $state('');
  let familyName = $state('');
  let nationality = $state('');
  let tilt = $state('none');

  let air: HTMLCanvasElement | undefined = $state();
  let contentEl: HTMLElement | undefined = $state();
  let viewEl: HTMLElement | undefined = $state();
  let wipeEl: HTMLElement | undefined = $state();
  let marker: HTMLElement | undefined = $state();

  function t(key: string, parameters: Record<string, string> = {}) {
    lang;
    return translate(lang, key, parameters);
  }

  function tCount(key: string, count: number) {
    lang;
    return translate(lang, key, { count: String(count) }, count);
  }

  function tMsg(message: TranslationMessage | null | undefined) {
    lang;
    if (!message) return '';
    return translate(lang, message.key, message.parameters ?? {});
  }

  function icon(path: string) {
    return `<svg class="i" viewBox="0 0 24 24" aria-hidden="true">${path}</svg>`;
  }

  function crest(name: string | null | undefined) {
    const text = name?.trim() || '—';
    const parts = text.split(/\s+/);
    if (parts.length < 2 || text === '—') return { lead: text, rest: '' };
    return { lead: parts[0] ?? text, rest: parts.slice(1).join(' ') };
  }

  function initials(name: string) {
    const words = name.split(/\s+/).filter(Boolean);
    if (words.length >= 2) return `${words[0]?.[0] ?? ''}${words[1]?.[0] ?? ''}`.toUpperCase();
    return name.slice(0, 2).toUpperCase();
  }

  function placeNav() {
    if (!marker) return;
    const current = document.querySelector('#nav a.on') as HTMLElement | null;
    if (!current) {
      marker.style.opacity = '0';
      return;
    }
    marker.style.opacity = '1';
    marker.style.height = `${current.offsetHeight}px`;
    marker.style.transform = `translateY(${current.offsetTop}px)`;
  }

  async function boot() {
    const session = await query('session', { managerId: HUMAN_MANAGER_ID });
    if (session.open) {
      gate = false;
      await refresh();
      return;
    }
    gate = true;
    year = session.suggestedYear;
    const listed = await query('teams', { managerId: HUMAN_MANAGER_ID, year: session.suggestedYear });
    teams = listed.teams.map((item) => ({ id: item.id, name: item.name }));
    teamId = teams.find((item) => item.id === 'ferrari')?.id ?? teams[0]?.id ?? '';
    const stored = await query('saves', { managerId: HUMAN_MANAGER_ID });
    saves = stored.saves.map((item) => ({ name: item.name, date: item.date, teamId: item.teamId }));
    fault = null;
  }

  async function takeOver() {
    if (busy || !teamId || !givenName.trim() || !familyName.trim() || !nationality.trim()) return;
    busy = true;
    try {
      await command('startCareer', {
        managerId: HUMAN_MANAGER_ID,
        preset,
        year,
        fatality,
        teamId,
        givenName: givenName.trim(),
        familyName: familyName.trim(),
        nationality: nationality.trim(),
        tilt,
        seed: '1',
        careerName: 'career',
      });
      gate = false;
      await refresh();
    } catch (error) {
      fault = error instanceof BridgeError ? error : new BridgeError('bridge.error.internal');
    } finally {
      busy = false;
    }
  }

  async function loadSave(name: string) {
    if (busy) return;
    busy = true;
    try {
      await command('loadCareer', { managerId: HUMAN_MANAGER_ID, name });
      gate = false;
      await refresh();
    } catch (error) {
      fault = error instanceof BridgeError ? error : new BridgeError('bridge.error.internal');
    } finally {
      busy = false;
    }
  }

  async function refresh() {
    const token = ++refreshToken;
    const nextShell = await query('shell', { managerId: HUMAN_MANAGER_ID });
    if (token !== refreshToken) return;
    shell = nextShell;
    if (screen === 'skrzynka') {
      const nextInbox = await query('inbox', { managerId: HUMAN_MANAGER_ID });
      if (token !== refreshToken) return;
      inbox = nextInbox;
      if (!selected) {
        selected = nextShell.decisionItemId ?? nextInbox.items.find((item) => item.needsDecision)?.id ?? nextInbox.items[0]?.id ?? null;
      }
    }
    fault = null;
  }

  async function transition(id: string) {
    if (id === screen) return;
    if (moving) {
      queued = id;
      return;
    }
    if (!contentEl || !viewEl || !wipeEl) {
      screen = id;
      return;
    }
    moving = true;
    await sweep(contentEl, viewEl, wipeEl, async () => {
      screen = id;
      selected = null;
      optionId = null;
      await tick();
    });
    moving = false;
    if (screen === 'skrzynka') await refresh();
    if (queued && queued !== screen) {
      const next = queued;
      queued = null;
      await transition(next);
    } else {
      queued = null;
    }
  }

  async function nextDay() {
    if (!shell || busy || moving) return;
    const action = nextAction(shell);
    if (action.type === 'show') {
      location.hash = '#/skrzynka';
      return;
    }
    busy = true;
    try {
      const result = await command('advanceDay', { managerId: HUMAN_MANAGER_ID });
      afterAdvance({ ok: true, data: result });
      await refresh();
      document.querySelector('.hud .date')?.animate(
        [{ background: 'color-mix(in oklab, var(--t2) 40%, transparent)' }, { background: 'transparent' }],
        { duration: 900, easing: 'ease-out' },
      );
    } catch (error) {
      const bridge = error instanceof BridgeError ? error : new BridgeError('bridge.error.internal');
      const outcome = afterAdvance({ ok: false, error: { key: bridge.key, parameters: bridge.parameters } });
      await refresh().catch(() => {});
      if (outcome.type === 'show') location.hash = '#/skrzynka';
      else fault = bridge;
    } finally {
      busy = false;
    }
  }

  async function confirmChoice() {
    if (!selected || !optionId || busy) return;
    busy = true;
    try {
      await command('resolveInbox', { managerId: HUMAN_MANAGER_ID, itemId: selected, optionId });
      optionId = null;
      selected = null;
      await refresh();
    } catch (error) {
      fault = error instanceof BridgeError ? error : new BridgeError('bridge.error.internal');
    } finally {
      busy = false;
    }
  }

  function chooseItem(item: InboxItemView) {
    selected = item.id;
    optionId = null;
  }

  onMount(() => {
    loadLanguage();
    lang = getLanguage();
    langChoice = lang;
    screen = screenId(location.hash);
    const stopLang = subscribeLanguage(() => {
      lang = getLanguage();
    });
    const stopSmoke = air ? startSmoke(air) : () => {};
    const stopBridge = connect(() => {
      void boot().catch((error: unknown) => {
        fault = error instanceof BridgeError ? error : new BridgeError('bridge.error.internal');
      });
    });
    const onHash = () => {
      void transition(screenId(location.hash));
    };
    window.addEventListener('hashchange', onHash);
    void ready()
      .then(() => boot())
      .catch((error: unknown) => {
        fault = error instanceof BridgeError ? error : new BridgeError('bridge.error.internal');
      });
    return () => {
      stopLang();
      stopSmoke();
      stopBridge();
      window.removeEventListener('hashchange', onHash);
    };
  });

  $effect(() => {
    document.documentElement.lang = lang;
  });

  $effect(() => {
    if (langChoice !== getLanguage()) setLanguage(langChoice);
  });

  $effect(() => {
    screen;
    shell?.inboxOpen;
    queueMicrotask(placeNav);
  });

  let team = $derived(crest(shell?.organizationName));
  let blocking = $derived(
    shell?.decisionSubjectKey
      ? t(shell.decisionSubjectKey)
      : shell?.blockingKind
        ? t('ready.blockingItem')
        : '',
  );
  let currentItem = $derived(inbox?.items.find((item) => item.id === selected) ?? null);
</script>

{@html flagSprite()}
<canvas id="air" aria-hidden="true" bind:this={air}></canvas>
<div class="app">
  <aside>
    <div class="crest">
      <div class="team">{team.lead}{#if team.rest}<span>{team.rest}</span>{/if}</div>
    </div>
    <nav id="nav">
      <span class="nav-ind" aria-hidden="true" bind:this={marker}></span>
      {#each NAV as item, index (item.id ?? `sep-${index}`)}
        {#if item.sep}
          <div class="sep"></div>
        {:else}
          <a href={`#/${item.id}`} data-r={item.id} class:on={screen === item.id}>
            {@html icon(item.icon ?? '')}
            <span>{t(item.key ?? '')}</span>
            {#if item.id === 'skrzynka' && (shell?.inboxOpen ?? 0) > 0}
              <span class="n">{shell?.inboxOpen}</span>
            {/if}
          </a>
        {/if}
      {/each}
    </nav>
    <div class="foot">
      <a href={`#/${SETTINGS.id}`} data-r={SETTINGS.id} class:on={screen === SETTINGS.id}>
        {@html icon(SETTINGS.icon)}
        {t(SETTINGS.key)}
      </a>
    </div>
    <div class="stripes"></div>
  </aside>
  <div class="content" bind:this={contentEl}>
    <header class="top">
      <div class="hud">
        <a class="cell me" href="#/zarzad">
          <span class="av">{initials(shell?.organizationName ?? '')}</span>
          <span><b>{shell?.organizationName ?? '—'}</b><small>{t('shell.role')}</small></span>
        </a>
        <a class="cell" href="#/finanse">
          <span class="meta">{t('shell.cash')}</span>
          <span class="num v">{formatMoney(shell?.cashCents ?? null, lang)}</span>
        </a>
      </div>
      <div class="spacer"></div>
      <div class="hud">
        <a class="cell date" href="#/kalendarz"><b>{shell ? formatDate(shell.date, lang) : '—'}</b></a>
      </div>
      <button class="go" type="button" aria-disabled={!shell || busy} onclick={nextDay}>
        <span>
          <b>{t('shell.next')}</b>
          {#if blocking}<small><i class="blk"></i>{blocking}</small>{/if}
        </span>
        <span class="arr">{@html icon(arrow)}</span>
      </button>
    </header>
    <main id="view" bind:this={viewEl}>
      {#if gate}
        <div class="screen-head"><h1 class="screen">{t('shell.gate.title')}</h1></div>
        {#if fault}<p class="bad">{t(fault.key, fault.parameters)}</p>{/if}
        <div class="panel">
          <div class="body gate">
            <label><span class="meta">{t('shell.gate.preset')}</span>
              <select bind:value={preset}>
                <option value="MostHistorical">{t('shell.gate.preset.mostHistorical')}</option>
                <option value="Balanced">{t('shell.gate.preset.balanced')}</option>
                <option value="Chaos">{t('shell.gate.preset.chaos')}</option>
              </select>
            </label>
            <label><span class="meta">{t('shell.gate.year')}</span><input type="number" bind:value={year} /></label>
            <label><span class="meta">{t('shell.gate.fatality')}</span>
              <select bind:value={fatality}><option value="Off">{t('shell.gate.fatality.off')}</option><option value="On">{t('shell.gate.fatality.on')}</option></select>
            </label>
            <label><span class="meta">{t('shell.gate.team')}</span>
              <select bind:value={teamId}>
                {#each teams as item (item.id)}<option value={item.id}>{item.name}</option>{/each}
              </select>
            </label>
            <label><span class="meta">{t('shell.gate.given')}</span><input bind:value={givenName} /></label>
            <label><span class="meta">{t('shell.gate.family')}</span><input bind:value={familyName} /></label>
            <label><span class="meta">{t('shell.gate.nationality')}</span><input bind:value={nationality} /></label>
            <label><span class="meta">{t('shell.gate.tilt')}</span><input bind:value={tilt} /></label>
            <div class="span"><button class="btn primary" type="button" disabled={busy} onclick={takeOver}>{t('shell.gate.start')}</button></div>
          </div>
        </div>
        <div class="panel">
          <header><b>{t('shell.gate.loadTitle')}</b></header>
          <div class="body">
            {#if saves.length === 0}<p>{t('shell.gate.empty')}</p>{:else}
              {#each saves as save (save.name)}
                <button class="btn" type="button" disabled={busy} onclick={() => loadSave(save.name)}>{save.name}</button>
              {/each}
            {/if}
          </div>
        </div>
      {:else}
      <div class="screen-head">
        <h1 class="screen">{t(screenKey(screen) || 'shell.nav.home')}</h1>
        {#if screen === 'skrzynka' && inbox && inbox.openCount > 0}
          <Status text={tCount('shell.inbox.open', inbox.openCount)} tone={inbox.openDecisionCount > 0 ? 'warn' : 'team'} />
        {/if}
      </div>
      {#if fault}
        <p class="bad">{t(fault.key, fault.parameters)}</p>
      {/if}
      {#if screen === 'skrzynka' && inbox}
        <div class="panel">
          {#each inbox.items as item (item.id)}
            <button type="button" class="mail" class:decision={item.needsDecision} class:sel={item.id === selected} onclick={() => chooseItem(item)}>
              <span class="av">{initials(tMsg(item.subject))}</span>
              <span class="t">{tMsg(item.subject)}</span>
              <span class="when">{item.validUntil ? formatDate(item.validUntil, lang) : ''}</span>
            </button>
          {/each}
          {#if currentItem && currentItem.options.length > 0}
            <div class="body">
              <Fields items={[{ label: t('shell.nav.inbox'), value: tMsg(currentItem.subject) }]} />
              <div class="choices" role="radiogroup">
                {#each currentItem.options as option (option.id)}
                  <button type="button" class="choice" role="radio" aria-checked={optionId === option.id} onclick={() => (optionId = option.id)}>
                    <span class="ch"><b>{tMsg(option.label)}</b><span class="rd"></span></span>
                    {#if option.consequence.key}
                      <span class="fx"><span class="row o"><b>·</b>{tMsg(option.consequence)}</span></span>
                    {/if}
                  </button>
                {/each}
              </div>
              <div class="confirm">
                <button class="btn primary" type="button" disabled={!optionId || busy} onclick={confirmChoice}>{t('shell.confirm')}</button>
              </div>
            </div>
          {/if}
        </div>
      {:else if screen === 'ustawienia'}
        <div class="fields">
          <div class="fld">
            <span class="meta">{t('shell.language')}</span>
            <Tabs
              group="lang"
              items={[
                { value: 'pl', label: t('shell.lang.pl') },
                { value: 'en', label: t('shell.lang.en') },
              ]}
              bind:value={langChoice}
            />
          </div>
        </div>
      {/if}
      {/if}
    </main>
    <div id="wipe" aria-hidden="true" bind:this={wipeEl}><i></i><i></i><i></i></div>
  </div>
</div>
