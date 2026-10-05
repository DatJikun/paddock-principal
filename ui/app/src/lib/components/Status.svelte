<script lang="ts">
  let { text, tone = '', solid = false }: { text: string; tone?: string; solid?: boolean } = $props();
</script>

<span class="st {tone}" class:solid>{text}</span>
