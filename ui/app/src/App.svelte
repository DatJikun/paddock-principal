<script lang="ts">
  import { onMount, tick } from 'svelte';
  import { BridgeError, canExit, command, connect, exitApp, HUMAN_MANAGER_ID, query, ready } from './lib/api/client';
  import type { BridgeCommandName, LiveClockView, NewCareerCall, NextRaceView, QuickRaceCall, QuickRaceStartedView, SaveListItem, SessionView, ShellView } from './lib/api/types.generated';
  import { latestSave, newestFirst, saveLabel } from './lib/career.mjs';
  import GameMenu from './lib/components/GameMenu.svelte';
  import SettingsPanel from './lib/components/SettingsPanel.svelte';
  import Toasts, { type Toast } from './lib/components/Toasts.svelte';
  import LoadList from './lib/components/LoadList.svelte';
  import MenuHome from './lib/components/MenuHome.svelte';
  import NewCareer from './lib/components/NewCareer.svelte';
  import QuickRace from './lib/components/QuickRace.svelte';
  import RaceLive from './lib/components/RaceLive.svelte';
  import Status from './lib/components/Status.svelte';
  import { isNewerItem, mayStart, stopReason } from './lib/autoplay.mjs';
  import { addDays, daysBetween, formatDate, weekdayIndex } from './lib/date.mjs';
  import { flagSprite } from './lib/flags.mjs';
  import { getLanguage, loadLanguage, setLanguage, subscribeLanguage, translate, type Language } from './lib/i18n';
  import { formatMoney } from './lib/money.mjs';
  import { afterAdvance, blockingLabel, inboxArea, nextAction } from './lib/protocol.mjs';
  import { loadSettings, saveSettings } from './lib/settings.mjs';
  import { livery } from './lib/livery.mjs';
  import { loadScreen, type ScreenData } from './lib/screens';
  import { NAV, navOwner, parseRoute, sameRoute, screenKey, SETTINGS } from './lib/shell-nav.mjs';
  import { startSmoke } from './lib/smoke';
  import { sweep } from './lib/sweep';
  import { icon, ICON, initials, translator } from './lib/ui';
  import Akademia from './screens/Akademia.svelte';
  import Auto from './screens/Auto.svelte';
  import Dostawcy from './screens/Dostawcy.svelte';
  import Finanse from './screens/Finanse.svelte';
  import Infrastruktura from './screens/Infrastruktura.svelte';
  import Kalendarz from './screens/Kalendarz.svelte';
  import Kierowca from './screens/Kierowca.svelte';
  import Kierowcy from './screens/Kierowcy.svelte';
  import Klasyfikacje from './screens/Klasyfikacje.svelte';
  import Menedzer from './screens/Menedzer.svelte';
  import Negocjacja from './screens/Negocjacja.svelte';
  import Osoba from './screens/Osoba.svelte';
  import Personel from './screens/Personel.svelte';
  import Porownaj from './screens/Porownaj.svelte';
  import Pulpit from './screens/Pulpit.svelte';
  import Rynek from './screens/Rynek.svelte';
  import Skrzynka from './screens/Skrzynka.svelte';
  import Sponsorzy from './screens/Sponsorzy.svelte';
  import Wyscig from './screens/Wyscig.svelte';
  import Zarzad from './screens/Zarzad.svelte';

  type Route = { name: string; args: string[] };

  const call = { managerId: HUMAN_MANAGER_ID };
  /* The top bar shows one square per day only when the race is close enough for the squares to fit. */
  const TICK_DAYS = 21;

  let lang = $state<Language>('pl');
  let langChoice = $state<Language>('pl');
  let session = $state<SessionView | null>(null);
  let shell = $state<ShellView | null>(null);
  let nextRace = $state<NextRaceView | null>(null);
  let saves = $state<SaveListItem[]>([]);
  /* 'menu' is the main menu and its pages; 'game' is the career. The bridge keeps a career in memory in both. */
  let phase = $state<'menu' | 'game'>('menu');
  let menuPage = $state<'home' | 'new' | 'quick' | 'load' | 'settings'>('home');
  let gameMenu = $state(false);
  /* The file the career was last saved to or loaded from, and the date it held then. Saving is manual only. */
  let savedName = $state<string | null>(null);
  let savedDate = $state<string | null>(null);
  let loadPick = $state('');
  let toasts = $state<Toast[]>([]);
  let toastKey = 0;
  /* Dalej runs the days by itself while `running`; a pause or any stop reason ends the run. */
  let settings = $state(loadSettings());
  let running = $state(false);
  /* Nothing is drawn until the first read says whether a career is already open, so the menu does not flash over a running game. */
  let booted = $state(false);
  let runToken = 0;
  let racedNow = false;
  let seasonNow = false;
  /* The newest important inbox item the player has already been told about. */
  let toldAbout: string | null | undefined;
  let route = $state<Route>({ name: 'pulpit', args: [] });
  let screenData = $state<ScreenData>({ kind: 'none' });
  let fault = $state<BridgeError | null>(null);
  let busy = $state(false);
  let moving = false;
  let queued = false;
  let refreshToken = 0;
  /* Race mode (PP-052): a separate full-screen root. While it is open the shell node is taken out of the document, not hidden. */
  let racing = $state(false);
  let raceClock = $state<LiveClockView | null>(null);
  let liveOpen = $state(false);
  /* A quick race (#280) is watched from the main menu. The bridge holds it next to the career, which stays as it was. */
  let quick = $state<QuickRaceStartedView | null>(null);
  let appEl: HTMLElement | undefined = $state();
  let appSlot: { parent: Node; next: Node | null } | null = null;

  let air: HTMLCanvasElement | undefined = $state();
  let contentEl: HTMLElement | undefined = $state();
  let viewEl: HTMLElement | undefined = $state();
  let wipeEl: HTMLElement | undefined = $state();
  let marker: HTMLElement | undefined = $state();

  let tr = $derived(translator(lang));

  function t(key: string, parameters: Record<string, string> = {}) {
    lang;
    return translate(lang, key, parameters);
  }

  function crest(name: string | null | undefined) {
    const text = name?.trim() || '—';
    const parts = text.split(/\s+/);
    if (parts.length < 2 || text === '—') return { lead: text, rest: '' };
    return { lead: parts[0] ?? text, rest: parts.slice(1).join(' ') };
  }

  function placeNav() {
    if (!marker) return;
    const current = document.querySelector('#nav .on') as HTMLElement | null;
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
      nextRace = null;
      screenData = { kind: 'none' };
      const saveList = await query('saves', call);
      if (token !== refreshToken) return;
      saves = saveList.saves;
      fault = null;
      return;
    }
    const asked = route;
    const [nextShell, race, data] = await Promise.all([
      query('shell', call),
      query('nextRace', call),
      loadScreen(asked.name, asked.args),
    ]);
    if (token !== refreshToken) return;
    shell = nextShell;
    nextRace = race;
    const live = await query('liveClock', call).catch(() => null);
    if (token !== refreshToken) return;
    liveOpen = live !== null && live.active && !live.finished;
    /* A read that was started for a screen the player has since left must not replace the screen that is showing now. */
    if (sameRoute(asked, route)) screenData = data;
    fault = null;
  }

  async function transition(next: Route) {
    if (sameRoute(next, route)) return;
    if (moving) {
      queued = true;
      return;
    }
    /* Skrzynka to skrzynka: switching mail is instant, the data is already on screen. */
    if (next.name === 'skrzynka' && route.name === 'skrzynka') {
      route = next;
      return;
    }
    moving = true;
    try {
      const data = session?.started ? await loadScreen(next.name, next.args) : screenData;
      if (!contentEl || !viewEl || !wipeEl) {
        route = next;
        screenData = data;
      } else {
        await sweep(contentEl, viewEl, wipeEl, async () => {
          route = next;
          screenData = data;
          await tick();
          if (viewEl) viewEl.scrollTop = 0;
        });
      }
      fault = null;
    } catch (error) {
      catchFault(error);
    } finally {
      moving = false;
    }
    if (queued) {
      queued = false;
      await transition(parseRoute(location.hash));
    }
  }

  /** Any screen change that is not a route (menu pages, entering the game) gets the same colour sweep. */
  async function swap(change: () => void | Promise<void>) {
    if (moving) return;
    moving = true;
    try {
      if (!contentEl || !viewEl || !wipeEl) {
        await change();
      } else {
        await sweep(contentEl, viewEl, wipeEl, async () => {
          await change();
          await tick();
          if (viewEl) viewEl.scrollTop = 0;
        });
      }
    } catch (error) {
      catchFault(error);
    } finally {
      moving = false;
    }
  }

  async function refreshSaves() {
    saves = (await query('saves', call)).saves;
  }

  function pushToast(item: Omit<Toast, 'key'>, seconds = 3) {
    const key = ++toastKey;
    toasts = [...toasts.slice(-3), { ...item, key }];
    setTimeout(() => closeToast(key), seconds * 1000);
  }

  function closeToast(key: number) {
    toasts = toasts.filter((item) => item.key !== key);
  }

  function say(text: string) {
    pushToast({ title: '', text, href: null, action: '' });
  }

  /** The newest important inbox item is new to the player: say so, wherever the day was advanced from. */
  function tellAboutImportant() {
    if (!shell) return;
    const id = shell.importantItemId ?? null;
    /* The first read of a career (or of a loaded one) only sets the baseline: what was already there is not news. */
    if (toldAbout === undefined) {
      toldAbout = id;
      return;
    }
    if (!id || !shell.importantSubject || !isNewerItem(id, toldAbout)) return;
    toldAbout = id;
    pushToast(
      {
        title: `${t('toast.inbox')} · ${t(inboxArea(shell.importantKind))}`,
        text: tr.tMsg(shell.importantSubject),
        href: `#/skrzynka/${encodeURIComponent(id)}`,
        action: t('toast.open'),
      },
      9,
    );
  }

  async function openMenuPage(page: 'home' | 'new' | 'quick' | 'load' | 'settings') {
    if (page === menuPage || moving) return;
    fault = null;
    if (page === 'load') await refreshSaves().catch(catchFault);
    loadPick = '';
    await swap(() => {
      menuPage = page;
    });
  }

  async function openGameMenu() {
    if (gameMenu || phase !== 'game') return;
    fault = null;
    await refreshSaves().catch(catchFault);
    gameMenu = true;
  }

  /** One day through the same command Dalej always used. Returns false when the clock did not move. */
  async function stepDay(): Promise<boolean> {
    busy = true;
    try {
      const resultDay = await command('advanceDay', call);
      afterAdvance({ ok: true, data: resultDay });
      await refresh();
      document.querySelector('.hud .date')?.animate(
        [{ background: 'color-mix(in oklab, var(--t2) 40%, transparent)' }, { background: 'transparent' }],
        { duration: 900, easing: 'ease-out' },
      );
      return true;
    } catch (error) {
      const bridge = error instanceof BridgeError ? error : new BridgeError('bridge.error.internal');
      const outcome = afterAdvance({ ok: false, error: { key: bridge.key, parameters: bridge.parameters } });
      await refresh().catch(() => {});
      if (outcome.type === 'show') location.hash = '#/skrzynka';
      else fault = bridge;
      return false;
    } finally {
      busy = false;
    }
  }

  function stopRun() {
    runToken++;
    running = false;
  }

  /** Days one after another until something needs the player: a held clock, a race day, a new important message. */
  async function runDays() {
    const mine = ++runToken;
    running = true;
    let seen = shell?.importantItemId ?? null;
    try {
      while (mine === runToken) {
        const began = performance.now();
        racedNow = false;
        seasonNow = false;
        if (!(await stepDay())) break;
        if (mine !== runToken) break;
        const reason = stopReason({ shell, nextRace, seenImportantId: seen, raced: racedNow, seasonChanged: seasonNow });
        seen = shell?.importantItemId ?? seen;
        if (reason) break;
        const rest = settings.daySeconds * 1000 - (performance.now() - began);
        if (rest > 0) await new Promise((resolve) => setTimeout(resolve, rest));
      }
    } finally {
      if (mine === runToken) running = false;
    }
  }

  async function nextDay() {
    if (running) {
      stopRun();
      return;
    }
    if (!shell || busy || moving) return;
    const action = nextAction(shell);
    if (action.type === 'show') {
      location.hash = `#/skrzynka/${encodeURIComponent(action.itemId)}`;
      return;
    }
    if (settings.autoAdvance && mayStart({ shell, nextRace })) await runDays();
    else await stepDay();
  }

  async function confirmChoice(itemId: string, optionId: string) {
    if (busy) return;
    busy = true;
    try {
      await command('resolveInbox', { managerId: HUMAN_MANAGER_ID, itemId, optionId });
      await refresh();
    } catch (error) {
      catchFault(error);
    } finally {
      busy = false;
    }
  }

  /** A player command from a screen. The screen shows the refusal (the page's fault line) and asks for its own confirm first. */
  async function act(name: BridgeCommandName, args: Record<string, unknown>): Promise<boolean> {
    if (busy) return false;
    busy = true;
    fault = null;
    try {
      await command(name, { managerId: HUMAN_MANAGER_ID, ...args } as never);
      await refresh();
      return true;
    } catch (error) {
      catchFault(error);
      return false;
    } finally {
      busy = false;
    }
  }

  async function dismissMany(itemIds: string[]) {
    if (busy) return;
    busy = true;
    try {
      for (const itemId of itemIds) await command('dismissInbox', { managerId: HUMAN_MANAGER_ID, itemId });
      await refresh();
    } catch (error) {
      catchFault(error);
      await refresh().catch(() => {});
    } finally {
      busy = false;
    }
  }

  async function dismissItem(itemId: string) {
    if (busy) return;
    busy = true;
    try {
      await command('dismissInbox', { managerId: HUMAN_MANAGER_ID, itemId });
      await refresh();
    } catch (error) {
      catchFault(error);
    } finally {
      busy = false;
    }
  }

  /** A career is open on the bridge: read it, then show the game under the sweep. */
  async function enterGame() {
    route = { name: 'pulpit', args: [] };
    location.hash = '#/pulpit';
    toldAbout = undefined;
    await swap(async () => {
      await refresh();
      savedDate = shell?.date ?? null;
      gameMenu = false;
      phase = 'game';
    });
  }

  async function beginCareer(call: NewCareerCall) {
    if (busy) return;
    busy = true;
    fault = null;
    try {
      await command('newCareer', call);
      savedName = null;
      await enterGame();
    } catch (error) {
      catchFault(error);
    } finally {
      busy = false;
    }
  }

  async function loadCareer(name: string) {
    if (busy) return;
    busy = true;
    fault = null;
    try {
      await command('loadCareer', { managerId: HUMAN_MANAGER_ID, path: name });
      savedName = name;
      await enterGame();
    } catch (error) {
      catchFault(error);
    } finally {
      busy = false;
    }
  }

  async function continueCareer() {
    const last = latestSave(saves);
    if (last) await loadCareer(last.name);
  }

  async function saveCareer(name: string) {
    const wanted = name.trim();
    if (!wanted || busy) return;
    busy = true;
    fault = null;
    try {
      const saved = await command('saveCareer', { managerId: HUMAN_MANAGER_ID, name: wanted });
      savedName = saved.name;
      savedDate = shell?.date ?? null;
      gameMenu = false;
      say(t('save.saved', { name: saveLabel(saved.name) }));
    } catch (error) {
      catchFault(error);
    } finally {
      busy = false;
    }
  }

  async function leaveToMenu() {
    if (busy) return;
    stopRun();
    gameMenu = false;
    fault = null;
    await refreshSaves().catch(catchFault);
    await swap(() => {
      phase = 'menu';
      menuPage = 'home';
    });
  }

  function enterRace() {
    if (phase !== 'game' || racing) return;
    gameMenu = false;
    racing = true;
  }

  function leaveRace() {
    racing = false;
    void refresh().catch(catchFault);
  }

  async function startQuickRace(call: QuickRaceCall) {
    if (busy || racing) return;
    busy = true;
    fault = null;
    try {
      quick = await command('startQuickRace', call);
      raceClock = null;
      racing = true;
    } catch (error) {
      catchFault(error);
    } finally {
      busy = false;
    }
  }

  /** The flag fell on a quick race: close it on the bridge and come back to the form, ready for another. */
  async function leaveQuickRace() {
    racing = false;
    quick = null;
    raceClock = null;
    try {
      await command('closeQuickRace', call);
    } catch (error) {
      catchFault(error);
    }
  }

  function onKey(event: KeyboardEvent) {
    if (event.key !== 'Escape' || event.defaultPrevented || gameMenu || racing) return;
    if (running) {
      event.preventDefault();
      stopRun();
    } else if (phase === 'game') {
      event.preventDefault();
      void openGameMenu();
    } else if (menuPage !== 'home') {
      event.preventDefault();
      void openMenuPage('home');
    }
  }

  onMount(() => {
    loadLanguage();
    lang = getLanguage();
    langChoice = lang;
    route = parseRoute(location.hash);
    const stopLang = subscribeLanguage(() => {
      lang = getLanguage();
    });
    const stopSmoke = air ? startSmoke(air) : () => {};
    const stopBridge = connect((type, data) => {
      /* raceTape: a pit wall order re-ran the race (#286); the clock it carries tells the race screen to read it again. */
      if (type === 'raceClock' || type === 'raceTape') {
        raceClock = data as LiveClockView;
        return;
      }
      if (type === 'raceFinished') racedNow = true;
      if (type === 'seasonChanged') seasonNow = true;
      /* While days run, the loop reads the world once per day itself. */
      if (!running && (type === 'dayAdvanced' || type === 'inboxChanged' || type === 'seasonChanged' || type === 'raceFinished')) {
        void refresh().catch(catchFault);
      }
      /* The race ran today: watch it live (the result is already written; leaving early skips nothing). */
      if (type === 'raceFinished') enterRace();
    });
    const onHash = () => {
      if (phase === 'game') void transition(parseRoute(location.hash));
    };
    window.addEventListener('hashchange', onHash);
    window.addEventListener('keydown', onKey);
    void ready()
      .then(() => refresh())
      .then(() => {
        /* A career that is already open on the bridge (a reloaded page) goes straight back to the game. */
        if (session?.started) {
          savedDate = shell?.date ?? null;
          phase = 'game';
        }
      })
      .catch(catchFault)
      .finally(() => (booted = true));
    return () => {
      stopLang();
      stopSmoke();
      stopBridge();
      window.removeEventListener('hashchange', onHash);
      window.removeEventListener('keydown', onKey);
    };
  });

  $effect(() => {
    document.documentElement.lang = lang;
  });

  $effect(() => {
    /* The shell keeps its state while the race is on: the node is detached and put back exactly where it was. */
    if (!appEl) return;
    if (racing && !appSlot && appEl.parentNode) {
      appSlot = { parent: appEl.parentNode, next: appEl.nextSibling };
      appEl.remove();
    } else if (!racing && appSlot) {
      appSlot.parent.insertBefore(appEl, appSlot.next);
      appSlot = null;
    }
  });

  $effect(() => {
    saveSettings(settings);
  });

  $effect(() => {
    /* A new important message raises a toast, whether the day came from a run or from a click. */
    shell?.importantItemId;
    if (phase === 'game') tellAboutImportant();
  });

  $effect(() => {
    if (langChoice !== getLanguage()) setLanguage(langChoice);
  });

  $effect(() => {
    route;
    shell?.inboxOpen;
    phase;
    menuPage;
    saves.length;
    queueMicrotask(placeNav);
  });

  $effect(() => {
    /* Every team is drawn in its own colours; the main menu has none and keeps the default ink. */
    const id = quick ? quick.organizationId : phase === 'game' ? shell?.organizationId : null;
    const body = document.body;
    if (!id) {
      delete body.dataset.team;
      for (const name of ['--t1', '--t2', '--on1', '--on2']) body.style.removeProperty(name);
      return;
    }
    const colours = livery(id);
    if (id === 'ferrari' || id === 'lotus' || id === 'tyrrell') body.dataset.team = id;
    else delete body.dataset.team;
    body.style.setProperty('--t1', colours.main);
    body.style.setProperty('--t2', colours.accent);
    body.style.setProperty('--on1', colours.on);
    body.style.setProperty('--on2', colours.onAccent);
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
  let inGame = $derived(phase === 'game' && session?.started === true);
  let latest = $derived(latestSave(saves));
  let ordered = $derived(newestFirst(saves));
  let faultText = $derived(fault ? t(fault.key, fault.parameters) : '');
  let unsavedSince = $derived(savedDate !== null && shell && shell.date !== savedDate ? savedDate : null);
  let lit = $derived(navOwner(route.name));
  let teamId = $derived(shell?.organizationId ?? '');
  let raceDays = $derived(shell && nextRace?.date ? daysBetween(shell.date, nextRace.date) : null);
  let ticks = $derived.by(() => {
    if (!shell || raceDays === null || raceDays < 0 || raceDays > TICK_DAYS) return [];
    const list: { cls: string; date: string }[] = [];
    for (let day = 0; day <= raceDays; day++) {
      const date = addDays(shell.date, day) ?? '';
      const weekday = weekdayIndex(date);
      const cls = day === 0 ? 'now' : day === raceDays ? 'finish' : weekday === 5 || weekday === 6 ? 'we' : '';
      list.push({ cls, date });
    }
    return list;
  });
  let rounds = $derived(screenData.kind === 'pulpit' ? screenData.calendar.rounds.length : 0);
</script>

{@html flagSprite()}
<canvas id="air" aria-hidden="true" bind:this={air}></canvas>
{#if !inGame && booted}<div class="menu-drift" aria-hidden="true"><i></i><i></i><i></i></div>{/if}
<div class="app" bind:this={appEl} class:menu={!inGame} class:still={!settings.menuMotion} class:booting={!booted}>
  {#if inGame}
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
            <a href={`#/${item.id}`} data-r={item.id} class:on={lit === item.id} aria-current={lit === item.id ? 'page' : undefined}>
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
        <a href={`#/${SETTINGS.id}`} data-r={SETTINGS.id} class:on={lit === SETTINGS.id}>
          {@html icon(SETTINGS.icon)}
          {t(SETTINGS.key)}
        </a>
      </div>
      <div class="stripes"></div>
    </aside>
  {/if}
  <div class="content" bind:this={contentEl}>
    {#if inGame}
      <header class="top">
        <div class="hud">
          <button class="cell menu-btn" type="button" aria-label={t('game.menu.open')} title={t('game.menu.open')} onclick={openGameMenu}>{@html icon(ICON.menu, 22)}</button>
          <a class="cell me" href="#/menedzer">
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
          {#if liveOpen}
            <button class="cell live" type="button" onclick={enterRace}>
              <span class="meta">{t('live.ui.live')}</span>
              <b>{t('live.ui.watch')}</b>
            </button>
          {/if}
          {#if nextRace?.circuitName && nextRace.round}
            <a class="cell next" href={`#/wyscig/${nextRace.round}`}>
              <span class="meta">{nextRace.circuitName} · {raceDays !== null && raceDays > 0 ? tr.tCount('shell.days', raceDays) : t('shell.today')}</span>
              {#if ticks.length > 0}
                <span class="ticks">{#each ticks as item (item.date)}<i class={item.cls} title={formatDate(item.date, lang)}></i>{/each}</span>
              {/if}
            </a>
          {/if}
          <a class="cell date" href="#/kalendarz"><b>{shell ? formatDate(shell.date, lang) : '—'}</b></a>
        </div>
        <button class="go" class:running type="button" aria-disabled={!shell || (busy && !running)} title={decision ? tr.tMsg(decision.subject) : undefined} onclick={nextDay}>
          <span>
            <b>{running ? t('shell.pause') : t('shell.next')}</b>
            {#if !running && blocking}<small><i class="blk"></i>{blocking}</small>{:else if !running && raceDays === 0}<small><i class="blk"></i>{t('shell.go.race')}</small>{/if}
          </span>
          <span class="arr">{@html icon(running ? ICON.pause : ICON.arrow)}</span>
        </button>
      </header>
    {/if}
    <main id="view" class:noscroll={inGame && route.name === 'pulpit'} bind:this={viewEl}>
      {#if !inGame}
        {#if menuPage === 'home'}
          <MenuHome {tr} {latest} saveCount={saves.length} {busy} canQuit={canExit()} onContinue={continueCareer} onOpen={openMenuPage} onQuit={exitApp} />
        {:else if menuPage === 'new'}
          <div class="menu-page">
            <div class="screen-head"><h1 class="screen">{t('menu.new')}</h1><button class="btn sm back-home" type="button" onclick={() => openMenuPage('home')}>{@html icon(ICON.back, 16)}<span>{t('career.back')}</span></button></div>
            {#if session}
              <NewCareer {tr} presets={session.presets} suggestedYear={session.suggestedYear} suggestedSeed={session.suggestedSeed} {busy} error={faultText} onStart={beginCareer} />
            {/if}
          </div>
        {:else if menuPage === 'quick'}
          <div class="menu-page">
            <div class="screen-head"><h1 class="screen">{t('menu.quick')}</h1><button class="btn sm back-home" type="button" onclick={() => openMenuPage('home')}>{@html icon(ICON.back, 16)}<span>{t('career.back')}</span></button></div>
            {#if session}
              <QuickRace {tr} suggestedYear={session.suggestedYear} {busy} error={faultText} onStart={startQuickRace} />
            {/if}
          </div>
        {:else if menuPage === 'load'}
          <div class="menu-page narrow">
          <div class="screen-head"><h1 class="screen">{t('menu.load')}</h1><button class="btn sm back-home" type="button" onclick={() => openMenuPage('home')}>{@html icon(ICON.back, 16)}<span>{t('career.back')}</span></button></div>
          <div class="load-page">
            <LoadList saves={ordered} {tr} selected={loadPick} onSelect={(name) => (loadPick = name)} />
            {#if faultText}<p class="bad">{faultText}</p>{/if}
            <div class="confirm">
              <button class="btn primary" type="button" disabled={!loadPick || busy} onclick={() => loadCareer(loadPick)}>
                {@html icon(ICON.check, 17)}<span>{t('menu.load')}</span>
              </button>
            </div>
          </div>
          </div>
        {:else}
          <div class="menu-page narrow">
            <div class="screen-head"><h1 class="screen">{t('menu.settings')}</h1><button class="btn sm back-home" type="button" onclick={() => openMenuPage('home')}>{@html icon(ICON.back, 16)}<span>{t('career.back')}</span></button></div>
            <SettingsPanel {tr} bind:lang={langChoice} bind:settings />
          </div>
        {/if}
      {:else}
        {#if faultText && !gameMenu}
          <p class="bad">{faultText}</p>
        {/if}
        {#if screenData.kind === 'pulpit' && route.name === 'pulpit'}
          <div class="pulpit-wrap">
            {#if session?.peopleNoticeKey}
              <div class="notice"><Status text={t(session.peopleNoticeKey)} tone="warn" /></div>
            {/if}
            <Pulpit data={screenData} {tr} today={shell?.date ?? ''} {teamId} />
          </div>
        {:else if screenData.kind === 'skrzynka' && route.name === 'skrzynka'}
          <Skrzynka data={screenData} {tr} selectedId={route.args[0] ?? null} {busy} onConfirm={confirmChoice} onDismiss={dismissItem} onDismissMany={dismissMany} />
        {:else if screenData.kind === 'kalendarz' && route.name === 'kalendarz'}
          <Kalendarz data={screenData} {tr} />
        {:else if screenData.kind === 'wyscig' && route.name === 'wyscig'}
          <Wyscig data={screenData} {tr} {teamId} />
        {:else if screenData.kind === 'klasyfikacje' && route.name === 'klasyfikacje'}
          <Klasyfikacje data={screenData} {tr} {teamId} {rounds} />
        {:else if screenData.kind === 'kierowcy' && route.name === 'kierowcy'}
          <Kierowcy data={screenData} {tr} today={shell?.date ?? ''} />
        {:else if screenData.kind === 'kierowca' && route.name === 'kierowca'}
          <Kierowca data={screenData} {tr} today={shell?.date ?? ''} {teamId} {busy} {act} />
        {:else if screenData.kind === 'porownaj' && route.name === 'porownaj'}
          <Porownaj data={screenData} {tr} />
        {:else if screenData.kind === 'personel' && route.name === 'personel'}
          <Personel data={screenData} {tr} {teamId} />
        {:else if screenData.kind === 'osoba' && route.name === 'osoba'}
          <Osoba data={screenData} {tr} id={route.args[0] ?? ''} {teamId} {busy} {act} />
        {:else if screenData.kind === 'auto' && route.name === 'auto'}
          <Auto data={screenData} {tr} {teamId} {busy} {act} />
        {:else if screenData.kind === 'rynek' && route.name === 'rynek'}
          <Rynek data={screenData} {tr} />
        {:else if screenData.kind === 'negocjacja' && route.name === 'negocjacja'}
          <Negocjacja data={screenData} {tr} id={route.args[0] ?? ''} {busy} {act} />
        {:else if screenData.kind === 'infrastruktura' && route.name === 'infrastruktura'}
          <Infrastruktura data={screenData} {tr} {busy} {act} />
        {:else if screenData.kind === 'finanse' && route.name === 'finanse'}
          <Finanse data={screenData} {tr} />
        {:else if screenData.kind === 'sponsorzy' && route.name === 'sponsorzy'}
          <Sponsorzy data={screenData} {tr} {teamId} {busy} {act} />
        {:else if screenData.kind === 'zarzad' && route.name === 'zarzad'}
          <Zarzad data={screenData} {tr} {teamId} />
        {:else if screenData.kind === 'menedzer' && route.name === 'menedzer'}
          <Menedzer data={screenData} {tr} {teamId} />
        {:else if screenData.kind === 'dostawcy' && route.name === 'dostawcy'}
          <Dostawcy data={screenData} {tr} {teamId} {busy} {act} />
        {:else if screenData.kind === 'akademia' && route.name === 'akademia'}
          <Akademia data={screenData} {tr} {busy} {act} />
        {:else if route.name === 'ustawienia'}
          <div class="screen-head">
            <h1 class="screen">{t(SETTINGS.key)}</h1>
          </div>
          <SettingsPanel {tr} bind:lang={langChoice} bind:settings />
        {:else}
          <div class="screen-head">
            <h1 class="screen">{t(screenKey(route.name))}</h1>
          </div>
        {/if}
      {/if}
    </main>
    <div id="wipe" aria-hidden="true" bind:this={wipeEl}><i></i><i></i><i></i></div>
  </div>
</div>
{#if racing && quick}
  <RaceLive {tr} pushed={raceClock} onexit={leaveQuickRace} backKey="quick.back" canLeave />
{:else if racing && inGame}
  <RaceLive {tr} pushed={raceClock} onexit={leaveRace} />
{/if}
{#if gameMenu && inGame}
  <GameMenu
    {tr}
    {saves}
    {savedName}
    {unsavedSince}
    {busy}
    error={faultText}
    bind:lang={langChoice}
    bind:settings
    onClear={() => (fault = null)}
    onSave={saveCareer}
    onLoad={loadCareer}
    onToMenu={leaveToMenu}
    onClose={() => (gameMenu = false)}
  />
{/if}
<Toasts items={toasts} onClose={closeToast} />
