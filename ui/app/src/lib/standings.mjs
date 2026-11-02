/**
 * The rows a championship panel lists (#320): the whole table of the chosen title, never a top slice. The constructors'
 * table is offered only when the era has that title and someone is in it.
 */
export function hasConstructorsTitle(standings) {
  return standings.rules !== null && standings.rules.constructors !== 'NoChampionship' && standings.constructors.length > 0;
}

export function championshipRows(standings, table) {
  return table === 'constructors' && hasConstructorsTitle(standings) ? standings.constructors : standings.drivers;
}
