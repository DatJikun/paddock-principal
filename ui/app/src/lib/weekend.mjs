/*
 * The race weekend's screens (#316). Display only (TECH §3): the bridge decides what a plan may be and refuses it with a key;
 * these helpers only order rows, keep a form and turn it into the bridge's `setRacePlan` call.
 */

/** The steps of a race weekend, in order. */
export const STEPS = ['practice', 'qualifying', 'strategy', 'race'];

/** The order a watched qualifying shows its grid in: from the back to pole, pit-lane starters first. */
export function revealOrder(grid) {
  return [...grid].sort((a, b) => rank(b) - rank(a));
}

/** The grid as it stands after `count` cars have set their time: pole first, only the cars already shown. */
export function revealed(grid, count) {
  const shown = new Set(revealOrder(grid).slice(0, Math.max(0, count)).map((row) => row.carId));
  return [...grid].filter((row) => shown.has(row.carId)).sort((a, b) => rank(a) - rank(b));
}

function rank(row) {
  return row.position === 0 ? Number.MAX_SAFE_INTEGER : row.position;
}

/** The form of one car's plan, from the bridge's plan view. `pace` 'auto' and `stops` null leave them to the strategist. */
export function formOf(plan) {
  return {
    startTyres: plan.startTyres,
    pace: plan.pace ?? 'auto',
    engine: plan.engine,
    stopsMode: plan.stops !== null ? 'own' : 'auto',
    stops: (plan.stops ?? []).map((stop) => ({ lap: stop.lap, tyres: stop.tyres })),
  };
}

/** The `setRacePlan` call of a form. */
export function planCall(form, season, round, carId) {
  return {
    season,
    round,
    carId,
    startTyres: form.startTyres,
    pace: form.pace === 'auto' ? null : form.pace,
    engine: form.engine,
    stops: form.stopsMode === 'own' ? form.stops.map((stop) => ({ lap: Number(stop.lap), tyres: stop.tyres })) : null,
  };
}

/** True when the form says something else than the plan the world holds. */
export function changed(form, plan) {
  return JSON.stringify(planCall(form, 0, 0, '')) !== JSON.stringify(planCall(formOf(plan), 0, 0, ''));
}

/** A new stop for the form: a lap after the last one (or at a third of the race) on the other dry tyres. */
export function nextStop(form, totalLaps, dryTyres) {
  const last = form.stops.length > 0 ? form.stops[form.stops.length - 1] : null;
  const lap = last ? Math.min(totalLaps - 1, Number(last.lap) + Math.max(1, Math.round(totalLaps / 3))) : Math.max(1, Math.round(totalLaps / 3));
  const current = last ? last.tyres : form.startTyres;
  const tyres = dryTyres.find((id) => id !== current) ?? current;
  return { lap, tyres };
}

/** How a place in the table moved over the round: up, down, same, or new (not in the table before). */
export function placeMove(before, after) {
  if (before === null || before === undefined) return { kind: 'new', by: 0 };
  if (after < before) return { kind: 'up', by: before - after };
  if (after > before) return { kind: 'down', by: after - before };
  return { kind: 'same', by: 0 };
}
