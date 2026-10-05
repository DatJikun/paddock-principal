<script lang="ts">
  type Field = { label: string; value: string; numeric?: boolean };
  let { items }: { items: Field[] } = $props();
</script>

<div class="fields">
  {#each items as field}
    <div class="fld">
      <span class="meta">{field.label}</span>
      <span class="v" class:num={field.numeric}>{field.value}</span>
    </div>
  {/each}
</div>
