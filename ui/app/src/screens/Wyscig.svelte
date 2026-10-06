<script lang="ts">
  import type { ReportLineView } from '../lib/api/types.generated';
  import Flag from '../lib/components/Flag.svelte';
  import TrackMap from '../lib/components/TrackMap.svelte';
  import { formatDate, formatDay } from '../lib/date.mjs';
  import { hasFlag } from '../lib/flags.mjs';
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
  let winners = $derived(track?.winners ?? []);
  let facts = $derived(result?.facts ?? null);
  /* The fastest lap of the race is set in bold in the table. */
  let fastestId = $derived(facts?.fastestLap?.driverId ?? null);
</script>

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
                        <span class="person">{#if hasFlag(row.nationality)}<Flag code={row.nationality} />{/if}<b>{row.driverName}</b></span>
                        <span class="meta team">{row.teamName}</span>
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
          {#if report.length > 0}
            <section class="panel">
              <header><h2>{tr.t('race.report')}</h2></header>
              <div class="body report-lines">
                {#each report as section (section.title.key)}
                  <div class="rl">
                    <span class="meta">{tr.t(REPORT[section.title.key] ?? '')}</span>
                    {#each section.lines as item, at (`${item.key}-${at}`)}<p>{line(item)}</p>{/each}
                  </div>
                {/each}
              </div>
            </section>
          {/if}
        {:else}
          <section class="panel rp-map wide"><TrackMap points={track?.points} cls="big" label={round.circuitName} /></section>
        {/if}
      </div>
      <div class="col">
        {#if result}
          <section class="panel rp-map"><TrackMap points={track?.points} cls="big" label={round.circuitName} /></section>
        {/if}
        {#if facts}
          <section class="panel">
            <header><h2>{tr.t('race.facts')}</h2></header>
            <div class="body">
              <div class="fields eq">
                <div class="fld"><span class="meta">{tr.t('race.laps')}</span><span class="v num">{facts.laps}</span></div>
                <div class="fld"><span class="meta">{tr.t('race.distance')}</span><span class="v num">{km(tr, facts.distanceMeters / 1000)}</span></div>
                {#if facts.pole}
                  <div class="fld"><span class="meta">{tr.t('race.pole')}</span><span class="v">{facts.pole.driverName}</span>{#if facts.pole.timeMs !== null}<span class="v num">{formatLapTime(facts.pole.timeMs)}</span>{/if}</div>
                {/if}
                {#if facts.fastestLap}
                  <div class="fld"><span class="meta">{tr.t('race.fastestLap')}</span><span class="v">{facts.fastestLap.driverName}</span>{#if facts.fastestLap.timeMs !== null}<span class="v num">{formatLapTime(facts.fastestLap.timeMs)}</span>{/if}</div>
                {/if}
              </div>
            </div>
          </section>
        {/if}
        {#if round.practice || round.qualifying || round.race}
        <section class="panel">
          <header><h2>{tr.t('race.day')}</h2></header>
          <div class="body">
            <div class="fields eq">
              {#if round.practice}<div class="fld"><span class="meta">{tr.t('race.practice')}</span><span class="v">{formatDay(round.practice, tr.lang)}</span></div>{/if}
              {#if round.qualifying}<div class="fld"><span class="meta">{tr.t('race.qualifying')}</span><span class="v">{formatDay(round.qualifying, tr.lang)}</span></div>{/if}
              {#if round.race}<div class="fld"><span class="meta">{tr.t('race.day')}</span><span class="v">{formatDay(round.race, tr.lang)}</span></div>{/if}
            </div>
          </div>
        </section>
        {/if}
        {#if track && track.races > 0}
          <section class="panel">
            <header><h2>{tr.t('race.numbers')}</h2></header>
            <div class="body">
              <div class="fields eq">
                <div class="fld"><span class="meta">{tr.t('race.races')}</span><span class="v num">{track.races}</span></div>
                <div class="fld"><span class="meta">{tr.t('race.retirements')}</span><span class="v num">{track.retirements}</span></div>
              </div>
            </div>
          </section>
        {/if}
        {#if winners.length > 0}
          <section class="panel tbl">
            <header><h2>{tr.t('race.winners')}</h2></header>
            <div class="tbl-scroll">
              <table class="table tight">
                <tbody>
                  {#each winners as winner (`${winner.season}-${winner.round}`)}
                    <tr class:mine={winner.teamId === teamId}>
                      <td class="c num">{winner.season}</td>
                      <td><b>{winner.driverName}</b></td>
                      <td class="muted">{winner.teamName}</td>
                    </tr>
                  {/each}
                </tbody>
              </table>
            </div>
          </section>
        {/if}
      </div>
    </div>
  </div>
{/if}
