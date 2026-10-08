import { HUMAN_MANAGER_ID, query } from './api/client';
import type {
  BoardView,
  CalendarRoundView,
  FinanceView,
  ManagerProfileView,
  ManagerSupplyView,
  PoolView,
  SponsorView,
  CalendarView,
  DevelopmentOverview,
  DriverProfileView,
  DriversView,
  InfrastructureOverview,
  InboxView,
  ManagerCarRoster,
  MarketView,
  NegotiationsView,
  NextRaceView,
  RaceResultView,
  SeasonOverviewView,
  StaffListView,
  StandingsView,
  TrackView,
} from './api/types.generated';

const call = { managerId: HUMAN_MANAGER_ID };

export type PulpitData = {
  kind: 'pulpit';
  inbox: InboxView;
  next: NextRaceView;
  track: TrackView | null;
  standings: StandingsView;
  board: BoardView;
  calendar: CalendarView;
  latest: RaceResultView;
};

export type InboxData = { kind: 'skrzynka'; inbox: InboxView };

export type CalendarData = {
  kind: 'kalendarz';
  calendar: CalendarView;
  next: NextRaceView;
  tracks: Record<string, TrackView>;
};

export type RaceData = {
  kind: 'wyscig';
  calendar: CalendarView;
  round: CalendarRoundView | null;
  result: RaceResultView | null;
  track: TrackView | null;
};

export type StandingsData = { kind: 'klasyfikacje'; standings: StandingsView; overview: SeasonOverviewView };

export type SquadData = { kind: 'kierowcy'; drivers: DriversView; profiles: DriverProfileView[]; season: number };

export type DriverData = {
  kind: 'kierowca';
  profile: DriverProfileView;
  /** The other drivers of the squad, for the comparison link. */
  squad: DriversView;
  negotiations: NegotiationsView;
};

export type CompareData = { kind: 'porownaj'; a: DriverProfileView; b: DriverProfileView; season: number };

export type StaffData = { kind: 'personel' | 'osoba'; staff: StaffListView; drivers: DriversView; today: string };

export type CarData = { kind: 'auto'; cars: ManagerCarRoster; development: DevelopmentOverview; staff: StaffListView };

export type MarketData = {
  kind: 'rynek' | 'negocjacja';
  market: MarketView;
  negotiations: NegotiationsView;
  drivers: DriversView;
};

export type InfraData = { kind: 'infrastruktura'; infra: InfrastructureOverview; today: string };
export type FinanceData = { kind: 'finanse'; finance: FinanceView };
export type SponsorData = { kind: 'sponsorzy'; sponsors: SponsorView; today: string };
export type BoardData = { kind: 'zarzad'; board: BoardView; manager: ManagerProfileView; today: string };
export type ManagerData = { kind: 'menedzer'; board: BoardView; manager: ManagerProfileView };
export type SupplyData = { kind: 'dostawcy'; supply: ManagerSupplyView; today: string };
export type AcademyData = { kind: 'akademia'; pool: PoolView; today: string };

export type ScreenData =
  | FinanceData
  | InfraData
  | SponsorData
  | BoardData
  | ManagerData
  | SupplyData
  | AcademyData
  | PulpitData
  | InboxData
  | CalendarData
  | RaceData
  | StandingsData
  | SquadData
  | DriverData
  | CompareData
  | StaffData
  | CarData
  | MarketData
  | { kind: 'none' };

function profile(personId: string) {
  return query('driver', { managerId: HUMAN_MANAGER_ID, personId });
}

function track(layoutId: string | null | undefined) {
  return layoutId ? query('track', { managerId: HUMAN_MANAGER_ID, layoutId }) : Promise.resolve(null);
}

/** Everything a screen shows, read before the screen is drawn so a transition never reveals an empty page. */
export async function loadScreen(name: string, args: string[]): Promise<ScreenData> {
  switch (name) {
    case 'pulpit': {
      const [inbox, next, standings, board, calendar, latest] = await Promise.all([
        query('inbox', call),
        query('nextRace', call),
        query('standings', call),
        query('board', call),
        query('calendar', call),
        query('raceResult', { managerId: HUMAN_MANAGER_ID, season: null, round: null }),
      ]);
      return { kind: 'pulpit', inbox, next, track: await track(next.layoutId), standings, board, calendar, latest };
    }
    case 'skrzynka':
      return { kind: 'skrzynka', inbox: await query('inbox', call) };
    case 'kalendarz': {
      const [calendar, next] = await Promise.all([query('calendar', call), query('nextRace', call)]);
      const layouts = [...new Set(calendar.rounds.map((round) => round.layoutId))];
      const views = await Promise.all(layouts.map((id) => track(id)));
      const tracks: Record<string, TrackView> = {};
      layouts.forEach((id, index) => {
        const view = views[index];
        if (view) tracks[id] = view;
      });
      return { kind: 'kalendarz', calendar, next, tracks };
    }
    case 'wyscig': {
      const calendar = await query('calendar', call);
      const wanted = Number(args[0]);
      const round = calendar.rounds.find((item) => item.round === wanted) ?? null;
      if (!round) return { kind: 'wyscig', calendar, round: null, result: null, track: null };
      const [result, view] = await Promise.all([
        round.finished
          ? query('raceResult', { managerId: HUMAN_MANAGER_ID, season: round.season, round: round.round })
          : Promise.resolve(null),
        track(round.layoutId),
      ]);
      return { kind: 'wyscig', calendar, round, result, track: view };
    }
    case 'klasyfikacje': {
      const [standings, overview] = await Promise.all([query('standings', call), query('seasonOverview', call)]);
      return { kind: 'klasyfikacje', standings, overview };
    }
    case 'kierowcy': {
      const [drivers, shell] = await Promise.all([query('drivers', call), query('shell', call)]);
      const profiles = await Promise.all(drivers.own.map((driver) => profile(driver.personId)));
      return { kind: 'kierowcy', drivers, profiles, season: Number(shell.date.slice(0, 4)) };
    }
    case 'kierowca': {
      const [view, squad, negotiations] = await Promise.all([
        profile(args[0] ?? ''),
        query('drivers', call),
        query('negotiations', call),
      ]);
      return { kind: 'kierowca', profile: view, squad, negotiations };
    }
    case 'porownaj': {
      const [a, b, shell] = await Promise.all([profile(args[0] ?? ''), profile(args[1] ?? ''), query('shell', call)]);
      return { kind: 'porownaj', a, b, season: Number(shell.date.slice(0, 4)) };
    }
    case 'personel':
    case 'osoba': {
      const [staff, drivers, shell] = await Promise.all([query('staff', call), query('drivers', call), query('shell', call)]);
      return { kind: name, staff, drivers, today: shell.date };
    }
    case 'infrastruktura': {
      const [infra, shell] = await Promise.all([query('infrastructure', call), query('shell', call)]);
      return { kind: 'infrastruktura', infra, today: shell.date };
    }
    case 'finanse':
      return { kind: 'finanse', finance: await query('finance', call) };
    case 'sponsorzy': {
      const [sponsors, shell] = await Promise.all([query('sponsors', call), query('shell', call)]);
      return { kind: 'sponsorzy', sponsors, today: shell.date };
    }
    case 'zarzad': {
      const [board, manager, shell] = await Promise.all([query('board', call), query('manager', call), query('shell', call)]);
      return { kind: 'zarzad', board, manager, today: shell.date };
    }
    case 'menedzer': {
      const [board, manager] = await Promise.all([query('board', call), query('manager', call)]);
      return { kind: 'menedzer', board, manager };
    }
    case 'dostawcy': {
      const [supply, shell] = await Promise.all([query('supply', call), query('shell', call)]);
      return { kind: 'dostawcy', supply, today: shell.date };
    }
    case 'akademia': {
      const [pool, shell] = await Promise.all([query('pool', call), query('shell', call)]);
      return { kind: 'akademia', pool, today: shell.date };
    }
    case 'auto': {
      const [cars, development, staff] = await Promise.all([query('cars', call), query('development', call), query('staff', call)]);
      return { kind: 'auto', cars, development, staff };
    }
    case 'rynek':
    case 'negocjacja': {
      const [market, negotiations, drivers] = await Promise.all([query('market', call), query('negotiations', call), query('drivers', call)]);
      return { kind: name, market, negotiations, drivers };
    }
    default:
      return { kind: 'none' };
  }
}
