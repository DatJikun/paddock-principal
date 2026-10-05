<script lang="ts">
  type Step = { value: string; label: string };

  let {
    items,
    current,
    reachable,
    onGo,
  }: {
    items: Step[];
    current: string;
    /** The furthest step the form has allowed so far; earlier ones can be revisited. */
    reachable: number;
    onGo: (value: string) => void;
  } = $props();

  let at = $derived(items.findIndex((item) => item.value === current));
</script>

<ol class="steps">
  {#each items as item, index (item.value)}
    <li class:on={index === at} class:done={index < at}>
      <button type="button" disabled={index > reachable || index === at} aria-current={index === at ? 'step' : undefined} onclick={() => onGo(item.value)}>
        <span class="no num">{index + 1}</span>
        <span class="lb">{item.label}</span>
      </button>
    </li>
  {/each}
</ol>
