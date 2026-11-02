<script lang="ts">
  import type { ReportLineView, SummaryStandingView } from '../lib/api/types.generated';
  import PersonName from '../lib/components/PersonName.svelte';
  import { livery } from '../lib/livery.mjs';
  import { formatMoney } from '../lib/money.mjs';
  import { retirementLabel } from '../lib/race.mjs';
  import type { SummaryData } from '../lib/screens';
  import { icon, ICON, points, type Tr } from '../lib/ui';
  import { placeMove } from '../lib/weekend.mjs';

  let { data, tr, teamId }: { data: SummaryData; tr: Tr; teamId: string } = $props();

  let summary = $derived(data.summary);
  let result = $derived(summary.result);
  let own = $derived(result.rows.filter((row) => row.teamId === teamId));
  let story = $derived(result.sections.find((section) => section.title.key === 'race.section.race') ?? null);
  let total = $derived(summary.money.reduce((sum, line) => sum + line.amountCents, 0));

  function line(item: ReportLineView) {
    const parameters: Record<string, string> = {};
    for (const arg of item.args) parameters[arg.name] = arg.value;
    return item.count === null ? tr.t(item.key, parameters) : tr.tCount(item.key, item.count, parameters);
  }

  function move(row: SummaryStandingView) {
    return placeMove(row.before, row.after);
  }
</script>

{#snippet place(row: SummaryStandingView)}
  {@const moved = move(row)}
  <div class="place">
    <b class="num">P{row.after}</b>
    {#if moved.kind === 'up'}<span class="up num">▲ {moved.by}</span>{:else if moved.kind === 'down'}<span class="down num">▼ {moved.by}</span>{:else if moved.kind === 'same'}<span class="meta">=</span>{/if}
  </div>
  <span class="meta num">{points(tr, row.pointsBefore)} → {points(tr, row.pointsAfter)}</span>
{/snippet}

{#if summary.found}
  <div class="summary">
    <section class="panel sm-head">
      <span class="rno big">{summary.round}</span>
      <div class="sm-title">
        <span class="meta">{tr.t('summary.title')}</span>
        <h1 class="screen">{data.round?.circuitName ?? result.layoutId}</h1>
      </div>
      <a class="btn primary" href="#/kalendarz">{@html icon(ICON.check, 17)}<span>{tr.t('summary.done')}</span></a>
    </section>

    <div class="sm-grid">
      <section class="panel">
        <header><h2>{tr.t('summary.result')}</h2></header>
        <div class="body cars">
          {#each own as row (row.driverId)}
            <div class="car" style={`--team:${livery(row.teamId).main}`}>
              <span class="pos num">{row.classified ? `P${row.position}` : tr.t('race.dnf')}</span>
              <div class="who">
                <PersonName name={row.driverName} id={row.driverId} nationality={row.nationality} />
                <span class="meta">
                  {#if row.classified}{tr.t('race.grid')} {row.gridPosition ?? '—'}{:else}{tr.t(retirementLabel(row.retirementKey))}{/if}
                </span>
              </div>
              <b class="num pts">{row.points === '0' ? '' : `+${points(tr, row.points)}`}</b>
            </div>
          {/each}
        </div>
      </section>

      <section class="panel">
        <header><h2>{tr.t('summary.standings')}</h2></header>
        <div class="body tables">
          {#if summary.team}
            <div class="trow team"><span class="meta">{summary.team.name}</span>{@render place(summary.team)}</div>
          {/if}
          {#each summary.drivers as row (row.id)}
            <div class="trow"><span class="meta">{row.name}</span>{@render place(row)}</div>
          {/each}
        </div>
      </section>

      <section class="panel">
        <header><h2>{tr.t('summary.money')}</h2><b class="num" class:neg={total < 0}>{formatMoney(total, tr.lang)}</b></header>
        <div class="body money">
          {#each summary.money as item, at (at)}
            <div class="mrow"><span>{tr.t(`finance.category.${item.category}`)}</span><span class="num" class:neg={item.amountCents < 0}>{formatMoney(item.amountCents, tr.lang)}</span></div>
          {/each}
          {#if summary.balanceCents !== null}
            <div class="mrow bal"><span class="meta">{tr.t('shell.cash')}</span><b class="num">{formatMoney(summary.balanceCents, tr.lang)}</b></div>
          {/if}
        </div>
      </section>

      {#if summary.board}
        <section class="panel">
          <header><h2>{tr.t('summary.board')}</h2></header>
          <div class="body board">
            <b class="num conf">{summary.board.confidence}</b>
            <span class="v">{tr.tMsg(summary.board.band)}</span>
          </div>
        </section>
      {/if}
    </div>

    {#if story && story.lines.length > 0}
      <section class="panel">
        <header><h2>{tr.t('race.report')}</h2></header>
        <div class="body report-lines"><div class="rl">{#each story.lines as item, at (`${item.key}-${at}`)}<p>{line(item)}</p>{/each}</div></div>
      </section>
    {/if}
  </div>
{:else}
  <section class="panel"><div class="body"><a class="btn" href="#/kalendarz">{tr.t('summary.done')}</a></div></section>
{/if}

<style>
  .summary {
    display: grid;
    gap: 18px;
    max-width: 1180px;
  }
  .sm-head {
    display: grid;
    grid-template-columns: auto 1fr auto;
    align-items: center;
    gap: 22px;
    padding: 18px 22px;
  }
  .sm-title {
    display: grid;
    gap: 4px;
  }
  .sm-grid {
    display: grid;
    grid-template-columns: repeat(2, minmax(0, 1fr));
    gap: 18px;
    align-items: start;
  }
  .cars,
  .tables,
  .money {
    display: grid;
    gap: 10px;
  }
  .car {
    display: grid;
    grid-template-columns: 64px 1fr auto;
    align-items: center;
    gap: 14px;
    padding: 10px 14px;
    border-left: 5px solid var(--team);
    border-radius: 10px;
    background: color-mix(in oklab, var(--card) 70%, transparent);
  }
  .pos {
    font: 900 28px/1 var(--display);
  }
  .who {
    display: grid;
    gap: 2px;
  }
  .pts {
    font-size: 20px;
  }
  .trow {
    display: grid;
    grid-template-columns: 1fr auto auto;
    align-items: center;
    gap: 16px;
  }
  .trow.team .meta {
    font-weight: 700;
  }
  .place {
    display: flex;
    align-items: baseline;
    gap: 8px;
  }
  .place b {
    font: 900 22px/1 var(--display);
  }
  .up {
    color: var(--good, #2f8f4e);
    font-weight: 700;
  }
  .down,
  .neg {
    color: var(--bad, #c0392b);
  }
  .down {
    font-weight: 700;
  }
  .mrow {
    display: flex;
    justify-content: space-between;
    gap: 16px;
  }
  .mrow.bal {
    border-top: 1px solid var(--rule);
    padding-top: 10px;
  }
  .board {
    display: flex;
    align-items: baseline;
    gap: 14px;
  }
  .conf {
    font: 900 40px/1 var(--display);
  }
  .board .v {
    font: 600 18px/1.2 var(--ui);
  }
  @media (max-width: 1100px) {
    .sm-grid {
      grid-template-columns: 1fr;
    }
  }
</style>
