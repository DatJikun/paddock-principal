// Docs page behaviour: theme switch (light / dark / system) and the active section in the table of contents.
(function () {
  var root = document.documentElement;
  var button = document.querySelector('.theme');
  var order = ['light', 'dark', ''];
  if (button) {
    button.addEventListener('click', function () {
      var now = root.dataset.theme || '';
      var next = order[(order.indexOf(now) + 1) % order.length];
      if (next) root.dataset.theme = next; else delete root.dataset.theme;
      try { if (next) localStorage.setItem('pp-docs-theme', next); else localStorage.removeItem('pp-docs-theme'); } catch (e) { /* storage blocked */ }
      button.title = next === 'light' ? 'Motyw: jasny' : next === 'dark' ? 'Motyw: ciemny' : 'Motyw: systemowy';
    });
  }

  var toc = document.querySelector('.toc');
  if (!toc) return;
  if (window.matchMedia('(max-width: 960px)').matches) {
    var details = toc.querySelector('details');
    if (details) details.open = false;
  }
  var links = Array.prototype.slice.call(toc.querySelectorAll('a[href^="#"]'));
  var byId = {};
  links.forEach(function (a) { byId[decodeURIComponent(a.getAttribute('href').slice(1))] = a; });
  var targets = links.map(function (a) { return document.getElementById(decodeURIComponent(a.getAttribute('href').slice(1))); }).filter(Boolean);
  var current = null;
  function update() {
    var y = window.scrollY + 100;
    var active = null;
    for (var i = 0; i < targets.length; i++) {
      if (targets[i].offsetTop <= y) active = targets[i]; else break;
    }
    var link = active ? byId[active.id] : null;
    if (link === current) return;
    if (current) current.classList.remove('on');
    current = link;
    if (current) {
      current.classList.add('on');
      var box = toc.getBoundingClientRect();
      var r = current.getBoundingClientRect();
      if (r.top < box.top + 40 || r.bottom > box.bottom - 40) toc.scrollTop += r.top - box.top - box.height / 2;
    }
  }
  var pending = false;
  window.addEventListener('scroll', function () {
    if (pending) return;
    pending = true;
    requestAnimationFrame(function () { pending = false; update(); });
  }, { passive: true });
  window.addEventListener('hashchange', function () { setTimeout(update, 0); });
  window.addEventListener('load', function () { setTimeout(update, 0); });
  update();
})();
