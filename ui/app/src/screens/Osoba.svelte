<script lang="ts">
  import { teamLabel } from '../lib/career.mjs';
  import AttrRow from '../lib/components/AttrRow.svelte';
  import Flag from '../lib/components/Flag.svelte';
  import Status from '../lib/components/Status.svelte';
  import { formatDate } from '../lib/date.mjs';
  import { hasFlag } from '../lib/flags.mjs';
  import { endsThisSeason } from '../lib/people.mjs';
  import type { StaffData } from '../lib/screens';
  import { countryName, icon, ICON, initials, type Tr } from '../lib/ui';

  let { data, tr, id }: { data: StaffData; tr: Tr; id: string } = $props();

  let person = $derived(data.staff.people.find((item) => item.personId === id) ?? null);
  let driver = $derived(person?.driverId ? (data.drivers.own.find((item) => item.personId === person.driverId) ?? null) : null);
</script>

{#if !person}
  <div class="screen-head"><h1 class="screen">{tr.t('shell.nav.staff')}</h1></div>
  <Status text={tr.t('staff.unknown')} tone="warn" />
{:else}
  <div class="profile">
    <section class="panel hero staff">
      <div class="num-big"><span>{initials(person.name)}</span></div>
      <div class="hero-main">
        <h1 class="screen">{person.name}</h1>
        <div class="fields mid">
          <div class="fld">
            <span class="meta">{tr.t('career.you.country')}</span>
            <span class="v">{#if hasFlag(person.nationality)}<Flag code={person.nationality} size="md" />{/if}{countryName(tr, person.nationality)}</span>
          </div>
          <div class="fld"><span class="meta">{tr.t('drivers.age')}</span><span class="v num">{person.age}</span></div>
          <div class="fld"><span class="meta">{tr.t('staff.role')}</span><span class="v">{tr.t(`staff.role.${person.role}`)}</span></div>
          <div class="fld"><span class="meta">{tr.t('shell.col.team')}</span><span class="v">{teamLabel(person.organizationId)}</span></div>
        </div>
      </div>
      <div class="hero-side">
        <div class="tools">
          <a class="btn" href="#/personel">{@html icon(ICON.back, 17)}<span>{tr.t('shell.nav.staff')}</span></a>
        </div>
      </div>
    </section>

    <div class="prof-grid s">
      <section class="panel">
        <header><h2>{tr.t('driver.attributes')}</h2></header>
        <div class="body">
          {#if person.attributes}
            <div class="attrs">
              {#each person.attributes as attribute (attribute.key)}
                <AttrRow label={tr.t(`attr.${attribute.key}`)} low={attribute.low} high={attribute.high} />
              {/each}
            </div>
          {:else}
            <Status text={tr.t('driver.noRatings')} />
          {/if}
        </div>
      </section>
      <div class="col">
        {#if person.ownTeam}
          <section class="panel">
            <header><h2>{tr.t('driver.contract')}</h2></header>
            <div class="body">
              <div class="fields">
                <div class="fld"><span class="meta">{tr.t('driver.contract.until')}</span><span class="v num" class:bad={endsThisSeason(person.contractEnd, data.today)}>{person.contractEnd ? formatDate(person.contractEnd, tr.lang) : '—'}</span></div>
              </div>
            </div>
          </section>
        {/if}
        {#if driver}
          <section class="panel">
            <header><h2>{tr.t('staff.driver')}</h2></header>
            <div class="body">
              <div class="fields">
                <div class="fld"><span class="meta">{tr.t('shell.col.driver')}</span><a class="v plain" href={`#/kierowca/${encodeURIComponent(driver.personId)}`}>{driver.name}</a></div>
                {#if person.relationship !== null}
                  <div class="fld"><span class="meta">{tr.t('staff.relationship')}</span><span class="v num">{person.relationship}</span></div>
                {/if}
              </div>
            </div>
          </section>
        {/if}
      </div>
    </div>
  </div>
{/if}
