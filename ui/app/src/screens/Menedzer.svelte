<script lang="ts">
  import { teamLabel } from '../lib/career.mjs';
  import AttrRow from '../lib/components/AttrRow.svelte';
  import Flag from '../lib/components/Flag.svelte';
  import Status from '../lib/components/Status.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { hasFlag } from '../lib/flags.mjs';
  import type { ManagerData } from '../lib/screens';
  import { countryName, icon, ICON, initials, type Tr } from '../lib/ui';

  let { data, tr, teamId }: { data: ManagerData; tr: Tr; teamId: string } = $props();

  let manager = $derived(data.manager);
  let reputation = $derived(data.board.reputation);
</script>

{#if !manager.found}
  <div class="screen-head"><h1 class="screen">{tr.t('shell.nav.board')}</h1></div>
  <Status text={tr.t('manager.none')} tone="warn" />
{:else}
  <div class="profile">
    <section class="panel hero staff">
      <div class="num-big"><span>{initials(manager.name)}</span></div>
      <div class="hero-main">
        <h1 class="screen">{manager.name}</h1>
        <div class="fields mid">
          <div class="fld">
            <span class="meta">{tr.t('career.you.country')}</span>
            <span class="v">{#if hasFlag(manager.nationality)}<Flag code={manager.nationality} size="md" />{/if}{countryName(tr, manager.nationality)}</span>
          </div>
          <div class="fld"><span class="meta">{tr.t('drivers.age')}</span><span class="v num">{manager.age}</span></div>
          <div class="fld"><span class="meta">{tr.t('staff.role')}</span><span class="v">{tr.t('shell.role')}</span></div>
          <div class="fld"><span class="meta">{tr.t('shell.col.team')}</span><span class="v">{teamLabel(teamId)}</span></div>
          {#if manager.since}
            <div class="fld"><span class="meta">{tr.t('manager.since')}</span><span class="v num">{formatDate(manager.since, tr.lang)}</span></div>
          {/if}
        </div>
      </div>
      <div class="hero-side">
        <div class="tools"><a class="btn" href="#/zarzad">{@html icon(ICON.back, 17)}<span>{tr.t('shell.nav.board')}</span></a></div>
      </div>
    </section>

    <div class="prof-grid s">
      <section class="panel">
        <header><h2>{tr.t('driver.attributes')}</h2></header>
        <div class="body">
          {#if manager.attributes.length > 0}
            <div class="attrs">
              {#each manager.attributes as attribute (attribute.key)}
                <AttrRow label={tr.t(`attr.${attribute.key}`)} low={attribute.low} high={attribute.high} />
              {/each}
            </div>
          {:else}
            <Status text={tr.t('driver.noRatings')} />
          {/if}
        </div>
      </section>
      {#if reputation}
        <section class="panel">
          <header><h2>{tr.t('board.reputation')}</h2></header>
          <div class="body">
            <div class="fields">
              <div class="fld"><span class="meta">{tr.t('board.points')}</span><span class="v num">{reputation.points}</span></div>
              <div class="fld"><span class="meta">{tr.t('board.mood')}</span><span class="v">{tr.tMsg(reputation.band)}</span></div>
            </div>
          </div>
        </section>
      {/if}
    </div>
  </div>
{/if}
