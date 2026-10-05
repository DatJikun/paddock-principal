<script lang="ts">
  import type {
    CalendarRoundView,
    RaceResultView,
    ReportLineView,
    StandingRowView,
    StandingsView,
  } from '../api/types.generated';
  import { formatDate } from '../date.mjs';
  import { hasFlag } from '../flags.mjs';
  import { translate, type Language } from '../i18n';
  import Fields from './Fields.svelte';
  import Flag from './Flag.svelte';
  import Tabs from './Tabs.svelte';

  let {
    kind,
    rounds = [],
    standings = null,
    result = null,
    ownTeam = '',
    lang,
    onRound,
  }: {
    kind: 'calendar' | 'standings' | 'result';
    rounds?: CalendarRoundView[];
    standings?: StandingsView | null;
    result?: RaceResultView | null;
    ownTeam?: string;
    lang: Language;
    onRound?: (round: CalendarRoundView) => void;
  } = $props();

  let table = $state('drivers');

  function t(key: string, parameters: Record<string, string> = {}, count?: number) {
    lang;
    return translate(lang, key, parameters, count);
  }

  function line(item: ReportLineView) {
    const parameters: Record<string, string> = {};
    for (const arg of item.args) parameters[arg.name] = arg.value;
    return t(item.key, parameters, item.count ?? undefined);
  }
</script>

{#if kind === 'calendar'}
  <table class="table fit">
    <thead>
      <tr>
        <th class="c">{t('shell.col.round')}</th>
        <th>{t('shell.col.circuit')}</th>
        <th class="r">{t('shell.col.date')}</th>
      </tr>
    </thead>
    <tbody>
      {#each rounds as round (round.round)}
        <tr class="go-row" class:sel={result?.round === round.round && result?.found} onclick={() => onRound?.(round)}>
          <td class="c num">{round.round}</td>
          <td>
            <span class="person">
              {#if hasFlag(round.country)}<Flag code={round.country} />{/if}
              <span><b>{round.circuitName}</b></span>
            </span>
          </td>
          <td class="r num">{round.race ? formatDate(round.race, lang) : ''}</td>
        </tr>
      {/each}
    </tbody>
  </table>
{:else if kind === 'standings' && standings}
  <div class="screen-head">
    <Tabs
      group="table"
      items={[
        { value: 'drivers', label: t('shell.standings.drivers') },
        { value: 'constructors', label: t('shell.standings.constructors') },
      ]}
      bind:value={table}
    />
  </div>
  {#if standings.rules}
    <Fields
      items={standings.rules.positionPoints.map((points, index) => ({
        label: String(index + 1),
        value: String(points),
        numeric: true,
      }))}
    />
  {/if}
  {@const rows = (table === 'drivers' ? standings.drivers : standings.constructors) as StandingRowView[]}
  <table class="table fit">
    <thead>
      <tr>
        <th class="c">{t('shell.col.position')}</th>
        <th>{table === 'drivers' ? t('shell.col.driver') : t('shell.col.team')}</th>
        <th class="c">{t('shell.col.points')}</th>
        <th class="c">{t('shell.col.wins')}</th>
      </tr>
    </thead>
    <tbody>
      {#each rows as row (row.id)}
        <tr class:mine={table === 'constructors' && row.id === ownTeam}>
          <td class="c num">{row.position}</td>
          <td>{row.name}</td>
          <td class="c num">{row.points}</td>
          <td class="c num">{row.wins}</td>
        </tr>
      {/each}
    </tbody>
  </table>
{:else if kind === 'result' && result?.found}
  <table class="table fit">
    <thead>
      <tr>
        <th class="c">{t('shell.col.position')}</th>
        <th>{t('shell.col.driver')}</th>
        <th>{t('shell.col.team')}</th>
        <th class="c">{t('shell.col.points')}</th>
        <th></th>
      </tr>
    </thead>
    <tbody>
      {#each result.rows as row (`${row.position}-${row.driverId}`)}
        <tr class:mine={row.teamId === ownTeam}>
          <td class="c num">{row.classified ? row.position : ''}</td>
          <td>{row.driverName}</td>
          <td>{row.teamName}</td>
          <td class="c num">{row.points}</td>
          <td>{row.retirementKey ? t(row.retirementKey) : t('race.status.finished')}</td>
        </tr>
      {/each}
    </tbody>
  </table>
  {#each result.sections as section (section.title.key)}
    <section class="report">
      <h2>{line(section.title)}</h2>
      {#each section.lines as item, index (`${item.key}-${index}`)}
        <p>{line(item)}</p>
      {/each}
    </section>
  {/each}
{/if}
