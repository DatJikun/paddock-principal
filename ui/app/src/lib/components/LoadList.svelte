<script lang="ts">
  import type { SaveListItem } from '../api/types.generated';
  import { saveLabel, teamLabel } from '../career.mjs';
  import { formatDate } from '../date.mjs';
  import type { Tr } from '../ui';

  let {
    saves,
    tr,
    selected,
    onSelect,
  }: {
    saves: SaveListItem[];
    tr: Tr;
    selected: string;
    onSelect: (name: string) => void;
  } = $props();

  /* The file's write time in UTC, shown as the player's local date. */
  function writtenOn(iso: string) {
    const stamp = new Date(iso);
    if (Number.isNaN(stamp.getTime())) return '';
    const local = `${stamp.getFullYear()}-${String(stamp.getMonth() + 1).padStart(2, '0')}-${String(stamp.getDate()).padStart(2, '0')}`;
    return formatDate(local, tr.lang);
  }
</script>

<div class="panel tbl loads">
  <table class="table fit">
    <thead>
      <tr>
        <th>{tr.t('shell.save.name')}</th>
        <th>{tr.t('load.col.career')}</th>
        <th>{tr.t('shell.start.team')}</th>
        <th class="r">{tr.t('shell.col.date')}</th>
        <th class="r">{tr.t('load.col.saved')}</th>
      </tr>
    </thead>
    <tbody>
      {#each saves as save (save.name)}
        <tr class="go-row" class:sel={selected === save.name} onclick={() => onSelect(save.name)}>
          <td><b>{saveLabel(save.name)}</b></td>
          <td>{save.careerName}</td>
          <td>{teamLabel(save.teamId)}</td>
          <td class="r num">{formatDate(save.date, tr.lang)}</td>
          <td class="r num muted">{writtenOn(save.savedAt)}</td>
        </tr>
      {/each}
    </tbody>
  </table>
</div>
