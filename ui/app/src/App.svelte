<script lang="ts">
  import { onMount, tick } from 'svelte';
  import { BridgeError, command, connect, HUMAN_MANAGER_ID, query, ready } from './lib/api/client';
  import type {
    CalendarRoundView,
    CalendarView,
    InboxItemView,
    InboxView,
    NextRaceView,
    RaceResultView,
    SaveListItem,
    SessionView,
    ShellView,
    StandingsView,
    TeamOptionView,
    TranslationMessage,
  } from './lib/api/types.generated';
  import Fields from './lib/components/Fields.svelte';
  import RaceBoards from './lib/components/RaceBoards.svelte';
  import StartScreen, { type CareerForm } from './lib/components/StartScreen.svelte';
  import Status from './lib/components/Status.svelte';
  import Tabs from './lib/components/Tabs.svelte';
  import { formatDate } from './lib/date.mjs';
  import { flagSprite } from './lib/flags.mjs';
  import { getLanguage, loadLanguage, setLanguage, subscribeLanguage, translate, type Language } from './lib/i18n';
  import { formatMoney } from './lib/money.mjs';
  import { afterAdvance, blockingLabel, nextAction } from './lib/protocol.mjs';
  import { NAV, screenId, screenKey, SETTINGS } from './lib/shell-nav.mjs';
  import { startSmoke } from './lib/smoke';
  import { sweep } from './lib/sweep';

  const arrow = '<path d="M5 12h14M13 6l6 6-6 6"/>';
  const call = { managerId: HUMAN_MANAGER_ID };

  let lang = $state<Language>('pl');
  let langChoice = $state<Language>('pl');
  let session = $state<SessionView | null>(null);
  let shell = $state<ShellView | null>(null);
  let inbox = $state<InboxView | null>(null);
  let teams = $state<TeamOptionView[]>([]);
  let saves = $state<SaveListItem[]>([]);
  let year = $state(1955);
  let yearSeeded = false;
  let calendar = $state<CalendarView | null>(null);
  let standings = $state<StandingsView | null>(null);
  let nextRace = $state<NextRaceView | null>(null);
  let result = $state<RaceResultView | null>(null);
  let saveDraft = $state('career');
  let savedName = $state<string | null>(null);
  let screen = $state('pulpit');
  let fault = $state<BridgeError | null>(null);
  let busy = $state(false);
  let selected = $state<string | null>(null);
  let optionId = $state<string | null>(null);
  let moving = false;
  let queued: string | null = null;
  let refreshToken = 0;

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

  function catchFault(error: unknown) {
    fault = error instanceof BridgeError ? error : new BridgeError('bridge.error.internal');
  }

  async function refresh() {
    const token = ++refreshToken;
    const nextSession = await query('session', call);
    if (token !== refreshToken) return;
    session = nextSession;
    if (!nextSession.started) {
      shell = null;
      inbox = null;
      calendar = null;
      standings = null;
      nextRace = null;
      result = null;
      if (!yearSeeded) {
        year = nextSession.suggestedYear;
        yearSeeded = true;
      }
      const saveList = await query('saves', call);
      if (token !== refreshToken) return;
      saves = saveList.saves;
      fault = null;
      return;
    }
    const nextShell = await query('shell', call);
    if (token !== refreshToken) return;
    shell = nextShell;
    nextRace = await query('nextRace', call);
    if (token !== refreshToken) return;
    if (screen === 'skrzynka') {
      const nextInbox = await query('inbox', call);
      if (token !== refreshToken) return;
      inbox = nextInbox;
      if (!selected) {
        selected = nextShell.decisionItemId ?? nextInbox.items.find((item) => item.needsDecision)?.id ?? nextInbox.items[0]?.id ?? null;
      }
    }
    if (screen === 'kalendarz') {
      calendar = await query('calendar', call);
      if (token !== refreshToken) return;
    }
    if (screen === 'klasyfikacje') {
      standings = await query('standings', call);
      if (token !== refreshToken) return;
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
      result = null;
      await tick();
    });
    moving = false;
    if (screen === 'skrzynka' || screen === 'kalendarz' || screen === 'klasyfikacje') await refresh();
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
      const resultDay = await command('advanceDay', call);
      afterAdvance({ ok: true, data: resultDay });
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
      catchFault(error);
    } finally {
      busy = false;
    }
  }

  async function beginCareer(form: CareerForm) {
    if (busy) return;
    busy = true;
    try {
      const seed = Number(session?.suggestedSeed);
      const started = await command('newCareer', {
        managerId: HUMAN_MANAGER_ID,
        teamId: form.teamId,
        givenName: form.givenName,
        familyName: form.familyName,
        nationality: form.nationality,
        tilt: form.tilt,
        preset: form.preset,
        year: form.year,
        seed: Number.isFinite(seed) ? seed : null,
        ai: null,
        fatality: null,
        history: null,
        name: null,
        noNumbers: null,
        people: null,
        randomness: null,
        rules: null,
      });
      session = {
        started: true,
        managerId: started.managerId,
        date: started.date,
        organizationId: started.organizationId,
        organizationName: null,
        peopleNoticeKey: started.peopleNoticeKey,
        suggestedYear: session?.suggestedYear ?? form.year,
        suggestedSeed: session?.suggestedSeed ?? '',
      };
      location.hash = '#/pulpit';
      screen = 'pulpit';
      await refresh();
    } catch (error) {
      catchFault(error);
    } finally {
      busy = false;
    }
  }

  async function loadCareer(name: string) {
    if (busy) return;
    busy = true;
    try {
      await command('loadCareer', { managerId: HUMAN_MANAGER_ID, path: name });
      location.hash = '#/pulpit';
      screen = 'pulpit';
      await refresh();
    } catch (error) {
      catchFault(error);
    } finally {
      busy = false;
    }
  }

  async function openRound(round: CalendarRoundView) {
    if (!round.finished) {
      result = null;
      return;
    }
    try {
      result = await query('raceResult', { managerId: HUMAN_MANAGER_ID, season: round.season, round: round.round });
      fault = null;
    } catch (error) {
      catchFault(error);
    }
  }

  async function saveCareer() {
    const name = saveDraft.trim();
    if (!name || busy) return;
    busy = true;
    try {
      const saved = await command('saveCareer', { managerId: HUMAN_MANAGER_ID, name });
      savedName = saved.name;
      fault = null;
    } catch (error) {
      catchFault(error);
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
    const stopBridge = connect((type) => {
      if (type === 'dayAdvanced' || type === 'inboxChanged' || type === 'seasonChanged' || type === 'raceFinished') {
        void refresh().catch(catchFault);
      }
    });
    const onHash = () => {
      void transition(screenId(location.hash));
    };
    window.addEventListener('hashchange', onHash);
    void ready()
      .then(() => refresh())
      .catch(catchFault);
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

  $effect(() => {
    const id = shell?.organizationId;
    if (id === 'ferrari' || id === 'lotus' || id === 'tyrrell') document.body.dataset.team = id;
  });

  $effect(() => {
    if (!session || session.started) return;
    const wanted = Number(year);
    if (!Number.isFinite(wanted) || wanted < 1950) return;
    let cancel = false;
    void query('teams', { managerId: HUMAN_MANAGER_ID, year: wanted })
      .then((list) => {
        if (!cancel) teams = list.teams;
      })
      .catch(catchFault);
    return () => {
      cancel = true;
    };
  });

  let team = $derived(crest(shell?.organizationName));
  let decision = $derived(blockingLabel(shell));
  let blocking = $derived(
    decision
      ? t('shell.go.decision', { area: t(decision.area) })
      : shell?.blockingKind
        ? t('ready.blockingItem')
        : '',
  );
  let currentItem = $derived(inbox?.items.find((item) => item.id === selected) ?? null);
  let started = $derived(session?.started === true);
</script>

{@html flagSprite()}
<canvas id="air" aria-hidden="true" bind:this={air}></canvas>
<div class="app">
  <aside>
    <div class="crest">
      <div class="team">{started ? team.lead : 'Paddock'}{#if started && team.rest}<span>{team.rest}</span>{:else if !started}<span>Principal</span>{/if}</div>
    </div>
    {#if started}
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
    {/if}
    <div class="stripes"></div>
  </aside>
  <div class="content" bind:this={contentEl}>
    {#if started}
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
          {#if nextRace?.circuitName}
            <a class="cell" href="#/kalendarz">
              <span class="meta">{t('shell.nextRace')}</span>
              <span class="v">{nextRace.circuitName}</span>
            </a>
          {/if}
          <a class="cell date" href="#/kalendarz"><b>{shell ? formatDate(shell.date, lang) : '—'}</b></a>
        </div>
        <button class="go" type="button" aria-disabled={!shell || busy} title={decision ? tMsg(decision.subject) : undefined} onclick={nextDay}>
          <span>
            <b>{t('shell.next')}</b>
            {#if blocking}<small><i class="blk"></i>{blocking}</small>{/if}
          </span>
          <span class="arr">{@html icon(arrow)}</span>
        </button>
      </header>
    {/if}
    <main id="view" bind:this={viewEl}>
      {#if !started}
        <div class="screen-head">
          <h1 class="screen">{t('shell.start.title')}</h1>
          <Tabs
            group="lang"
            items={[
              { value: 'pl', label: t('shell.lang.pl') },
              { value: 'en', label: t('shell.lang.en') },
            ]}
            bind:value={langChoice}
          />
        </div>
        {#if fault}
          <p class="bad">{t(fault.key, fault.parameters)}</p>
        {/if}
        {#if session}
          <StartScreen bind:year {teams} {saves} {busy} {lang} {t} onStart={beginCareer} onLoad={loadCareer} />
        {/if}
      {:else}
        <div class="screen-head">
          <h1 class="screen">{t(screenKey(screen) || 'shell.nav.home')}</h1>
          {#if screen === 'skrzynka' && inbox && inbox.openCount > 0}
            <Status text={tCount('shell.inbox.open', inbox.openCount)} tone={inbox.openDecisionCount > 0 ? 'warn' : 'team'} />
          {/if}
          {#if screen === 'pulpit' && session?.peopleNoticeKey}
            <Status text={t(session.peopleNoticeKey)} tone="warn" />
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
        {:else if screen === 'kalendarz'}
          <RaceBoards kind="calendar" rounds={calendar?.rounds ?? []} {result} {lang} onRound={openRound} />
          {#if result?.found}
            <h2>{t('shell.race.result')}</h2>
            <RaceBoards kind="result" {result} ownTeam={shell?.organizationId ?? ''} {lang} />
          {/if}
        {:else if screen === 'klasyfikacje'}
          <RaceBoards kind="standings" {standings} ownTeam={shell?.organizationId ?? ''} {lang} />
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
            <label class="fld">
              <span class="meta">{t('shell.save.name')}</span>
              <input class="text" type="text" bind:value={saveDraft} autocomplete="off" />
            </label>
          </div>
          <div class="confirm">
            <button class="btn primary" type="button" disabled={!saveDraft.trim() || busy} onclick={saveCareer}>{t('shell.save')}</button>
          </div>
          {#if savedName}
            <Fields items={[{ label: t('shell.save.name'), value: savedName }]} />
          {/if}
        {/if}
      {/if}
    </main>
    <div id="wipe" aria-hidden="true" bind:this={wipeEl}><i></i><i></i><i></i></div>
  </div>
</div>
