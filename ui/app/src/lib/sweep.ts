/** Colour sweep between screens, ported from the prototype. Inbox mail switches stay instant. */
export async function sweep(
  content: HTMLElement,
  view: HTMLElement,
  wipe: HTMLElement,
  render: () => Promise<void> | void,
): Promise<void> {
  const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
  if (reduce) {
    await render();
    view.animate([{ opacity: 0 }, { opacity: 1 }], { duration: 220, easing: 'ease-out' });
    return;
  }

  const bounds = view.getBoundingClientRect();
  const contentBounds = content.getBoundingClientRect();
  const width = view.clientWidth;
  const height = view.clientHeight;
  const x0 = bounds.left - contentBounds.left;
  const y0 = bounds.top - contentBounds.top;
  const old = document.createElement('div');
  old.className = 'view-old';
  Object.assign(old.style, { left: `${x0}px`, top: `${y0}px`, width: `${width}px`, height: `${height}px` });
  const inner = document.createElement('div');
  inner.className = 'vo-in';
  const style = getComputedStyle(view);
  Object.assign(inner.style, {
    padding: style.padding,
    width: `${width}px`,
    height: `${height}px`,
    transform: `translateY(${-view.scrollTop}px)`,
  });
  inner.innerHTML = view.innerHTML;
  inner.querySelectorAll('[id]').forEach((node) => node.removeAttribute('id'));
  old.appendChild(inner);
  content.appendChild(old);
  content.classList.add('wiping');
  await render();

  const slant = Math.round(height * 0.2);
  const band = 90;
  const far = width + slant + band + 40;
  const duration = 820;
  const easing = 'cubic-bezier(.7,0,.2,1)';
  const oldPoly = (x: number) => `polygon(${x + slant}px 0px, ${far}px 0px, ${far}px ${height}px, ${x}px ${height}px)`;
  const newPoly = (x: number) => `polygon(-40px 0px, ${x - band + slant}px 0px, ${x - band}px ${height}px, -40px ${height}px)`;
  Object.assign(wipe.style, {
    display: 'block',
    left: `${x0}px`,
    top: `${y0}px`,
    height: `${height}px`,
    width: `${band + slant}px`,
  });
  wipe.querySelectorAll('i').forEach((stripe, index) => {
    const item = stripe as HTMLElement;
    item.style.left = `${index * 30}px`;
    item.style.transformOrigin = '0 100%';
    item.style.transform = `skewX(${(-Math.atan(slant / height) * 180) / Math.PI}deg)`;
  });
  const options: KeyframeAnimationOptions = { duration, easing, fill: 'forwards' };
  const leaving = old.animate([{ clipPath: oldPoly(-slant) }, { clipPath: oldPoly(width + band) }], options);
  view.animate([{ clipPath: newPoly(-slant) }, { clipPath: newPoly(width + band) }], options);
  wipe.animate([{ transform: `translateX(${-slant - band}px)` }, { transform: `translateX(${width}px)` }], options);

  await new Promise<void>((resolve) => {
    let done = false;
    const finish = () => {
      if (done) return;
      done = true;
      old.remove();
      wipe.style.display = 'none';
      view.getAnimations().forEach((animation) => animation.cancel());
      wipe.getAnimations().forEach((animation) => animation.cancel());
      view.style.clipPath = '';
      content.classList.remove('wiping');
      resolve();
    };
    leaving.onfinish = finish;
    setTimeout(finish, duration + 250);
  });
}
