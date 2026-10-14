/* The top bar's player block (#323). The team's name is the first line of the block; the player's own name, as the
   new-career wizard gave it, is the second line; the round badge shows the initials of that name. */

/** The player's name from the manager profile, or '' when the career has no principal yet. */
export function headName(profile) {
  if (!profile || !profile.found) return '';
  return String(profile.name ?? '').trim();
}
