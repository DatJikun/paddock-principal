/**
 * A result row carries the report sentence key of its retirement ("report.retire.engine": "Lap {lap}: {driver}
 * retires with an engine failure"). A table cell wants the short status of the same reason; anything else is
 * shown through its own key.
 */
export function retirementLabel(key) {
  if (!key) return 'race.status.finished';
  const prefix = 'report.retire.';
  return key.startsWith(prefix) ? `race.retired.${key.slice(prefix.length)}` : key;
}
