<script lang="ts">
  import type { BridgeCommandName, PoolItemView } from '../lib/api/types.generated';
  import AttrRow from '../lib/components/AttrRow.svelte';
  import Confirmation from '../lib/components/Confirmation.svelte';
  import PersonCell from '../lib/components/PersonCell.svelte';
  import Status from '../lib/components/Status.svelte';
  import { formatMoney } from '../lib/money.mjs';
  import { bandOf, bandText, DRIVER_ATTRS, sortRows } from '../lib/people.mjs';
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

  type Ask = { kind: 'focus'; handle: string | null } | { kind: 'fund'; handle: string; programme: 'CheapSlow' | 'ExpensiveFast' };

  let picked = $state('');
  let asking = $state<Ask | null>(null);
  let sort = $state<'name' | 'age'>('name');

  let items = $derived(sortRows(data.pool.items, (item: PoolItemView) => (sort === 'age' ? item.age : `${item.familyName} ${item.givenName}`)));
  let current = $derived(data.pool.items.find((item) => item.handle === picked) ?? null);
  let focused = $derived(data.pool.items.find((item) => item.handle === data.pool.focusHandle) ?? null);
  const name = (item: PoolItemView) => `${item.givenName} ${item.familyName}`;
  const same = (a: Ask | null, b: Ask) => a !== null && JSON.stringify(a) === JSON.stringify(b);

  function askText(ask: Ask) {
    if (ask.kind === 'focus') {
      const target = ask.handle ? data.pool.items.find((item) => item.handle === ask.handle) : null;
      return target ? tr.t('academy.ask.person', { name: name(target) }) : tr.t('academy.ask.pool');
    }
    const target = data.pool.items.find((item) => item.handle === ask.handle);
    const cost = formatMoney(ask.programme === 'CheapSlow' ? data.pool.cheapProgrammeCostCents : data.pool.fastProgrammeCostCents, tr.lang);
    return tr.t(ask.programme === 'CheapSlow' ? 'academy.ask.cheap' : 'academy.ask.fast', { name: target ? name(target) : '', cost });
  }

  async function run() {
    const ask = asking;
    asking = null;
    if (!ask) return;
    if (ask.kind === 'focus') await act('assignScoutFocus', { personHandle: ask.handle });
    else await act('fundJunior', { personHandle: ask.handle, programme: ask.programme });
  }

  const poolFocus: Ask = { kind: 'focus', handle: null };
</script>

<div class="screen-head">
  <h1 class="screen">{tr.t('shell.nav.academy')}</h1>
  <div class="fields">
    <div class="fld"><span class="meta">{tr.t('academy.pool')}</span><span class="v num">{data.pool.items.length}</span></div>
    <div class="fld">
      <span class="meta">{tr.t('academy.focus')}</span>
      <span class="v">{#if data.pool.focus === 'Person' && focused}{name(focused)}{:else if data.pool.focus === 'Pool'}{tr.t('scouting.focus.pool')}{:else}—{/if}</span>
    </div>
  </div>
  <div class="tools">
    {#if same(asking, poolFocus)}
      <span></span>
    {:else}
      <button class="btn" type="button" disabled={busy || data.pool.focus === 'Pool'} onclick={() => (asking = poolFocus)}>{tr.t('academy.watchAll')}</button>
    {/if}
  </div>
</div>

{#if same(asking, poolFocus) && asking}
  <Confirmation {tr} {busy} ask={askText(asking)} onCancel={() => (asking = null)} onConfirm={run} />
{/if}

{#if data.pool.items.length === 0}
  <Status text={tr.t('academy.empty')} />
{:else}
  <div class="acad">
    <section class="panel tbl market">
      <table class="table">
        <thead>
          <tr>
            <th data-sort class={sort === 'name' ? 'sorted asc' : ''} onclick={() => (sort = 'name')}><span>{tr.t('shell.col.driver')}</span></th>
            <th data-sort class={`c ${sort === 'age' ? 'sorted asc' : ''}`} onclick={() => (sort = 'age')}><span>{tr.t('drivers.age')}</span></th>
            <th>{tr.t('driver.potential')}</th>
            <th>{tr.t('academy.programme')}</th>
          </tr>
        </thead>
        <tbody>
          {#each items as item (item.handle)}
            <tr class="go-row" class:sel={item.handle === picked} onclick={() => (picked = item.handle)}>
              <td><PersonCell name={name(item)} nationality={item.nationality} /></td>
              <td class="c num">{item.age}</td>
              <td class="num">{item.potential ? bandText(item.potential.low, item.potential.high) : ''}</td>
              <td>{#if item.yourFunding}<Status text={tr.t(item.yourFunding === 'CheapSlow' ? 'pool.programme.cheapSlow' : 'pool.programme.expensiveFast')} tone="good" />{/if}</td>
            </tr>
          {/each}
        </tbody>
      </table>
    </section>

    <section class="panel acad-detail">
      {#if current}
        {@const watch = { kind: 'focus', handle: current.handle } as Ask}
        {@const cheap = { kind: 'fund', handle: current.handle, programme: 'CheapSlow' } as Ask}
        {@const fast = { kind: 'fund', handle: current.handle, programme: 'ExpensiveFast' } as Ask}
        <header><h2>{name(current)}</h2><Status text={`${current.age}`} /></header>
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
          {#if asking && (same(asking, watch) || same(asking, cheap) || same(asking, fast))}
            <Confirmation {tr} {busy} ask={askText(asking)} onCancel={() => (asking = null)} onConfirm={run} />
          {:else}
            <div class="confirm acad-actions">
              <button class="btn" type="button" disabled={busy || data.pool.focusHandle === current.handle} onclick={() => (asking = watch)}>{tr.t('academy.watch')}</button>
              <button class="btn" type="button" disabled={busy || current.yourFunding !== null} onclick={() => (asking = cheap)}>{tr.t('pool.programme.cheapSlow')} · {formatMoney(data.pool.cheapProgrammeCostCents, tr.lang)}</button>
              <button class="btn primary" type="button" disabled={busy || current.yourFunding !== null} onclick={() => (asking = fast)}>{tr.t('pool.programme.expensiveFast')} · {formatMoney(data.pool.fastProgrammeCostCents, tr.lang)}</button>
            </div>
          {/if}
        </div>
      {:else}
        <div class="body"><Status text={tr.t('academy.pick')} /></div>
      {/if}
    </section>
  </div>
{/if}
