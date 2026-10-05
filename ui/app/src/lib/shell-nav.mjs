export const NAV = [
  { id: 'pulpit', key: 'shell.nav.home', icon: '<path d="M4 11l8-7 8 7v9H4z"/>' },
  { id: 'skrzynka', key: 'shell.nav.inbox', icon: '<path d="M4 6h16v12H4z"/><path d="M4 7l8 6 8-6"/>' },
  { id: 'kalendarz', key: 'shell.nav.calendar', icon: '<rect x="4" y="5" width="16" height="15" rx="2"/><path d="M8 3v4M16 3v4M4 10h16"/>' },
  { id: 'klasyfikacje', key: 'shell.nav.standings', icon: '<path d="M5 20V11M12 20V5M19 20v-6"/>' },
  { sep: true },
  { id: 'kierowcy', key: 'shell.nav.drivers', icon: '<circle cx="12" cy="8" r="4"/><path d="M4 20c1-4 4-6 8-6s7 2 8 6"/>' },
  { id: 'personel', key: 'shell.nav.staff', icon: '<circle cx="9" cy="9" r="3"/><circle cx="17" cy="10" r="2.5"/><path d="M3 19c1-3 3-5 6-5s5 2 6 5M15 15c3 0 5 1.5 6 4"/>' },
  { id: 'akademia', key: 'shell.nav.academy', icon: '<path d="M3 9l9-4 9 4-9 4z"/><path d="M7 11v5c3 2 7 2 10 0v-5"/>' },
  { id: 'auto', key: 'shell.nav.car', icon: '<path d="M8 3.5h8M12 3.5v4M10.5 7.5h3l1 5v4.5l-1.5 3h-3L8.5 17v-4.5z"/><rect x="5" y="7" width="2.6" height="4.2" rx="1"/><rect x="16.4" y="7" width="2.6" height="4.2" rx="1"/><rect x="4.6" y="14.5" width="3" height="4.8" rx="1"/><rect x="16.4" y="14.5" width="3" height="4.8" rx="1"/><path d="M7.5 21h9"/>' },
  { id: 'infrastruktura', key: 'shell.nav.infrastructure', icon: '<path d="M4 20V9l5-3v14M9 20V4l6 3v13M15 20v-9l5 2v7M3 20h18"/>' },
  { sep: true },
  { id: 'dostawcy', key: 'shell.nav.suppliers', icon: '<path d="M3 7h11v9H3zM14 10h4l3 3v3h-7"/><circle cx="7" cy="18" r="1.6"/><circle cx="17" cy="18" r="1.6"/>' },
  { id: 'sponsorzy', key: 'shell.nav.sponsors', icon: '<path d="M12 3l2.6 5.3 5.9.9-4.3 4.1 1 5.8L12 16.4 6.8 19.1l1-5.8L3.5 9.2l5.9-.9z"/>' },
  { id: 'finanse', key: 'shell.nav.finance', icon: '<path d="M12 3v18M17 7H9.5a3 3 0 000 6h5a3 3 0 010 6H6"/>' },
  { id: 'zarzad', key: 'shell.nav.board', icon: '<path d="M4 20h16M6 20V10M10 20V10M14 20V10M18 20V10M3 10l9-6 9 6z"/>' },
  { sep: true },
  { id: 'rynek', key: 'shell.nav.market', icon: '<circle cx="11" cy="11" r="6"/><path d="M20 20l-4.5-4.5"/>' },
  { id: 'monthly', key: 'shell.nav.monthly', icon: '<path d="M5 4h11l3 3v13H5z"/><path d="M8 9h8M8 13h8M8 17h5"/>' },
  { id: 'fia', key: 'shell.nav.fia', icon: '<path d="M12 3v18M5 7h14M7 7l-3 7h6zM17 7l-3 7h6z"/>' },
  { id: 'kronika', key: 'shell.nav.chronicle', icon: '<path d="M6 3h12v18l-6-4-6 4z"/>' },
];

export const SETTINGS = {
  id: 'ustawienia',
  key: 'shell.nav.settings',
  icon: '<circle cx="12" cy="12" r="3"/><path d="M12 2v3M12 19v3M4.9 4.9l2.1 2.1M17 17l2.1 2.1M2 12h3M19 12h3M4.9 19.1L7 17M17 7l2.1-2.1"/>',
};

export const SHELL_KEYS = [
  ...NAV.filter((item) => item.key).map((item) => item.key),
  SETTINGS.key,
  'shell.cash',
  'shell.next',
  'shell.role',
  'shell.confirm',
  'shell.language',
  'shell.lang.pl',
  'shell.lang.en',
  'shell.inbox.open',
  'shell.start.title',
  'shell.start.new',
  'shell.start.load',
  'shell.start.team',
  'shell.start.given',
  'shell.start.family',
  'shell.start.nationality',
  'shell.start.tilt',
  'shell.start.year',
  'shell.start.preset',
  'shell.tilt.none',
  'shell.tilt.negotiation',
  'shell.tilt.peopleManagement',
  'shell.tilt.politics',
  'shell.tilt.business',
  'shell.preset.mostHistorical',
  'shell.preset.balanced',
  'shell.preset.chaos',
  'shell.col.date',
  'shell.col.round',
  'shell.col.circuit',
  'shell.col.points',
  'shell.col.wins',
  'shell.col.position',
  'shell.col.driver',
  'shell.col.team',
  'shell.standings.drivers',
  'shell.standings.constructors',
  'shell.race.result',
  'shell.save',
  'shell.save.name',
  'shell.nextRace',
];

const ids = [...NAV.filter((item) => item.id).map((item) => item.id), SETTINGS.id];

export function screenId(hash) {
  const name = (hash || '').replace(/^#\/?/, '') || 'pulpit';
  return ids.includes(name) ? name : 'pulpit';
}

export function screenKey(id) {
  const item = NAV.find((entry) => entry.id === id) ?? (id === SETTINGS.id ? SETTINGS : NAV[0]);
  return item.key ?? 'shell.nav.home';
}
