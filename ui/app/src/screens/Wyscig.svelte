<script lang="ts">
  import type { ReportLineView } from '../lib/api/types.generated';
  import Flag from '../lib/components/Flag.svelte';
  import PersonName from '../lib/components/PersonName.svelte';
  import Tabs from '../lib/components/Tabs.svelte';
  import TrackMap from '../lib/components/TrackMap.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { hasFlag } from '../lib/flags.mjs';
  import { livery } from '../lib/livery.mjs';
  import { formatLapTime, retirementLabel, timeCell } from '../lib/race.mjs';
  import type { RaceData } from '../lib/screens';
  import { countryName, icon, ICON, km, points, type Tr } from '../lib/ui';

  let { data, tr, teamId }: { data: RaceData; tr: Tr; teamId: string } = $props();

  /* The report's qualifying and race sections read as prose; the classification and points are the table above. */
  const REPORT: Record<string, string> = {
    'race.section.qualifying': 'race.report.qualifying',
    'race.section.race': 'race.report.race',
  };

  function line(item: ReportLineView) {
    const parameters: Record<string, string> = {};
    for (const arg of item.args) parameters[arg.name] = arg.value;
    return item.count === null ? tr.t(item.key, parameters) : tr.tCount(item.key, item.count, parameters);
  }

  let round = $derived(data.round);
  let rounds = $derived(data.calendar.rounds);
  let index = $derived(round ? rounds.findIndex((item) => item.round === round.round) : -1);
  let previous = $derived(index > 0 ? rounds[index - 1] : null);
  let following = $derived(index >= 0 && index < rounds.length - 1 ? rounds[index + 1] : null);
  let result = $derived(data.result?.found ? data.result : null);
  let report = $derived(result ? result.sections.filter((section) => REPORT[section.title.key] && section.lines.length > 0) : []);
  let track = $derived(data.track?.found ? data.track : null);
  let past = $derived(track?.past ?? []);
  let tab = $state('race');
  /* A new round opens on its race. */
  $effect(() => {
    round?.round;
    tab = 'race';
  });
  /* Qualifying is what the weekend stored: the grid each car started from and the pole time. Practice is not simulated, so it has no tab. */
  let grid = $derived(result ? result.rows.filter((row) => row.gridPosition !== null).sort((a, b) => (a.gridPosition ?? 0) - (b.gridPosition ?? 0)) : []);
  let tabs = $derived(grid.length > 0 ? [{ value: 'qualifying', label: tr.t('race.tab.qualifying') }, { value: 'race', label: tr.t('race.tab.race') }] : []);
  let shown = $derived(tabs.length > 0 ? tab : 'race');
  let facts = $derived(result?.facts ?? null);
  /* The fastest lap of the race is set in bold in the table. */
  let fastestId = $derived(facts?.fastestLap?.driverId ?? null);
</script>

{#snippet who(name: string, nationality: string, id: string)}
  <PersonName {name} {id} {nationality} />
{/snippet}

{#snippet teamTag(id: string, name: string)}
  <span class="meta team"><i class="tdot" style={`background:${livery(id).main}`}></i>{name}</span>
{/snippet}

{#if round}
  <div class="race-page">
    <section class="panel rp-head">
      <span class="rno big">{round.round}</span>
      <div class="rp-title">
        <div class="rp-where">
          {#if hasFlag(round.country)}<Flag code={round.country} size="lg" />{/if}
          <span class="meta">{round.race ? formatDate(round.race, tr.lang) : ''}</span>
        </div>
        <h1 class="screen">{round.circuitName}</h1>
        <div class="fields mid">
          <div class="fld"><span class="meta">{tr.t('race.country')}</span><span class="v">{countryName(tr, round.country)}</span></div>
          {#if track}
            <div class="fld"><span class="meta">{tr.t('race.lapLength')}</span><span class="v num">{km(tr, track.lengthKm)}</span></div>
          {/if}
          {#if track && track.character.length > 0}
            <div class="fld"><span class="meta">{tr.t('race.character')}</span><span class="v">{track.character.map((tag) => tr.t(`track.character.${tag}`)).join(', ')}</span></div>
          {/if}
        </div>
      </div>
      <div class="rp-tools">
        {#if previous}<a class="btn sm" href={`#/wyscig/${previous.round}`}>{@html icon(ICON.back, 16)}<span>{tr.t('race.roundNo', { round: String(previous.round) })}</span></a>{/if}
        {#if following}<a class="btn sm" href={`#/wyscig/${following.round}`}><span>{tr.t('race.roundNo', { round: String(following.round) })}</span>{@html icon(ICON.arrow, 16)}</a>{/if}
      </div>
    </section>

    <div class="rp-grid">
      <div class="col">
        {#if result}
          {#if tabs.length > 0}
            <div class="rp-tabs"><Tabs group="race-session" items={tabs} bind:value={tab} /></div>
          {/if}
          {#if shown === 'qualifying'}
            <section class="panel tbl">
              <header>
                <h2>{tr.t('race.startingGrid')}</h2>
                {#if facts?.pole && facts.pole.timeMs !== null}<span class="meta">{tr.t('race.poleTime')} · <b class="num">{formatLapTime(facts.pole.timeMs)}</b></span>{/if}
              </header>
              <div class="tbl-scroll">
                <table class="table tight results">
                  <thead>
                    <tr>
                      <th class="c">{tr.t('shell.col.position')}</th>
                      <th>{tr.t('shell.col.driver')}</th>
                      <th>{tr.t('shell.col.team')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {#each grid as row (`${row.gridPosition}-${row.driverId}`)}
                      <tr class:mine={row.teamId === teamId}>
                        <td class="c num">{row.gridPosition}</td>
                        <td>{@render who(row.driverName, row.nationality, row.driverId)}</td>
                        <td>{@render teamTag(row.teamId, row.teamName)}</td>
                      </tr>
                    {/each}
                  </tbody>
                </table>
              </div>
            </section>
            {#each report.filter((section) => section.title.key === 'race.section.qualifying') as section (section.title.key)}
              <section class="panel">
                <header><h2>{tr.t('race.report')}</h2></header>
                <div class="body report-lines">
                  <div class="rl">
                    {#each section.lines as item, at (`${item.key}-${at}`)}<p>{line(item)}</p>{/each}
                  </div>
                </div>
              </section>
            {/each}
          {:else}
            <section class="panel tbl">
              <header><h2>{tr.t('race.results')}</h2></header>
              <div class="tbl-scroll">
                <table class="table tight results">
                  <thead>
                    <tr>
                      <th class="c">{tr.t('shell.col.position')}</th>
                      <th>{tr.t('shell.col.driver')}</th>
                      <th class="c">{tr.t('race.grid')}</th>
                      <th class="c">{tr.t('race.laps')}</th>
                      <th class="r">{tr.t('race.time')}</th>
                      <th class="r wrap">{tr.t('race.bestLap')}</th>
                      <th class="c">{tr.t('shell.col.points')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {#each result.rows as row (`${row.position}-${row.driverId}`)}
                      {@const cell = timeCell(row)}
                      <tr class:mine={row.teamId === teamId}>
                        <td class="c num">{#if row.classified}{row.position}{:else}<span class="bad">{tr.t('race.dnf')}</span>{/if}</td>
                        <td>
                          {@render who(row.driverName, row.nationality, row.driverId)}
                          {@render teamTag(row.teamId, row.teamName)}
                        </td>
                        <td class="c num">{row.gridPosition ?? ''}</td>
                        <td class="c num">{row.lapsCompleted ?? ''}</td>
                        <td class="r num" class:muted={cell.kind === 'none'}>
                          {#if cell.kind === 'none'}{tr.t(retirementLabel(row.retirementKey))}{:else if cell.kind === 'lapsDown'}{tr.tCount('race.lapsDown', cell.laps)}{:else}{cell.text}{/if}
                        </td>
                        <td class="r num" class:best={row.driverId === fastestId}>{formatLapTime(row.fastestLapMs)}</td>
                        <td class="c num">{row.points === '0' ? '' : points(tr, row.points)}</td>
                      </tr>
                    {/each}
                  </tbody>
                </table>
              </div>
            </section>
            {#each report.filter((section) => section.title.key === 'race.section.race') as section (section.title.key)}
              <section class="panel">
                <header><h2>{tr.t('race.report')}</h2></header>
                <div class="body report-lines">
                  <div class="rl">
                    {#each section.lines as item, at (`${item.key}-${at}`)}<p>{line(item)}</p>{/each}
                  </div>
                </div>
              </section>
            {/each}
          {/if}
        {:else}
          <section class="panel rp-map wide"><TrackMap points={track?.points} cls="big" label={round.circuitName} pit /></section>
        {/if}
        {#if past.length > 0}
          <section class="panel tbl">
            <header><h2>{tr.t('race.past')}</h2></header>
            <div class="tbl-scroll past-scroll">
              <table class="table tight past">
                <thead>
                  <tr>
                    <th class="c">{tr.t('race.past.season')}</th>
                    <th>{tr.t('race.past.first')}</th>
                    <th>{tr.t('race.past.second')}</th>
                    <th>{tr.t('race.past.third')}</th>
                  </tr>
                </thead>
                <tbody>
                  {#each past as race (`${race.source}-${race.season}`)}
                    <tr>
                      <td class="c num">{race.season}</td>
                      {#each [1, 2, 3] as place (place)}
                        {@const entry = race.podium.find((item) => item.position === place)}
                        <td class:win={place === 1}>
                          {#if entry}
                            <span class="pod" class:mine={entry.teamId === teamId} style={`--team:${livery(entry.teamId).main}`}>
                              <PersonName name={entry.driverName} nationality={entry.nationality} />
                              <small>{entry.teamName}</small>
                            </span>
                          {/if}
                        </td>
                      {/each}
                    </tr>
                  {/each}
                </tbody>
              </table>
            </div>
          </section>
        {/if}
      </div>
      <div class="col">
        {#if result}
          <section class="panel rp-map"><TrackMap points={track?.points} cls="big" label={round.circuitName} pit /></section>
        {/if}
        {#if facts}
          <section class="panel">
            <header><h2>{tr.t('race.facts')}</h2></header>
            <div class="body">
              <div class="fact-grid">
                <div class="fact"><span class="meta">{tr.t('race.laps')}</span><b class="num">{facts.laps}</b></div>
                <div class="fact"><span class="meta">{tr.t('race.distance')}</span><b class="num">{km(tr, facts.distanceMeters / 1000)}</b></div>
                {#if facts.pole}
                  {@const pole = result?.rows.find((row) => row.driverId === facts.pole?.driverId)}
                  <div class="fact wide">
                    <span class="meta">{tr.t('race.pole')}</span>
                    <span class="fact-name"><PersonName name={facts.pole.driverName} id={facts.pole.driverId} nationality={pole?.nationality ?? ''} /></span>
                    {#if facts.pole.timeMs !== null}<span class="num fact-time">{formatLapTime(facts.pole.timeMs)}</span>{/if}
                  </div>
                {/if}
                {#if facts.fastestLap}
                  {@const quick = result?.rows.find((row) => row.driverId === facts.fastestLap?.driverId)}
                  <div class="fact wide">
                    <span class="meta">{tr.t('race.fastestLap')}</span>
                    <span class="fact-name"><PersonName name={facts.fastestLap.driverName} id={facts.fastestLap.driverId} nationality={quick?.nationality ?? ''} /></span>
                    {#if facts.fastestLap.timeMs !== null}<span class="num fact-time">{formatLapTime(facts.fastestLap.timeMs)}</span>{/if}
                  </div>
                {/if}
              </div>
            </div>
          </section>
        {/if}
        {#if track && track.races > 0}
          <section class="panel">
            <header><h2>{tr.t('race.numbers')}</h2></header>
            <div class="body">
              <div class="fact-grid">
                <div class="fact"><span class="meta">{tr.t('race.races')}</span><b class="num">{track.races}</b></div>
                <div class="fact"><span class="meta">{tr.t('race.retirements')}</span><b class="num">{track.retirements}</b></div>
              </div>
            </div>
          </section>
        {/if}
      </div>
    </div>
  </div>
{/if}
