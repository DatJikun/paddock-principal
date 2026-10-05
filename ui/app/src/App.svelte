<script lang="ts">
  import { onMount, tick } from 'svelte';
  import { BridgeError, command, connect, HUMAN_MANAGER_ID, query, ready } from './lib/api/client';
  import type { NextRaceView, SaveListItem, SessionView, ShellView, TeamOptionView } from './lib/api/types.generated';
  import Fields from './lib/components/Fields.svelte';
  import StartScreen, { type CareerForm } from './lib/components/StartScreen.svelte';
  import Status from './lib/components/Status.svelte';
  import Tabs from './lib/components/Tabs.svelte';
  import { addDays, daysBetween, formatDate, weekdayIndex } from './lib/date.mjs';
  import { flagSprite } from './lib/flags.mjs';
  import { getLanguage, loadLanguage, setLanguage, subscribeLanguage, translate, type Language } from './lib/i18n';
  import { formatMoney } from './lib/money.mjs';
  import { afterAdvance, blockingLabel, nextAction } from './lib/protocol.mjs';
  import { loadScreen, type ScreenData } from './lib/screens';
  import { NAV, navOwner, parseRoute, screenKey, SETTINGS } from './lib/shell-nav.mjs';
  import { startSmoke } from './lib/smoke';
  import { sweep } from './lib/sweep';
  import { icon, ICON, initials, translator } from './lib/ui';
  import Kalendarz from './screens/Kalendarz.svelte';
  import Klasyfikacje from './screens/Klasyfikacje.svelte';
  import Pulpit from './screens/Pulpit.svelte';
  import Skrzynka from './screens/Skrzynka.svelte';
  import Wyscig from './screens/Wyscig.svelte';

  type Route = { name: string; args: string[] };

  const call = { managerId: HUMAN_MANAGER_ID };
  /* The top bar shows one square per day only when the race is close enough for the squares to fit. */
  const TICK_DAYS = 21;

  let lang = $state<Language>('pl');
  let langChoice = $state<Language>('pl');
  let session = $state<SessionView | null>(null);
  let shell = $state<ShellView | null>(null);
  let nextRace = $state<NextRaceView | null>(null);
  let teams = $state<TeamOptionView[]>([]);
  let saves = $state<SaveListItem[]>([]);
  let year = $state(1955);
  let yearSeeded = false;
  let saveDraft = $state('career');
  let savedName = $state<string | null>(null);
  let route = $state<Route>({ name: 'pulpit', args: [] });
  let screenData = $state<ScreenData>({ kind: 'none' });
  let fault = $state<BridgeError | null>(null);
  let busy = $state(false);
  let moving = false;
  let queued = false;
  let refreshToken = 0;

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
      nextRace = null;
      screenData = { kind: 'none' };
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
    const [nextShell, race, data] = await Promise.all([
      query('shell', call),
      query('nextRace', call),
      loadScreen(route.name, route.args),
    ]);
    if (token !== refreshToken) return;
    shell = nextShell;
    nextRace = race;
    screenData = data;
    fault = null;
  }

  async function transition(next: Route) {
    if (next.name === route.name && next.args.join('/') === route.args.join('/')) return;
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

  async function nextDay() {
    if (!shell || busy || moving) return;
    const action = nextAction(shell);
    if (action.type === 'show') {
      location.hash = `#/skrzynka/${encodeURIComponent(action.itemId)}`;
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
      route = { name: 'pulpit', args: [] };
      location.hash = '#/pulpit';
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
      route = { name: 'pulpit', args: [] };
      location.hash = '#/pulpit';
      await refresh();
    } catch (error) {
      catchFault(error);
    } finally {
      busy = false;
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

  onMount(() => {
    loadLanguage();
    lang = getLanguage();
    langChoice = lang;
    route = parseRoute(location.hash);
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
      void transition(parseRoute(location.hash));
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
    route;
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
  let started = $derived(session?.started === true);
  let lit = $derived(navOwner(route.name));
  let teamId = $derived(shell?.organizationId ?? '');
  let raceDays = $derived(shell && nextRace?.date ? daysBetween(shell.date, nextRace.date) : null);
  let ticks = $derived.by(() => {
    if (!shell || raceDays === null || raceDays < 0 || raceDays > TICK_DAYS) return [];
    const list: { cls: string; date: string }[] = [];
    for (let day = 0; day <= raceDays; day++) {
      const date = addDays(shell.date, day) ?? '';
      const weekday = weekdayIndex(date);
      const cls = day === 0 ? 'now' : day === raceDays ? 'race' : weekday === 5 || weekday === 6 ? 'we' : '';
      list.push({ cls, date });
    }
    return list;
  });
  let rounds = $derived(screenData.kind === 'pulpit' ? screenData.calendar.rounds.length : 0);
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
    {/if}
    <div class="stripes"></div>
  </aside>
  <div class="content" bind:this={contentEl}>
    {#if started}
      <header class="top">
        <div class="hud">
          <span class="cell me">
            <span class="av">{initials(shell?.organizationName ?? '')}</span>
            <span><b>{shell?.organizationName ?? '—'}</b><small>{t('shell.role')}</small></span>
          </span>
          <span class="cell">
            <span class="meta">{t('shell.cash')}</span>
            <span class="num v">{formatMoney(shell?.cashCents ?? null, lang)}</span>
          </span>
        </div>
        <div class="spacer"></div>
        <div class="hud">
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
        <button class="go" type="button" aria-disabled={!shell || busy} title={decision ? tr.tMsg(decision.subject) : undefined} onclick={nextDay}>
          <span>
            <b>{t('shell.next')}</b>
            {#if blocking}<small><i class="blk"></i>{blocking}</small>{/if}
          </span>
          <span class="arr">{@html icon(ICON.arrow)}</span>
        </button>
      </header>
    {/if}
    <main id="view" class:noscroll={started && route.name === 'pulpit'} bind:this={viewEl}>
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
        {#if fault}
          <p class="bad">{t(fault.key, fault.parameters)}</p>
        {/if}
        {#if screenData.kind === 'pulpit' && route.name === 'pulpit'}
          <div class="pulpit-wrap">
            {#if session?.peopleNoticeKey}
              <div class="notice"><Status text={t(session.peopleNoticeKey)} tone="warn" /></div>
            {/if}
            <Pulpit data={screenData} {tr} today={shell?.date ?? ''} {teamId} />
          </div>
        {:else if screenData.kind === 'skrzynka' && route.name === 'skrzynka'}
          <Skrzynka data={screenData} {tr} selectedId={route.args[0] ?? null} {busy} onConfirm={confirmChoice} onDismiss={dismissItem} />
        {:else if screenData.kind === 'kalendarz' && route.name === 'kalendarz'}
          <Kalendarz data={screenData} {tr} />
        {:else if screenData.kind === 'wyscig' && route.name === 'wyscig'}
          <Wyscig data={screenData} {tr} {teamId} />
        {:else if screenData.kind === 'klasyfikacje' && route.name === 'klasyfikacje'}
          <Klasyfikacje data={screenData} {tr} {teamId} {rounds} />
        {:else if route.name === 'ustawienia'}
          <div class="screen-head">
            <h1 class="screen">{t(SETTINGS.key)}</h1>
          </div>
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
