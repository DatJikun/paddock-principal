/** Smoke in the wind tunnel, ported from ui/prototype/js/app.js. A static frame is enough when motion is reduced. */
export function startSmoke(canvas: HTMLCanvasElement): () => void {
  const context = canvas.getContext('2d');
  if (!context) return () => {};
  const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
  let width = 0;
  let height = 0;
  let particles: { x: number; y: number; c: number; s: number }[] = [];
  let t = 0;
  let last = 0;
  let running = true;
  let frameId = 0;

  const count = () => Math.round((window.innerWidth * window.innerHeight) / 3900);
  const spawn = (x?: number) => ({
    x: x ?? -20 - Math.random() * 200,
    y: Math.random() * height,
    c: Math.random() < 0.55 ? 0 : 1,
    s: 0.35 + Math.random() * 0.3,
  });

  function size() {
    width = canvas.width = window.innerWidth;
    height = canvas.height = window.innerHeight;
    particles = Array.from({ length: count() }, () => spawn(Math.random() * width));
  }

  function angle(x: number, y: number) {
    const nx = x / width;
    const ny = y / height;
    const a = Math.sin(nx * 2.4 + t * 0.00018) * 0.28 + Math.sin(ny * 4.2 - t * 0.00012 + nx * 1.6) * 0.18;
    const dy = ny - 0.5 - Math.sin(t * 0.0001) * 0.05;
    const dx = nx - 0.64;
    const bump = Math.exp(-(dx * dx) * 30) * (dy > 0 ? 0.5 : -0.5) * Math.exp(-dy * dy * 8);
    return a + bump;
  }

  function frame(now: number) {
    if (!running || !context) return;
    frameId = requestAnimationFrame(frame);
    if (now - last < 33) return;
    last = now;
    t += 33;
    const style = getComputedStyle(document.body);
    const colors = [style.getPropertyValue('--t1').trim(), style.getPropertyValue('--smoke2').trim()];
    context.globalCompositeOperation = 'destination-out';
    context.fillStyle = 'rgba(0,0,0,.042)';
    context.fillRect(0, 0, width, height);
    context.globalCompositeOperation = 'source-over';
    context.lineWidth = 1.25;
    context.globalAlpha = 0.42;
    for (let k = 0; k < 2; k++) {
      context.strokeStyle = colors[k] ?? '#000';
      context.beginPath();
      for (const particle of particles) {
        if (particle.c !== k) continue;
        const a = angle(particle.x, particle.y);
        const nx = particle.x + Math.cos(a) * particle.s * 1.4;
        const ny = particle.y + Math.sin(a) * particle.s;
        context.moveTo(particle.x, particle.y);
        context.lineTo(nx, ny);
        particle.x = nx;
        particle.y = ny;
        if (particle.x > width + 10 || particle.y < -10 || particle.y > height + 10) Object.assign(particle, spawn());
      }
      context.stroke();
    }
    context.globalAlpha = 1;
  }

  size();
  const onResize = () => size();
  window.addEventListener('resize', onResize);
  if (reduce) {
    for (let i = 0; i < 400; i++) frame(i * 40);
    running = false;
  } else {
    frameId = requestAnimationFrame(frame);
  }

  return () => {
    running = false;
    cancelAnimationFrame(frameId);
    window.removeEventListener('resize', onResize);
  };
}
