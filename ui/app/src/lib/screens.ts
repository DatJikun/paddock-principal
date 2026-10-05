import { HUMAN_MANAGER_ID, query } from './api/client';
import type {
  BoardView,
  CalendarRoundView,
  CalendarView,
  InboxView,
  NextRaceView,
  RaceResultView,
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

export type StandingsData = { kind: 'klasyfikacje'; standings: StandingsView };

export type ScreenData = PulpitData | InboxData | CalendarData | RaceData | StandingsData | { kind: 'none' };

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
    case 'klasyfikacje':
      return { kind: 'klasyfikacje', standings: await query('standings', call) };
    default:
      return { kind: 'none' };
  }
}
