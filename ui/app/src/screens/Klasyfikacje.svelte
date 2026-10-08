<script lang="ts">
  import Flag from '../lib/components/Flag.svelte';
  import SeasonGrid from '../lib/components/SeasonGrid.svelte';
  import { hasFlag } from '../lib/flags.mjs';
  import type { StandingsData } from '../lib/screens';
  import { icon, ICON, points, type Tr } from '../lib/ui';

  let { data, tr, teamId, rounds }: { data: StandingsData; tr: Tr; teamId: string; rounds: number } = $props();

  let table = $derived(data.standings);
  let rules = $derived(table.rules);
  let titles = $derived(rules !== null && rules.constructors !== 'NoChampionship');
  let split = $derived(rules !== null && rules.firstQuota > 0 && rules.secondQuota > 0);
  let grid = $state(false);
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('standings.title', { season: String(table.season) })}</h1>
  <div class="fields">
    <div class="fld">
      <span class="meta">{tr.t('standings.after')}</span>
      <span class="v num">{tr.t('race.roundOf', { round: String(table.roundsCompleted), total: String(table.totalRounds || rounds) })}</span>
    </div>
  </div>
  {#if data.overview.rounds.some((round) => round.finished)}
    <button type="button" class="btn ov-open" onclick={() => (grid = true)}>{@html icon(ICON.board, 18)}<span>{tr.t('overview.open')}</span></button>
  {/if}
</div>

{#if grid}
  <SeasonGrid overview={data.overview} {tr} {teamId} onClose={() => (grid = false)} />
{/if}

<div class="stand-grid">
  <section class="panel tbl">
    <header><h2>{tr.t('shell.standings.drivers')}</h2></header>
    <div class="tbl-scroll">
      <table class="table tight">
        <thead>
          <tr>
            <th class="c">{tr.t('shell.col.position')}</th>
            <th>{tr.t('shell.col.driver')}</th>
            <th>{tr.t('shell.col.team')}</th>
            <th class="c">{tr.t('shell.col.wins')}</th>
            <th class="c">{tr.t('shell.col.podiums')}</th>
            <th class="c">{tr.t('shell.col.points')}</th>
          </tr>
        </thead>
        <tbody>
          {#each table.drivers as row (row.id)}
            <tr class:mine={row.teamId === teamId}>
              <td class="c num">{row.position}</td>
              <td><span class="person">{#if hasFlag(row.nationality)}<Flag code={row.nationality} />{/if}{row.name}</span></td>
              <td class="muted">{row.teamName ?? ''}</td>
              <td class="c num" class:zero={row.wins === 0}>{row.wins}</td>
              <td class="c num" class:zero={row.podiums === 0}>{row.podiums}</td>
              <td class="c num"><b>{points(tr, row.points)}</b></td>
            </tr>
          {/each}
        </tbody>
      </table>
    </div>
  </section>
  <div class="col">
    {#if titles && table.constructors.length > 0}
      <section class="panel tbl">
        <header><h2>{tr.t('shell.standings.constructors')}</h2></header>
        <table class="table tight">
          <thead>
            <tr>
              <th class="c">{tr.t('shell.col.position')}</th>
              <th>{tr.t('shell.col.team')}</th>
              <th class="c">{tr.t('shell.col.wins')}</th>
              <th class="c">{tr.t('shell.col.podiums')}</th>
              <th class="c">{tr.t('shell.col.points')}</th>
            </tr>
          </thead>
          <tbody>
            {#each table.constructors as row (row.id)}
              <tr class:mine={row.id === teamId}>
                <td class="c num">{row.position}</td>
                <td>{row.name}</td>
                <td class="c num" class:zero={row.wins === 0}>{row.wins}</td>
                <td class="c num" class:zero={row.podiums === 0}>{row.podiums}</td>
                <td class="c num"><b>{points(tr, row.points)}</b></td>
              </tr>
            {/each}
          </tbody>
        </table>
      </section>
    {/if}
    {#if rules}
      <section class="panel">
        <div class="body scoring-body">
          <div class="scoring">
            <span class="meta">{tr.t('standings.scoring')}</span>
            <div class="pts">
              {#each rules.positionPoints as value, at (at)}
                <div><span class="meta">{at + 1}.</span><b class="num">{value}</b></div>
              {/each}
            </div>
            <div class="fields">
              {#if split}
                <div class="fld"><span class="meta">{tr.t('standings.counted.first')}</span><span class="v num">{tr.t('standings.counted.best', { count: String(rules.firstQuota) })}</span></div>
                <div class="fld"><span class="meta">{tr.t('standings.counted.second')}</span><span class="v num">{tr.t('standings.counted.best', { count: String(rules.secondQuota) })}</span></div>
              {:else}
                <div class="fld">
                  <span class="meta">{tr.t('standings.counted')}</span>
                  <span class="v num">{rules.countedResults > 0 ? tr.t('standings.counted.best', { count: String(rules.countedResults) }) : tr.t('standings.counted.all')}</span>
                </div>
              {/if}
              <div class="fld"><span class="meta">{tr.t('standings.fastestLap')}</span><span class="v">{tr.t(`standings.fastestLap.${rules.fastestLap}`)}</span></div>
              <div class="fld"><span class="meta">{tr.t('standings.constructors')}</span><span class="v">{tr.t(`standings.constructors.${rules.constructors}`)}</span></div>
            </div>
          </div>
        </div>
      </section>
    {/if}
  </div>
</div>
