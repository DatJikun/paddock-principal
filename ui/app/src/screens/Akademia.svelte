<script lang="ts">
  import type { BridgeCommandName, PoolItemView, ProgrammeView } from '../lib/api/types.generated';
  import AttrRow from '../lib/components/AttrRow.svelte';
  import Confirmation from '../lib/components/Confirmation.svelte';
  import PersonCell from '../lib/components/PersonCell.svelte';
  import Status from '../lib/components/Status.svelte';
  import { formatMoney } from '../lib/money.mjs';
  import { bandOf, bandText, DRIVER_ATTRS, sortRows } from '../lib/people.mjs';
  import { driverHref, formatAge } from '../lib/person.mjs';
  import type { AcademyData } from '../lib/screens';
  import type { Tr } from '../lib/ui';

  let {
    data,
    tr,
    busy,
    act,
  }: {
    data: AcademyData;
    tr: Tr;
    busy: boolean;
    act: (name: BridgeCommandName, args: Record<string, unknown>) => Promise<boolean>;
  } = $props();

  type Ask =
    | { kind: 'focus'; handle: string | null }
    | { kind: 'recruit'; handle: string }
    | { kind: 'release'; handle: string }
    | { kind: 'fund'; handle: string; programme: ProgrammeView['programme'] };

  let picked = $state('');
  let asking = $state<Ask | null>(null);
  let sort = $state<'name' | 'age'>('name');

  let pool = $derived(data.pool);
  let juniors = $derived(pool.items.filter((item) => item.inYourAcademy));
  let open = $derived(Math.max(0, pool.academySlots - juniors.length));
  let list = $derived(sortRows(pool.items.filter((item) => !item.inYourAcademy), (item: PoolItemView) => (sort === 'age' ? item.age : `${item.familyName} ${item.givenName}`)));
  let current = $derived(pool.items.find((item) => item.handle === picked) ?? juniors[0] ?? list[0] ?? null);
  let focused = $derived(pool.items.find((item) => item.handle === pool.focusHandle) ?? null);
  const name = (item: PoolItemView) => `${item.givenName} ${item.familyName}`;
  const find = (handle: string | null) => pool.items.find((item) => item.handle === handle) ?? null;
  const programmeName = (programme: string) => tr.t(programme === 'CheapSlow' ? 'pool.programme.cheapSlow' : 'pool.programme.expensiveFast');
  const same = (a: Ask | null, b: Ask) => a !== null && JSON.stringify(a) === JSON.stringify(b);
  const speed = (percent: number) => `${percent}%`;

  function askText(ask: Ask) {
    if (ask.kind === 'focus') {
      const target = find(ask.handle);
      return target ? tr.t('academy.ask.person', { name: name(target) }) : tr.t('academy.ask.pool');
    }
    const target = find(ask.handle);
    const who = target ? name(target) : '';
    if (ask.kind === 'recruit') return tr.t('academy.ask.recruit', { name: who, slots: String(pool.academySlots) });
    if (ask.kind === 'release') return tr.t(target?.yourFunding ? 'academy.ask.releaseFunded' : 'academy.ask.release', { name: who });
    const programme = pool.programmes.find((item) => item.programme === ask.programme);
    return tr.t('academy.ask.programme', {
      name: who,
      programme: programmeName(ask.programme),
      cost: formatMoney(programme?.costCents ?? 0, tr.lang),
      speed: speed(programme?.speedPercent ?? 100),
    });
  }

  async function run() {
    const ask = asking;
    asking = null;
    if (!ask) return;
    if (ask.kind === 'focus') await act('assignScoutFocus', { personHandle: ask.handle });
    else if (ask.kind === 'recruit') await act('recruitJunior', { personHandle: ask.handle });
    else if (ask.kind === 'release') await act('releaseJunior', { personHandle: ask.handle });
    else await act('fundJunior', { personHandle: ask.handle, programme: ask.programme });
  }

  const poolFocus: Ask = { kind: 'focus', handle: null };
</script>

<div class="academy">
  <div class="screen-head">
    <h1 class="screen">{tr.t('shell.nav.academy')}</h1>
    <div class="fields">
      <div class="fld"><span class="meta">{tr.t('academy.slots')}</span><span class="v num">{juniors.length} / {pool.academySlots}</span></div>
      <div class="fld">
        <span class="meta">{tr.t('academy.focus')}</span>
        <span class="v">{#if pool.focus === 'Person' && focused}{name(focused)}{:else if pool.focus === 'Pool'}{tr.t('scouting.focus.pool')}{:else}—{/if}</span>
      </div>
    </div>
    <div class="tools">
      {#if same(asking, poolFocus)}
        <span></span>
      {:else}
        <button class="btn" type="button" disabled={busy || pool.focus === 'Pool'} onclick={() => (asking = poolFocus)}>{tr.t('academy.watchAll')}</button>
      {/if}
    </div>
  </div>

  {#if same(asking, poolFocus) && asking}
    <Confirmation {tr} {busy} ask={askText(asking)} onCancel={() => (asking = null)} onConfirm={run} />
  {/if}

  <section class="panel yours">
    <header><h2>{tr.t('academy.yours')}</h2><span class="meta num">{juniors.length} / {pool.academySlots}</span></header>
    <div class="slot-row">
      {#each juniors as junior (junior.handle)}
        <div
          class="slot-tile"
          class:sel={current?.handle === junior.handle}
          role="button"
          tabindex="0"
          onclick={() => (picked = junior.handle)}
          onkeydown={(event) => (event.key === 'Enter' || event.key === ' ') && event.target === event.currentTarget && (picked = junior.handle)}
        >
          <PersonCell name={name(junior)} href={driverHref(junior.handle)} nationality={junior.nationality} />
          <div class="fields">
            <div class="fld"><span class="meta">{tr.t('drivers.age')}</span><span class="v num">{formatAge(junior.age)}</span></div>
            <div class="fld"><span class="meta">{tr.t('driver.potential')}</span><span class="v num">{junior.potential ? bandText(junior.potential.low, junior.potential.high) : '—'}</span></div>
            <div class="fld"><span class="meta">{tr.t('academy.seasonsLeft')}</span><span class="v num" class:bad={junior.seasonsLeft === 0}>{junior.seasonsLeft ?? '—'}</span></div>
          </div>
          {#if junior.yourFunding}<Status text={programmeName(junior.yourFunding)} tone="good" />{:else}<Status text={tr.t('academy.programme.none')} />{/if}
        </div>
      {/each}
      {#each Array.from({ length: open }, (_, index) => index) as index (index)}
        <div class="slot-tile empty"><span class="meta">{tr.t('academy.free')}</span></div>
      {/each}
    </div>
  </section>

  {#if list.length === 0 && juniors.length === 0}
    <Status text={tr.t('academy.empty')} />
  {:else}
    <div class="acad">
      <section class="panel tbl market">
        <header><h2>{tr.t('academy.pool')}</h2><span class="meta num">{list.length}</span></header>
        <table class="table">
          <thead>
            <tr>
              <th data-sort class={sort === 'name' ? 'sorted asc' : ''} onclick={() => (sort = 'name')}><span>{tr.t('shell.col.driver')}</span></th>
              <th data-sort class={`c ${sort === 'age' ? 'sorted asc' : ''}`} onclick={() => (sort = 'age')}><span>{tr.t('drivers.age')}</span></th>
              <th class="c">{tr.t('driver.potential')}</th>
            </tr>
          </thead>
          <tbody>
            {#each list as item (item.handle)}
              <tr class="go-row" class:sel={item.handle === current?.handle} onclick={() => (picked = item.handle)}>
                <td><PersonCell name={name(item)} href={driverHref(item.handle)} nationality={item.nationality} /></td>
                <td class="c num">{formatAge(item.age)}</td>
                <td class="c num">{item.potential ? bandText(item.potential.low, item.potential.high) : '—'}</td>
              </tr>
            {/each}
          </tbody>
        </table>
      </section>

      <section class="panel acad-detail">
        {#if current}
          {@const watch = { kind: 'focus', handle: current.handle } as Ask}
          {@const recruit = { kind: 'recruit', handle: current.handle } as Ask}
          {@const release = { kind: 'release', handle: current.handle } as Ask}
          <header><h2><a class="plain" href={driverHref(current.handle)}>{name(current)}</a></h2><Status text={formatAge(current.age)} /></header>
          <div class="body">
            {#if current.attributes.length > 0}
              <div class="attrs">
                {#each DRIVER_ATTRS as key (key)}
                  {@const band = bandOf(current.attributes, key)}
                  {#if band}<AttrRow label={tr.t(`attr.${key}`)} low={band.low} high={band.high} />{/if}
                {/each}
              </div>
            {:else}
              <Status text={tr.t('scouting.band.unknown')} />
            {/if}

            {#if current.inYourAcademy}
              <div class="programmes">
                <div class="fields">
                  <div class="fld"><span class="meta">{tr.t('academy.programme')}</span><span class="v">{current.yourFunding ? programmeName(current.yourFunding) : tr.t('academy.programme.none')}</span></div>
                  <div class="fld"><span class="meta">{tr.t('academy.speed')}</span><span class="v num">{speed(pool.baseSpeedPercent)}</span></div>
                </div>
                {#each pool.programmes as programme (programme.programme)}
                  {@const fund = { kind: 'fund', handle: current.handle, programme: programme.programme } as Ask}
                  <button type="button" class="programme" disabled={busy || current.yourFunding !== null} onclick={() => (asking = fund)}>
                    <b>{programmeName(programme.programme)}</b>
                    <span class="fields">
                      <span class="fld"><span class="meta">{tr.t('academy.speed')}</span><span class="v num">{speed(programme.speedPercent)}</span></span>
                      <span class="fld"><span class="meta">{tr.t('academy.cost')}</span><span class="v num">{formatMoney(programme.costCents, tr.lang)}</span></span>
                    </span>
                  </button>
                {/each}
              </div>
            {/if}

            {#if asking && !same(asking, poolFocus) && asking.handle === current.handle}
              <Confirmation {tr} {busy} ask={askText(asking)} onCancel={() => (asking = null)} onConfirm={run} />
            {:else}
              <div class="confirm acad-actions">
                <button class="btn" type="button" disabled={busy || pool.focusHandle === current.handle} onclick={() => (asking = watch)}>{tr.t('academy.watch')}</button>
                {#if current.inYourAcademy}
                  <button class="btn" type="button" disabled={busy} onclick={() => (asking = release)}>{tr.t('academy.release')}</button>
                {:else if open === 0}
                  <Status text={tr.t('academy.full')} tone="warn" />
                {:else}
                  <button class="btn primary" type="button" disabled={busy} onclick={() => (asking = recruit)}>{tr.t('academy.recruit')}</button>
                {/if}
              </div>
            {/if}
          </div>
        {/if}
      </section>
    </div>
  {/if}
</div>
