<script lang="ts">
  import { teamLabel } from '../lib/career.mjs';
  import Status from '../lib/components/Status.svelte';
  import { formatDate, formatDay } from '../lib/date.mjs';
  import type { BoardData } from '../lib/screens';
  import { initials, type Tr } from '../lib/ui';

  let { data, tr, teamId }: { data: BoardData; tr: Tr; teamId: string } = $props();

  let own = $derived(data.board.own);
  let reputation = $derived(data.board.reputation);
  let observer = $derived(data.board.observer);
  const lc = (text: string) => text.charAt(0).toLowerCase() + text.slice(1);
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.board')}</h1>
  {#if own}
    <div class="fields">
      <div class="fld"><span class="meta">{tr.t('shell.col.team')}</span><span class="v">{teamLabel(teamId)}</span></div>
    </div>
  {/if}
</div>

{#if observer}
  <Status text={tr.tMsg(observer.status)} tone="warn" />
{:else if own}
  <div class="board">
    <section class="panel owner">
      <a class="owner-main person" href="#/menedzer">
        <span class="av big">{initials(data.manager.name || teamLabel(teamId))}</span>
        <div>
          <span class="meta">{tr.t('board.you')}</span>
          <h2>{data.manager.name || '—'}</h2>
        </div>
      </a>
      <div class="fields mid">
        <div class="fld">
          <span class="meta">{tr.t('board.confidence')}</span>
          <span class="v num">{own.state.confidence}<small class="muted"> / 100</small></span>
        </div>
        <div class="fld"><span class="meta">{tr.t('board.mood')}</span><span class="v">{tr.tMsg(own.state.band)}</span></div>
        <div class="fld">
          <span class="meta">{tr.t('board.protected')}</span>
          <span class="v">{#if own.state.protected && own.state.protectedUntil}{formatDate(own.state.protectedUntil, tr.lang)}{:else}<Status text={tr.t('board.notProtected')} />{/if}</span>
        </div>
        <div class="fld">
          <span class="meta">{tr.t('board.reviews')}</span>
          <span class="v num">{own.state.reviewsBelowBar} <small class="muted">/ {own.state.reviewsToDismiss}</small></span>
        </div>
      </div>
      <div class="bar"><i style="width:{own.state.confidence}%"></i></div>
      <Status text={tr.tMsg(own.forecast.message)} tone={own.forecast.kind === 'AtRisk' ? 'bad' : ''} />
    </section>

    <div class="board-grid">
      <section class="panel">
        <header><h2>{tr.t('board.expects')}</h2></header>
        <div class="body">
          <div class="fields boxed eq">
            <div class="fld"><span class="meta">{tr.t('board.expectedPosition')}</span><span class="v num">{own.why.expectedPosition !== null ? `P${own.why.expectedPosition}` : '—'}</span></div>
            <div class="fld"><span class="meta">{tr.t('board.currentPosition')}</span><span class="v num">{own.why.currentPosition !== null ? `P${own.why.currentPosition}` : '—'}</span></div>
            {#if own.why.targetConfidence !== null}
              <div class="fld"><span class="meta">{tr.t('board.targetConfidence')}</span><span class="v num">{own.why.targetConfidence}</span></div>
            {/if}
          </div>
          {#each own.why.expectations as goal (goal.id)}
            <div class="goal-line">
              <b>{tr.tMsg(goal.title)}</b>
              <span>{tr.tMsg(goal.requirement)}</span>
              <span class="goal-meta">
                <Status text={tr.t(`objective.status.${lc(goal.state.status)}`)} tone={goal.state.status === 'Met' ? 'good' : goal.state.status === 'Failed' ? 'bad' : ''} />
                <span class="muted num">{formatDay(goal.deadline, tr.lang)}</span>
              </span>
              <small class="muted">{tr.tMsg(goal.onMet)} {tr.tMsg(goal.onFailed)}</small>
              {#if goal.forecast}<small class="muted">{tr.tMsg(goal.forecast.message)}</small>{/if}
            </div>
          {/each}
        </div>
      </section>

      {#if reputation}
        <section class="panel tbl">
          <header><h2>{tr.t('board.reputation')}</h2><span class="v num">{reputation.points}</span></header>
          <div class="body"><Status text={tr.tMsg(reputation.band)} tone="hi" /></div>
          {#if reputation.history.length > 0}
            <table class="table tight">
              <thead><tr><th>{tr.t('shell.col.date')}</th><th>{tr.t('board.reason')}</th><th class="r">{tr.t('board.points')}</th></tr></thead>
              <tbody>
                {#each reputation.history as line, index (index)}
                  <tr>
                    <td class="num">{formatDate(line.on, tr.lang)}</td>
                    <td class="wrap">{tr.tMsg(line.reason)}</td>
                    <td class="r num" class:good={line.points > 0} class:bad={line.points < 0}>{line.points > 0 ? '+' : ''}{line.points}</td>
                  </tr>
                {/each}
              </tbody>
            </table>
          {/if}
        </section>
      {/if}
    </div>
  </div>
{/if}
