<script lang="ts">
  import { onMount } from 'svelte';

  type Item = { value: string; label: string; disabled?: boolean };

  let {
    group,
    items,
    fill = false,
    value = $bindable(),
  }: {
    group: string;
    items: Item[];
    fill?: boolean;
    value: string;
  } = $props();

  let root: HTMLDivElement | undefined = $state();

  function place() {
    if (!root) return;
    const current = root.querySelector('button.on') as HTMLElement | null;
    const marker = root.querySelector('.ind') as HTMLElement | null;
    if (!current || !marker) return;
    /* A long set may wrap onto a second row (the start form at a narrow width), so the marker follows both axes. */
    marker.style.width = `${current.offsetWidth}px`;
    marker.style.height = `${current.offsetHeight}px`;
    marker.style.top = '0';
    marker.style.bottom = 'auto';
    marker.style.transform = `translate(${current.offsetLeft}px, ${current.offsetTop}px)`;
    root.classList.add('ready');
  }

  onMount(place);
  $effect(() => {
    value;
    items;
    queueMicrotask(place);
  });
</script>

<div class="tabs" class:fill role="tablist" data-group={group} bind:this={root}>
  {#each items as item (item.value)}
    <button
      role="tab"
      type="button"
      data-v={item.value}
      aria-selected={item.value === value}
      class:on={item.value === value}
      disabled={item.disabled}
      onclick={() => (value = item.value)}
    >{item.label}</button>
  {/each}
  <i class="ind" aria-hidden="true"></i>
</div>
