/* Car development screen, path A (PP-043). Mock view only: the commands are recorded, never simulated. */
const CarDev = (() => {
  const COPY = {
    pl: {
      'screen.title': 'Auto i rozwój',
      'screen.car': 'Auto',
      'screen.people': 'Ludzie',
      'screen.toRace': 'Do wyścigu',
      'split.title': 'Podział zasobów',
      'split.current': 'Bieżące auto',
      'split.account': 'Konto rozwoju',
      'split.next': 'Przyszły rok',
      'split.accountBand': 'Konto',
      'split.sum': 'Suma',
      'split.priorities': 'Priorytety',
      'priority.aero': 'Aerodynamika',
      'priority.chassis': 'Podwozie',
      'priority.reliability': 'Niezawodność',
      'priority.tyres': 'Opony i prowadzenie',
      'confirm': 'Potwierdź',
      'projects.title': 'Projekty',
      'projects.col.project': 'Projekt',
      'projects.col.state': 'Stan',
      'projects.col.progress': 'Postęp',
      'projects.col.gain': 'Zysk',
      'projects.col.why': 'Dlaczego',
      'forecast.title': 'Prognoza',
      'forecast.until': 'Koniec sezonu',
      'forecast.downforce': 'Docisk',
      'forecast.grip': 'Przyczepność',
      'forecast.braking': 'Hamowanie',
      'forecast.reliability': 'Niezawodność',
      'concept.title': 'Koncepcja',
      'concept.gain': 'Dalszy zysk',
      'concept.production': 'Produkcja',
      'concept.cost': 'Koszt',
      'concept.live': 'W aucie od',
      'concept.ends': 'Koniec produkcji',
      'concept.commit': 'Wdrażamy teraz',
      'concept.wait': 'Czekamy',
      'concept.commitFx': 'Produkcja startuje.',
      'concept.waitFx': 'Koncepcja zostaje.',
      'timing.title': 'Termin',
      'timing.whenReady': 'Gdy gotowa',
      'timing.afterRaces': 'Po wyścigach',
      'timing.nextSeason': 'Przyszły sezon',
      'timing.races': 'Wyścigi',
      'reply.title': 'Od inżynierów',
      'reply.keep': 'Trzymaj plan',
      'reply.cut': 'Utnij projekt',
      'reply.keepFx': 'Projekt do końca.',
      'reply.cutFx': 'Zamknięcie teraz.',
      'reply.project': 'Projekt',
      'reply.left': 'Zostało',
      'estimate': 'ESTIMATE',
      'decided': 'Decyzja podjęta',
      'points': 'pkt',
      'kind.upgrade': 'Część',
      'kind.research': 'Badania',
      'kind.concept': 'Koncepcja',
      'status.active': 'W toku',
      'status.ready': 'Gotowa',
      'status.inProduction': 'W produkcji',
      'status.cut': 'Skrócony',
      'area.aero': 'Aerodynamika',
      'area.chassis': 'Podwozie',
      'area.reliability': 'Niezawodność',
      'area.tyres': 'Opony i prowadzenie',
      'day.one': 'dzień',
      'day.few': 'dni',
      'day.many': 'dni',
      'week.one': 'tydzień',
      'week.few': 'tygodnie',
      'week.many': 'tygodni',
      'race.one': 'wyścig',
      'race.few': 'wyścigi',
      'race.many': 'wyścigów',
      'person.one': 'osoba',
      'person.few': 'osoby',
      'person.many': 'osób',
      'error.badSplit': 'development.error.badSplit',
      'error.badPriority': 'development.error.badPriority',
      'error.badTiming': 'development.error.badTiming',
      'error.notReady': 'development.error.notReady',
      'error.notActive': 'development.error.notActive',
      'error.notDeployable': 'development.error.notDeployable',
      'error.noChoice': 'development.error.noChoice',
    },
    en: {
      'screen.title': 'Car and development',
      'screen.car': 'Car',
      'screen.people': 'People',
      'screen.toRace': 'To the race',
      'split.title': 'Resource split',
      'split.current': 'Current car',
      'split.account': 'Development account',
      'split.next': 'Next year',
      'split.accountBand': 'Account',
      'split.sum': 'Sum',
      'split.priorities': 'Priorities',
      'priority.aero': 'Aerodynamics',
      'priority.chassis': 'Chassis',
      'priority.reliability': 'Reliability',
      'priority.tyres': 'Tyres and handling',
      'confirm': 'Confirm',
      'projects.title': 'Projects',
      'projects.col.project': 'Project',
      'projects.col.state': 'State',
      'projects.col.progress': 'Progress',
      'projects.col.gain': 'Gain',
      'projects.col.why': 'Why',
      'forecast.title': 'Forecast',
      'forecast.until': 'Season end',
      'forecast.downforce': 'Downforce',
      'forecast.grip': 'Mechanical grip',
      'forecast.braking': 'Braking',
      'forecast.reliability': 'Reliability',
      'concept.title': 'Concept',
      'concept.gain': 'Further gain',
      'concept.production': 'Production',
      'concept.cost': 'Cost',
      'concept.live': 'On the car from',
      'concept.ends': 'Production ends',
      'concept.commit': 'Commit now',
      'concept.wait': 'Wait',
      'concept.commitFx': 'Production starts.',
      'concept.waitFx': 'The concept stays.',
      'timing.title': 'Timing',
      'timing.whenReady': 'When ready',
      'timing.afterRaces': 'After races',
      'timing.nextSeason': 'Next season',
      'timing.races': 'Races',
      'reply.title': 'From the engineers',
      'reply.keep': 'Keep the plan',
      'reply.cut': 'Cut the project',
      'reply.keepFx': 'Project runs to the end.',
      'reply.cutFx': 'Project closes now.',
      'reply.project': 'Project',
      'reply.left': 'Left',
      'estimate': 'ESTIMATE',
      'decided': 'Decision made',
      'points': 'pts',
      'kind.upgrade': 'Part',
      'kind.research': 'Research',
      'kind.concept': 'Concept',
      'status.active': 'In progress',
      'status.ready': 'Ready',
      'status.inProduction': 'In production',
      'status.cut': 'Cut short',
      'area.aero': 'Aerodynamics',
      'area.chassis': 'Chassis',
      'area.reliability': 'Reliability',
      'area.tyres': 'Tyres and handling',
      'day.one': 'day',
      'day.few': 'days',
      'day.many': 'days',
      'week.one': 'week',
      'week.few': 'weeks',
      'week.many': 'weeks',
      'race.one': 'race',
      'race.few': 'races',
      'race.many': 'races',
      'person.one': 'person',
      'person.few': 'people',
      'person.many': 'people',
      'error.badSplit': 'development.error.badSplit',
      'error.badPriority': 'development.error.badPriority',
      'error.badTiming': 'development.error.badTiming',
      'error.notReady': 'development.error.notReady',
      'error.notActive': 'development.error.notActive',
      'error.notDeployable': 'development.error.notDeployable',
      'error.noChoice': 'development.error.noChoice',
    },
  };

  /* Neutral mock. Bands and durations are display data, not a function of the split. */
  const mock = {
    organizationId: 'tyrrell',
    carName: 'Tyrrell P34',
    headcount: 34,
    daysToNextRace: 11,
    nextRaceName: 'Brands Hatch',
    accountBand: [1.2, 2.4],
    ongoingGain: [0.4, 1.1],
    split: { currentPercent: 60, accountPercent: 15, nextYearPercent: 25 },
    priorities: { aero: 4, chassis: 8, reliability: 5, tyres: 6 },
    forecast: {
      until: { pl: '31 grudnia 1976', en: '31 December 1976' },
      downforce: [5.5, 7.0],
      mechanicalGrip: [7.0, 8.5],
      braking: [6.0, 7.5],
      reliability: [4.5, 6.0],
    },
    projects: [
      {
        id: 'dev:1', kind: 'upgrade', area: 'chassis', engineerId: 'gardner', engineer: 'Derek Gardner',
        status: 'active', progressPercent: 62, gain: [0.3, 0.8],
        name: { pl: 'Przednie zawieszenie', en: 'Front suspension' },
        why: { pl: ['Zapas podwozia', 'Priorytet 8'], en: ['Chassis headroom', 'Priority 8'] },
      },
      {
        id: 'dev:2', kind: 'upgrade', area: 'reliability', engineerId: 'philippe', engineer: 'Maurice Philippe',
        status: 'active', progressPercent: 35, gain: [0.2, 0.5],
        name: { pl: 'Uszczelnienia skrzyni', en: 'Gearbox seals' },
        why: { pl: ['Awarie skrzyni', 'Priorytet 5'], en: ['Gearbox failures', 'Priority 5'] },
      },
      {
        id: 'dev:3', kind: 'research', area: null, engineerId: 'gardner', engineer: 'Derek Gardner',
        status: 'active', progressPercent: 48, gain: [0.2, 0.6],
        name: { pl: 'Małe koła przednie', en: 'Small front wheels' },
        why: { pl: ['Konto rozwoju', 'Priorytet 4'], en: ['Development account', 'Priority 4'] },
      },
      {
        id: 'dev:4', kind: 'concept', area: null, engineerId: 'gardner', engineer: 'Derek Gardner',
        status: 'ready', progressPercent: 100, gain: [1.5, 3.2],
        name: { pl: 'P34B', en: 'P34B' },
        why: { pl: ['Przyszły rok', 'Priorytet 6'], en: ['Next year', 'Priority 6'] },
      },
    ],
    concept: {
      projectId: 'dev:4',
      name: { pl: 'P34B', en: 'P34B' },
      status: 'ready',
      timing: 'NextSeason',
      productionDays: 46,
      productionCostThousands: 48,
      goesLive: { pl: 'GP Holandii · 29 sierpnia', en: 'Dutch GP · 29 August' },
      productionEnds: { pl: '21 sierpnia', en: '21 August' },
    },
    reply: {
      projectId: 'dev:1',
      engineerId: 'gardner',
      engineer: 'Derek Gardner',
      initials: 'DG',
      weeks: 2,
      quote: {
        pl: 'Dajcie nam jeszcze 2 tygodnie na przednie zawieszenie. Jesteśmy blisko.',
        en: 'Give us another 2 weeks on the front suspension. We are close.',
      },
    },
  };

  const SPLIT_KEYS = ['currentPercent', 'accountPercent', 'nextYearPercent'];
  const PRIORITY_KEYS = ['aero', 'chassis', 'reliability', 'tyres'];
  const TIMINGS = ['WhenReady', 'AfterRaces', 'NextSeason'];
  const MAX_RACES = 30;

  function t(lang, key) {
    const pack = COPY[lang] || COPY.pl;
    const text = pack[key];
    if (text == null) throw new Error('missing copy: ' + key);
    return text;
  }

  function lang() {
    return (typeof PREF !== 'undefined' && PREF.lang === 'en') ? 'en' : 'pl';
  }

  function pluralForm(language, n) {
    const a = Math.abs(n);
    if (language === 'en') return a === 1 ? 'one' : 'many';
    if (!Number.isInteger(a)) return 'few';
    if (a === 1) return 'one';
    const d = a % 10, h = a % 100;
    return d >= 2 && d <= 4 && (h < 12 || h > 14) ? 'few' : 'many';
  }

  function formatNum(language, n, digits) {
    return Number(n).toLocaleString(language === 'en' ? 'en-GB' : 'pl-PL', {
      minimumFractionDigits: digits,
      maximumFractionDigits: digits,
    });
  }

  function count(language, n, stem) {
    return `${formatNum(language, n, 0)} ${t(language, stem + '.' + pluralForm(language, n))}`;
  }

  function money(language, thousands) {
    const n = formatNum(language, thousands, 0);
    return language === 'en' ? `£${n}k` : `£${n} tys.`;
  }

  function band(language, pair) {
    return `${formatNum(language, pair[0], 1)}–${formatNum(language, pair[1], 1)}`;
  }

  function clone(v) { return JSON.parse(JSON.stringify(v)); }

  function same(a, b) { return JSON.stringify(a) === JSON.stringify(b); }

  function initial(source) {
    const data = source || mock;
    return {
      split: clone(data.split),
      draft: clone(data.split),
      priorities: clone(data.priorities),
      draftPriorities: clone(data.priorities),
      conceptPick: null,
      conceptCommand: null,
      timingApplied: data.concept.timing,
      timingPick: data.concept.timing,
      racesPick: 3,
      timingCommand: null,
      replyPick: null,
      replyCommand: null,
      production: false,
      cutProjectId: null,
    };
  }

  function nudgeSplit(split, key, delta) {
    if (!SPLIT_KEYS.includes(key) || !Number.isInteger(delta) || delta === 0) return split;
    const next = clone(split);
    const others = ['accountPercent', 'nextYearPercent', 'currentPercent'].filter(k => k !== key);
    if (delta > 0) {
      let left = delta;
      for (const k of others) {
        const take = Math.min(left, next[k]);
        next[k] -= take;
        left -= take;
      }
      if (left > 0) return split;
      next[key] += delta;
    } else {
      if (next[key] + delta < 0) return split;
      next[key] += delta;
      next[others[0]] -= delta;
    }
    return next;
  }

  function nudgePriority(priorities, key, delta) {
    if (!PRIORITY_KEYS.includes(key) || !Number.isInteger(delta) || delta === 0) return priorities;
    const value = priorities[key] + delta;
    if (value < 0 || value > 10) return priorities;
    return { ...priorities, [key]: value };
  }

  function nudgeRaces(races, delta) {
    const value = races + delta;
    if (value < 1 || value > MAX_RACES) return races;
    return value;
  }

  function whole(n) { return Number.isInteger(n); }

  function validateSplit(split) {
    if (!split || !SPLIT_KEYS.every(k => whole(split[k]) && split[k] >= 0 && split[k] <= 100)) {
      return 'error.badSplit';
    }
    const sum = SPLIT_KEYS.reduce((a, k) => a + split[k], 0);
    return sum === 100 ? null : 'error.badSplit';
  }

  function validatePriorities(priorities) {
    if (!priorities || !PRIORITY_KEYS.every(k => whole(priorities[k]) && priorities[k] >= 0 && priorities[k] <= 10)) {
      return 'error.badPriority';
    }
    return null;
  }

  function projectOf(source, id) {
    return source.projects.find(p => p.id === id) || null;
  }

  function buildSplit(source, split, priorities) {
    const bad = validateSplit(split) || validatePriorities(priorities);
    if (bad) return { ok: false, key: bad };
    return {
      ok: true,
      command: {
        command: 'SetDevelopmentSplit',
        organizationId: source.organizationId,
        currentPercent: split.currentPercent,
        accountPercent: split.accountPercent,
        nextYearPercent: split.nextYearPercent,
        aeroPriority: priorities.aero,
        chassisPriority: priorities.chassis,
        reliabilityPriority: priorities.reliability,
        tyresPriority: priorities.tyres,
      },
    };
  }

  function buildDeploy(source, timing, races, status) {
    if (status === 'inProduction' || status === 'cut') return { ok: false, key: 'error.notDeployable' };
    if (status !== 'ready' && status !== 'active') return { ok: false, key: 'error.notDeployable' };
    if (!TIMINGS.includes(timing)) return { ok: false, key: 'error.badTiming' };
    const racesOk = timing === 'AfterRaces' ? whole(races) && races >= 1 && races <= MAX_RACES : races === 0;
    if (!racesOk) return { ok: false, key: 'error.badTiming' };
    return {
      ok: true,
      command: {
        command: 'DeployConcept',
        organizationId: source.organizationId,
        projectId: source.concept.projectId,
        timing,
        races: timing === 'AfterRaces' ? races : 0,
      },
    };
  }

  function buildCommit(source, status) {
    if (status !== 'ready') return { ok: false, key: 'error.notReady' };
    return {
      ok: true,
      command: {
        command: 'CommitConcept',
        organizationId: source.organizationId,
        projectId: source.concept.projectId,
      },
    };
  }

  function buildCut(source, projectId) {
    const project = projectOf(source, projectId);
    if (!project || project.status !== 'active') return { ok: false, key: 'error.notActive' };
    return {
      ok: true,
      command: {
        command: 'CutProject',
        organizationId: source.organizationId,
        projectId,
      },
    };
  }

  function reduce(state, action, source) {
    const data = source || mock;
    const next = clone(state);
    if (action.type === 'nudge-split') {
      next.draft = nudgeSplit(state.draft, action.key, action.delta);
      return { state: next, result: null };
    }
    if (action.type === 'nudge-priority') {
      next.draftPriorities = nudgePriority(state.draftPriorities, action.key, action.delta);
      return { state: next, result: null };
    }
    if (action.type === 'nudge-races') {
      next.racesPick = nudgeRaces(state.racesPick, action.delta);
      return { state: next, result: null };
    }
    if (action.type === 'pick') {
      if (action.group === 'concept' && !state.conceptCommand && !state.production) next.conceptPick = action.value;
      if (action.group === 'reply' && !state.replyCommand) next.replyPick = action.value;
      if (action.group === 'timing' && !state.timingCommand && !state.production) next.timingPick = action.value;
      return { state: next, result: null };
    }
    if (action.type !== 'confirm') return { state, result: { ok: false, key: 'error.noChoice' } };

    if (action.group === 'split') {
      const built = buildSplit(data, state.draft, state.draftPriorities);
      if (!built.ok) return { state, result: built };
      next.split = clone(state.draft);
      next.priorities = clone(state.draftPriorities);
      return { state: next, result: built };
    }
    if (action.group === 'concept') {
      if (state.conceptCommand || state.production) return { state, result: { ok: false, key: 'error.notReady' } };
      if (state.conceptPick !== 'commit' && state.conceptPick !== 'wait') return { state, result: { ok: false, key: 'error.noChoice' } };
      if (state.conceptPick === 'wait') {
        const command = { command: 'Wait', organizationId: data.organizationId, projectId: data.concept.projectId };
        next.conceptCommand = command;
        return { state: next, result: { ok: true, command } };
      }
      const built = buildCommit(data, data.concept.status);
      if (!built.ok) return { state, result: built };
      next.conceptCommand = built.command;
      next.production = true;
      return { state: next, result: built };
    }
    if (action.group === 'timing') {
      if (state.timingCommand || state.production) return { state, result: { ok: false, key: 'error.notDeployable' } };
      const races = state.timingPick === 'AfterRaces' ? state.racesPick : 0;
      const built = buildDeploy(data, state.timingPick, races, data.concept.status);
      if (!built.ok) return { state, result: built };
      next.timingCommand = built.command;
      next.timingApplied = built.command.timing;
      if (built.command.timing === 'WhenReady') next.production = true;
      return { state: next, result: built };
    }
    if (action.group === 'reply') {
      if (state.replyCommand) return { state, result: { ok: false, key: 'error.notActive' } };
      if (state.replyPick !== 'keep' && state.replyPick !== 'cut') return { state, result: { ok: false, key: 'error.noChoice' } };
      if (state.replyPick === 'keep') {
        const command = { command: 'KeepPlan', organizationId: data.organizationId, projectId: data.reply.projectId };
        next.replyCommand = command;
        return { state: next, result: { ok: true, command } };
      }
      if (state.cutProjectId) return { state, result: { ok: false, key: 'error.notActive' } };
      const built = buildCut(data, data.reply.projectId);
      if (!built.ok) return { state, result: built };
      next.replyCommand = built.command;
      next.cutProjectId = data.reply.projectId;
      return { state: next, result: built };
    }
    return { state, result: { ok: false, key: 'error.noChoice' } };
  }

  function statusOf(project, state, source) {
    if (state.cutProjectId === project.id) return 'cut';
    if (state.production && project.id === source.concept.projectId) return 'inProduction';
    return project.status;
  }

  function present(source, state, language) {
    const L = language || 'pl';
    const data = source || mock;
    const conceptStatus = state.production ? 'inProduction' : data.concept.status;
    return {
      organizationId: data.organizationId,
      carName: data.carName,
      headcount: data.headcount,
      daysToNextRace: data.daysToNextRace,
      nextRaceName: data.nextRaceName,
      ongoingGain: data.ongoingGain.slice(),
      accountBand: data.accountBand.slice(),
      productionDays: data.concept.productionDays,
      productionCostThousands: data.concept.productionCostThousands,
      goesLive: data.concept.goesLive[L],
      productionEnds: data.concept.productionEnds[L],
      conceptStatus,
      conceptName: data.concept.name[L],
      forecast: {
        until: data.forecast.until[L],
        downforce: data.forecast.downforce.slice(),
        mechanicalGrip: data.forecast.mechanicalGrip.slice(),
        braking: data.forecast.braking.slice(),
        reliability: data.forecast.reliability.slice(),
      },
      projects: data.projects.map(project => ({
        id: project.id,
        kind: project.kind,
        area: project.area,
        name: project.name[L],
        engineer: project.engineer,
        engineerId: project.engineerId,
        status: statusOf(project, state, data),
        progressPercent: project.progressPercent,
        gain: project.gain.slice(),
        why: project.why[L].slice(),
      })),
      reply: {
        projectId: data.reply.projectId,
        projectName: projectOf(data, data.reply.projectId).name[L],
        engineer: data.reply.engineer,
        initials: data.reply.initials,
        weeks: data.reply.weeks,
        quote: data.reply.quote[L],
      },
    };
  }

  function esc(s) {
    return String(s).replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]));
  }

  function spin(group, key, value, label, decDisabled, incDisabled, step) {
    const s = step || 5;
    const dec = decDisabled ? ' disabled' : '';
    const inc = incDisabled ? ' disabled' : '';
    return `<div class="dev-spin"><button type="button" data-dev="nudge-${group}" data-k="${key}" data-d="${-s}" aria-label="${esc(label)} −"${dec}>−</button><b class="num">${value}</b><button type="button" data-dev="nudge-${group}" data-k="${key}" data-d="${s}" aria-label="${esc(label)} +"${inc}>+</button></div>`;
  }

  function prioritySpin(key, value, label) {
    return `<div class="dev-spin"><button type="button" data-dev="nudge-priority" data-k="${key}" data-d="-1" aria-label="${esc(label)} −"${value <= 0 ? ' disabled' : ''}>−</button><b class="num">${value}<small> / 10</small></b><button type="button" data-dev="nudge-priority" data-k="${key}" data-d="1" aria-label="${esc(label)} +"${value >= 10 ? ' disabled' : ''}>+</button></div>`;
  }

  function choiceRow(group, options, picked, locked) {
    return `<div class="choices" role="radiogroup">${options.map(o => {
      const on = picked === o.value;
      return `<button type="button" class="choice${on ? ' chosen' : ''}" role="radio" aria-checked="${on}" data-dev="pick" data-group="${group}" data-v="${o.value}"${locked ? ' disabled' : ''}><div class="ch"><b>${esc(o.label)}</b><span class="rd">${on ? RD : ''}</span></div>${o.fx ? `<small>${esc(o.fx)}</small>` : ''}</button>`;
    }).join('')}</div>`;
  }

  function confirmBar(ui, language, group, enabled, extra) {
    const dis = enabled ? '' : ' disabled';
    return `<div class="confirm">${extra || ''}${ui.btn(t(language, 'confirm'), { cls: 'primary', icon: ui.check, attrs: ` data-dev="confirm" data-group="${group}"${dis}` })}</div>`;
  }

  function stamp(ui, language, label) {
    return `<div class="stamp">${ui.st(t(language, 'decided'), 'good')}<b>${esc(label)}</b></div>`;
  }

  function tone(status) {
    return status === 'ready' ? 'hi' : status === 'inProduction' ? 'warn' : status === 'cut' ? 'bad' : 'team';
  }

  const RD = '<svg class="i" viewBox="0 0 24 24" aria-hidden="true" style="width:14px;height:14px"><path d="M5 12.5l4.5 4.5L19 7"/></svg>';

  function screen(opts) {
    const options = opts || {};
    const L = options.lang || lang();
    const ui = options.ui || (typeof UI !== 'undefined' ? UI : null);
    const headFn = options.head || (typeof head !== 'undefined' ? head : null);
    if (!ui || !headFn) throw new Error('screen needs UI and head');
    const source = options.source || mock;
    const state = options.state || CarDev.state || initial(source);
    const view = present(source, state, L);
    const est = text => `${text} ${ui.st(t(L, 'estimate'), 'warn')}`;
    const points = (pair, mark) => (mark ? est : x => x)(`${band(L, pair)} ${t(L, 'points')}`);

    const splitSum = SPLIT_KEYS.reduce((a, k) => a + state.draft[k], 0);
    const splitOk = !validateSplit(state.draft) && !validatePriorities(state.draftPriorities);
    const splitDirty = !same(state.draft, state.split) || !same(state.draftPriorities, state.priorities);
    const donorLeft = state.draft.accountPercent + state.draft.nextYearPercent + state.draft.currentPercent;

    let timing = '';
    if (!state.production && !state.timingCommand) {
      const racesExtra = state.timingPick === 'AfterRaces'
        ? ui.fields([{ k: t(L, 'timing.races'), v: `<span class="dev-inline">${spin('races', 'races', count(L, state.racesPick, 'race'), t(L, 'timing.races'), state.racesPick <= 1, state.racesPick >= MAX_RACES, 1)}</span>`, num: 1 }], '')
        : '';
      const changed = state.timingPick !== state.timingApplied || (state.timingPick === 'AfterRaces' && !state.timingCommand);
      timing = `<div class="dev-timing"><span class="meta">${t(L, 'timing.title')}</span>${ui.tabs('dev-timing', [
        ['WhenReady', t(L, 'timing.whenReady')],
        ['AfterRaces', t(L, 'timing.afterRaces')],
        ['NextSeason', t(L, 'timing.nextSeason')],
      ], state.timingPick)}${confirmBar(ui, L, 'timing', changed, racesExtra)}</div>`;
    } else if (state.timingCommand && !state.production) {
      const label = state.timingCommand.timing === 'AfterRaces'
        ? `${t(L, 'timing.afterRaces')} · ${count(L, state.timingCommand.races, 'race')}`
        : t(L, state.timingCommand.timing === 'WhenReady' ? 'timing.whenReady' : 'timing.nextSeason');
      timing = `<div class="dev-timing">${stamp(ui, L, label)}</div>`;
    }

    const rows = [
      ['currentPercent', 'split.current', 'var(--t1)'],
      ['accountPercent', 'split.account', 'var(--a2)'],
      ['nextYearPercent', 'split.next', 'var(--t2)'],
    ];
    const splitBody = `<div class="body"><div class="dev-split-top"><div class="split">${rows.map(([k, key, color]) => `<i style="flex:${state.draft[k] || 0};background:${color}">${state.draft[k] ? state.draft[k] + '%' : ''}</i>`).join('')}</div>
      <div class="dev-splits">${rows.map(([k, key]) => {
        const label = t(L, key);
        const others = donorLeft - state.draft[k];
        return `<div class="dev-line"><span>${label}</span>${spin('split', k, state.draft[k] + '%', label, state.draft[k] <= 0, others <= 0)}</div>`;
      }).join('')}</div></div>
      <div class="dev-split-bot">
      <div class="dev-pri-block"><span class="meta">${t(L, 'split.priorities')}</span><div class="dev-pri">${PRIORITY_KEYS.map(k => `<div class="dev-line"><span>${t(L, 'priority.' + k)}</span>${prioritySpin(k, state.draftPriorities[k], t(L, 'priority.' + k))}</div>`).join('')}${confirmBar(ui, L, 'split', splitDirty && splitOk)}</div></div>
      ${timing}</div></div>`;

    const forecast = `<div class="dev-forecast">${[
      ['forecast.downforce', view.forecast.downforce],
      ['forecast.grip', view.forecast.mechanicalGrip],
      ['forecast.braking', view.forecast.braking],
      ['forecast.reliability', view.forecast.reliability],
    ].map(([key, pair]) => `<span><b>${t(L, key)}</b><span class="num">${band(L, pair)} ${t(L, 'points')}</span></span>`).join('')}</div>`;
    const table = `<div class="dev-list"><table class="table tight"><thead><tr><th>${t(L, 'projects.col.project')}</th><th>${t(L, 'projects.col.state')}</th><th class="c">${t(L, 'projects.col.progress')}</th><th class="r">${t(L, 'projects.col.gain')}</th><th>${t(L, 'projects.col.why')}</th></tr></thead><tbody>${view.projects.map(p => {
      const kind = t(L, 'kind.' + p.kind) + (p.area ? ' · ' + t(L, 'area.' + p.area) : '');
      return `<tr><td><b>${esc(p.name)}</b> <span class="who"><a href="#/osoba/${esc(p.engineerId)}">${esc(p.engineer)}</a> · ${esc(kind)}</span></td><td>${ui.st(t(L, 'status.' + p.status), tone(p.status))}</td><td class="c num">${p.progressPercent}%</td><td class="r num">${band(L, p.gain)}</td><td><span class="dev-bits">${p.why.map(w => `<span>${esc(w)}</span>`).join('')}</span></td></tr>`;
    }).join('')}</tbody></table></div>`;
    const projectsBody = `<div class="body">${forecast}${table}</div>`;

    const conceptFields = ui.fields([
      { k: t(L, 'concept.gain'), v: points(view.ongoingGain, true), num: 1 },
      { k: t(L, 'screen.toRace'), v: `${count(L, view.daysToNextRace, 'day')} · ${esc(view.nextRaceName)}`, num: 1 },
      { k: t(L, 'concept.production'), v: est(count(L, view.productionDays, 'day')), num: 1 },
      { k: t(L, 'concept.cost'), v: est(money(L, view.productionCostThousands)), num: 1 },
      state.production
        ? { k: t(L, 'concept.ends'), v: view.productionEnds, num: 1 }
        : null,
      { k: t(L, 'concept.live'), v: view.goesLive },
    ].filter(Boolean), 'boxed');

    let conceptDecision = '';
    if (state.conceptCommand) {
      const label = state.conceptCommand.command === 'CommitConcept' ? t(L, 'concept.commit') : t(L, 'concept.wait');
      conceptDecision = stamp(ui, L, label);
    } else if (!state.production) {
      conceptDecision = `<div class="reply-actions">${choiceRow('concept', [
        { value: 'commit', label: t(L, 'concept.commit'), fx: t(L, 'concept.commitFx') },
        { value: 'wait', label: t(L, 'concept.wait'), fx: t(L, 'concept.waitFx') },
      ], state.conceptPick, false)}${confirmBar(ui, L, 'concept', !!state.conceptPick)}</div>`;
    }

    const conceptBody = `<div class="body">${conceptFields}${conceptDecision}</div>`;

    let replyBody;
    if (state.replyCommand) {
      const label = state.replyCommand.command === 'CutProject' ? t(L, 'reply.cut') : t(L, 'reply.keep');
      replyBody = `<div class="body"><p class="letter">„${esc(view.reply.quote)}”</p>${stamp(ui, L, label)}</div>`;
    } else {
      replyBody = `<div class="body"><div class="dev-from"><span class="av">${esc(view.reply.initials)}</span><div><b>${esc(view.reply.engineer)}</b><span class="dev-bits"><span>${esc(count(L, view.reply.weeks, 'week'))}</span><span>${esc(view.reply.projectName)}</span></span><p class="letter">„${esc(view.reply.quote)}”</p></div></div>
        <div class="reply-actions">${choiceRow('reply', [
          { value: 'keep', label: t(L, 'reply.keep'), fx: t(L, 'reply.keepFx') },
          { value: 'cut', label: t(L, 'reply.cut'), fx: t(L, 'reply.cutFx') },
        ], state.replyPick, false)}
        ${confirmBar(ui, L, 'reply', !!state.replyPick)}</div></div>`;
    }

    const headFields = ui.fields([
      { k: t(L, 'screen.car'), v: view.carName },
      { k: t(L, 'screen.toRace'), v: count(L, view.daysToNextRace, 'day'), num: 1 },
    ]);

    return `<div class="cardev-screen">${headFn(t(L, 'screen.title'), headFields)}<div class="cardev">
      ${ui.panel(t(L, 'split.title'), splitBody, { cls: 'cardev-split', right: ui.fields([
        { k: t(L, 'split.accountBand'), v: points(view.accountBand, true), num: 1 },
        { k: t(L, 'screen.people'), v: count(L, view.headcount, 'person'), num: 1 },
        { k: t(L, 'split.sum'), v: splitSum + '%', num: 1, cls: splitOk ? '' : 'bad' },
      ], 'hdr') })}
      ${ui.panel(t(L, 'projects.title'), projectsBody, { cls: 'cardev-projects tbl', right: `${ui.st(t(L, 'estimate'), 'warn')}<span class="meta dev-until">${esc(view.forecast.until)}</span>` })}
      ${ui.panel(`${t(L, 'concept.title')} ${esc(view.conceptName)}`, conceptBody, { cls: 'cardev-concept', right: ui.st(t(L, 'status.' + view.conceptStatus), tone(view.conceptStatus)) })}
      ${ui.panel(t(L, 'reply.title'), replyBody, { cls: 'cardev-reply' })}
    </div></div>`;
  }

  function wire(root) {
    root.querySelectorAll('[data-dev]').forEach(el => {
      el.addEventListener('click', event => {
        event.preventDefault();
        const ds = el.dataset;
        let action;
        if (ds.dev.startsWith('nudge-')) {
          action = { type: ds.dev, key: ds.k, delta: Number(ds.d) };
          if (ds.dev === 'nudge-split') action.delta = Number(ds.d);
        } else if (ds.dev === 'pick') action = { type: 'pick', group: ds.group, value: ds.v };
        else if (ds.dev === 'confirm') action = { type: 'confirm', group: ds.group };
        else return;
        const out = reduce(CarDev.state, action, mock);
        CarDev.state = out.state;
        if (out.result && out.result.ok && typeof UI !== 'undefined') UI.toast(t(lang(), 'decided'));
        if (typeof render === 'function' && typeof parse === 'function') render(parse());
      });
    });
  }

  const api = {
    COPY, mock, MAX_RACES, TIMINGS,
    t, lang, pluralForm, count, formatNum, money, band,
    initial, nudgeSplit, nudgePriority, nudgeRaces,
    validateSplit, validatePriorities,
    buildSplit, buildDeploy, buildCommit, buildCut,
    reduce, present, screen, wire,
    state: null,
  };
  api.state = initial(mock);
  return api;
})();

if (typeof S !== 'undefined') {
  S.auto = () => CarDev.screen();
  S.auto.fit = true;
  S.auto.after = () => CarDev.wire(document.getElementById('view'));
}

const carDevRoot = typeof window !== 'undefined' ? window : globalThis;
carDevRoot.CarDev = CarDev;

if (typeof document !== 'undefined') {
  document.addEventListener('tab', e => {
    if (!e.detail || e.detail.group !== 'dev-timing') return;
    if (typeof cur === 'undefined' || cur.name !== 'auto') return;
    const out = CarDev.reduce(CarDev.state, { type: 'pick', group: 'timing', value: e.detail.value }, CarDev.mock);
    CarDev.state = out.state;
    if (typeof render === 'function' && typeof parse === 'function') render(parse());
  });
}
