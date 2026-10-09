/**
 * The route a headline of the paper opens (#324). The host decides where a piece of news leads and names it by kind;
 * the page only turns that kind into the address of the screen that shows it.
 */
export function headlineHref(link) {
  const id = encodeURIComponent(link?.id ?? '');
  switch (link?.kind) {
    case 'race':
      return `#/wyscig/${id}`;
    case 'driver':
      return `#/kierowca/${id}`;
    case 'person':
      return `#/osoba/${id}`;
    case 'inbox':
      return id ? `#/skrzynka/${id}` : '#/skrzynka';
    default:
      return '#/klasyfikacje';
  }
}
