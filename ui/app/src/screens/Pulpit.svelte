<script lang="ts">
  import type { RaceRowView, StandingRowView } from '../lib/api/types.generated';
  import Flag from '../lib/components/Flag.svelte';
  import MailRow from '../lib/components/MailRow.svelte';
  import Status from '../lib/components/Status.svelte';
  import Tabs from '../lib/components/Tabs.svelte';
  import TrackMap from '../lib/components/TrackMap.svelte';
  import { daysBetween, formatDay, formatWeekday } from '../lib/date.mjs';
  import { hasFlag } from '../lib/flags.mjs';
  import { formatLapTime, retirementLabel, timeCell } from '../lib/race.mjs';
  import type { PulpitData } from '../lib/screens';
  import { countryName, icon, ICON, km, points, type Tr } from '../lib/ui';

  let { data, tr, today, teamId }: { data: PulpitData; tr: Tr; today: string; teamId: string } = $props();

  let table = $state('drivers');

  const rank = (item: { status: string; needsDecision: boolean }) =>
    item.status !== 'Open' ? 2 : item.needsDecision ? 0 : 1;
  let mails = $derived([...data.inbox.items].reverse().filter((item) => item.status === 'Open').sort((a, b) => rank(a) - rank(b)).slice(0, 7));
  let next = $derived(data.next.round ? data.calendar.rounds.find((round) => round.round === data.next.round) ?? null : null);
  let daysLeft = $derived(daysBetween(today, data.next.date));
  let titles = $derived(
    data.standings.rules !== null && data.standings.rules.constructors !== 'NoChampionship' && data.standings.constructors.length > 0,
  );
  let rows = $derived(
    ((table === 'constructors' && titles ? data.standings.constructors : data.standings.drivers) as StandingRowView[]).slice(0, 6),
  );
  let latestRound = $derived(data.latest.found ? data.calendar.rounds.find((round) => round.round === data.latest.round && round.season === data.latest.season) ?? null : null);
  let latestRows = $derived(
    data.latest.rows.filter((row, index) => index < 10 || row.teamId === teamId) as RaceRowView[],
  );
  let own = $derived(data.board.own);
</script>

<div class="dash">
  <section class="panel inbox">
    <header>
      <h2>{tr.t('shell.nav.inbox')}</h2>
      {#if data.inbox.openCount > 0}<span class="count">{data.inbox.openCount}</span>{/if}
    </header>
    <div class="list">
      {#each mails as item (item.id)}
        <MailRow {item} {tr} />
      {:else}
        <div class="empty"><Status text={tr.t('pulpit.inbox.empty')} /></div>
      {/each}
    </div>
    <footer><a class="link" href="#/skrzynka">{tr.t('pulpit.inbox.open')}{@html icon(ICON.arrow, 15)}</a></footer>
  </section>

  <div class="col">
    {#if data.next.round && data.next.circuitName}
      <a class="panel race" href={`#/wyscig/${data.next.round}`}>
        <div class="race-top">
          <div>
            <h1 class:long={data.next.circuitName.length > 16}>{data.next.circuitName}</h1>
            <div class="where">
              {#if hasFlag(data.next.country ?? '')}<Flag code={data.next.country ?? ''} size="md" />{/if}
              <span>{countryName(tr, data.next.country)}</span>
              {#if data.next.date}<span class="muted">· {formatWeekday(data.next.date, tr.lang)}</span>{/if}
            </div>
          </div>
          <TrackMap points={data.track?.points} cls="dash-map" label={data.next.circuitName} />
        </div>
        <div class="fields facts">
          <div class="fld"><span class="meta">{tr.t('race.round')}</span><span class="v num">{tr.t('race.roundOf', { round: String(data.next.round), total: String(data.calendar.rounds.length) })}</span></div>
          {#if data.track?.found}
            <div class="fld"><span class="meta">{tr.t('race.length')}</span><span class="v num">{km(tr, data.track.lengthKm)}</span></div>
          {/if}
          {#if next?.qualifying}
            <div class="fld"><span class="meta">{tr.t('race.qualifying')}</span><span class="v">{formatDay(next.qualifying, tr.lang)}</span></div>
          {/if}
          {#if daysLeft !== null}
            <div class="fld"><span class="meta">{tr.t('race.toGo')}</span><span class="v num">{daysLeft > 0 ? tr.tCount('shell.days', daysLeft) : tr.t('shell.today')}</span></div>
          {/if}
        </div>
        {#if data.track && data.track.character.length > 0}
          <div class="tags race-tags">
            {#each data.track.character as tag (tag)}<span class="tag">{tr.t(`track.character.${tag}`)}</span>{/each}
          </div>
        {/if}
      </a>
    {/if}
    {#if data.latest.found}
      <section class="panel last">
        <header>
          <h2>{tr.t('pulpit.lastRace')}</h2>
          {#if latestRound}<span class="meta">{tr.t('race.roundNo', { round: String(latestRound.round) })} · {latestRound.circuitName}</span>{/if}
        </header>
        <div class="tbl-scroll">
          <table class="table tight">
            <tbody>
              {#each latestRows as row (`${row.position}-${row.driverId}`)}
                {@const cell = timeCell(row)}
                <tr class:mine={row.teamId === teamId}>
                  <td class="c num" style="width:48px">{#if row.classified}{row.position}{:else}<span class="bad">{tr.t('race.dnf')}</span>{/if}</td>
                  <td><span class="person">{#if hasFlag(row.nationality)}<Flag code={row.nationality} />{/if}<b>{row.driverName}</b></span></td>
                  <td class="muted">{row.teamName}</td>
                  <td class="r num time" class:muted={cell.kind === 'none'}>
                    {#if cell.kind === 'none'}{row.classified ? '' : tr.t(retirementLabel(row.retirementKey))}{:else if cell.kind === 'lapsDown'}{tr.tCount('race.lapsDown', cell.laps)}{:else}{cell.text}{/if}
                  </td>
                  <td class="r num">{row.points === '0' ? '' : points(tr, row.points)}</td>
                </tr>
              {/each}
            </tbody>
          </table>
        </div>
        {#if data.latest.facts && (data.latest.facts.pole || data.latest.facts.fastestLap)}
          <div class="fields eq last-facts">
            {#if data.latest.facts.pole}
              <div class="fld"><span class="meta">{tr.t('race.pole')}</span><span class="v">{data.latest.facts.pole.driverName}</span></div>
            {/if}
            {#if data.latest.facts.fastestLap}
              <div class="fld"><span class="meta">{tr.t('race.fastestLap')}</span><span class="v">{data.latest.facts.fastestLap.driverName}{#if data.latest.facts.fastestLap.timeMs !== null} <span class="num muted">{formatLapTime(data.latest.facts.fastestLap.timeMs)}</span>{/if}</span></div>
            {/if}
          </div>
        {/if}
        <footer><a class="link" href={`#/wyscig/${data.latest.round}`}>{tr.t('pulpit.lastRace.full')}{@html icon(ICON.arrow, 15)}</a></footer>
      </section>
    {:else if data.calendar.rounds.length > 0}
      <section class="panel season">
        <header><h2>{tr.t('pulpit.season', { season: String(data.calendar.season) })}</h2></header>
        <div class="rounds">
          {#each data.calendar.rounds as round (round.round)}
            <a class="srnd" class:next={round.round === data.next.round} class:past={round.finished} href={`#/wyscig/${round.round}`}>
              <span class="num">{round.round}</span>
              {#if hasFlag(round.country)}<Flag code={round.country} />{:else}<span></span>{/if}
              <b>{round.circuitName}</b>
              <span class="muted">{round.race ? formatDay(round.race, tr.lang) : ''}</span>
            </a>
          {/each}
        </div>
        <footer><a class="link" href="#/kalendarz">{tr.t('shell.nav.calendar')}{@html icon(ICON.arrow, 15)}</a></footer>
      </section>
    {/if}
  </div>

  <div class="col">
    {#if own}
      <section class="panel brd">
        <header><h2>{tr.t('shell.nav.board')}</h2><span class="meta">{tr.t('pulpit.board.confidence')}: {tr.tMsg(own.state.band)}</span></header>
        <div class="body">
          {#each own.why.expectations as goal (goal.id)}
            <div class="goal-line">
              <b>{tr.tMsg(goal.title)}</b>
              <span>{tr.tMsg(goal.requirement)}</span>
            </div>
          {/each}
        </div>
      </section>
    {/if}
    <section class="panel stand">
      <header>
        <h2>{tr.t('pulpit.standings')}</h2>
        <span class="meta num">{tr.t('race.roundOf', { round: String(data.standings.roundsCompleted), total: String(data.standings.totalRounds || data.calendar.rounds.length) })}</span>
      </header>
      {#if titles}
        <div class="tabs-wrap">
          <Tabs
            group="pulpit-standings"
            fill
            items={[
              { value: 'drivers', label: tr.t('shell.standings.drivers') },
              { value: 'constructors', label: tr.t('shell.standings.constructors') },
            ]}
            bind:value={table}
          />
        </div>
      {/if}
      {#if rows.length > 0}
        <table class="table tight">
          <tbody>
            {#each rows as row (row.id)}
              <tr class:mine={row.id === teamId || row.teamId === teamId}>
                <td class="num c" style="width:40px">{row.position}</td>
                <td><span class="person">{#if hasFlag(row.nationality)}<Flag code={row.nationality} />{/if}{row.name}</span></td>
                <td class="r num">{points(tr, row.points)}</td>
              </tr>
            {/each}
          </tbody>
        </table>
      {/if}
      <footer><a class="link" href="#/klasyfikacje">{tr.t('pulpit.standings.full')}{@html icon(ICON.arrow, 15)}</a></footer>
    </section>
  </div>
</div>
