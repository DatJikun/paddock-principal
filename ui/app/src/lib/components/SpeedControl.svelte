<script lang="ts">
  /*
   * The race screen's one speed control (#322): a pause/resume button and the watching speed, stepped with the arrows. It shows
   * what the clock says and reports presses. The orders go through RaceLive and the step rules live in race-speed.mjs, so every
   * race view that mounts RaceLive gets the same control.
   */
  import type { Tr } from '../ui';
  import { canStep } from '../race-speed.mjs';

  let {
    tr,
    paused,
    speed,
    speeds,
    ontoggle,
    onstep,
  }: {
    tr: Tr;
    paused: boolean;
    speed: number;
    speeds: readonly number[];
    ontoggle: () => void;
    onstep: (step: number) => void;
  } = $props();
</script>

<div class="speed" role="group" aria-label={tr.t('live.ui.pace')}>
  <button
    type="button"
    class="pp"
    aria-pressed={paused}
    title={paused ? tr.t('live.ui.play') : tr.t('live.ui.pause')}
    onclick={ontoggle}
  >
    {#if paused}▶{:else}❚❚{/if}
  </button>
  <button
    type="button"
    class="step"
    title={tr.t('live.ui.slower')}
    aria-label={tr.t('live.ui.slower')}
    disabled={!canStep(speeds, speed, -1)}
    onclick={() => onstep(-1)}>‹</button
  >
  <b class="value num">{speed}×</b>
  <button
    type="button"
    class="step"
    title={tr.t('live.ui.faster')}
    aria-label={tr.t('live.ui.faster')}
    disabled={!canStep(speeds, speed, 1)}
    onclick={() => onstep(1)}>›</button
  >
</div>

<style>
  .speed {
    display: flex;
    align-items: center;
    gap: 4px;
    height: 44px;
    padding: 0 4px;
  }
  .speed button {
    all: unset;
    box-sizing: border-box;
    cursor: pointer;
    display: inline-flex;
    align-items: center;
    justify-content: center;
    border-radius: 10px;
    color: var(--ov-ink2);
    transition: background 0.2s, color 0.2s;
  }
  .speed button:hover:not(:disabled) {
    background: rgba(255, 255, 255, 0.07);
    color: var(--ov-ink);
  }
  .speed button:disabled {
    opacity: 0.35;
    cursor: default;
  }
  .speed button:focus-visible {
    outline: 2px solid var(--gold);
    outline-offset: 2px;
  }
  /* The button reset above (all: unset) outranks a bare class, so the sizes are written with the same scope. */
  .speed .pp {
    width: 42px;
    height: 36px;
    font: 700 14px/1 var(--ui);
  }
  .speed .pp[aria-pressed='true'] {
    background: var(--ov-ink);
    color: #15181e;
  }
  .speed .step {
    width: 36px;
    height: 36px;
    font: 700 22px/1 var(--ui);
  }
  .speed .value {
    min-width: 64px;
    text-align: center;
    font: 800 19px/1 var(--ui);
    color: var(--ov-ink);
    font-variant-numeric: tabular-nums;
  }
</style>
