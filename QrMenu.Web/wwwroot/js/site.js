document.addEventListener('DOMContentLoaded', () => {
  // Auto-dismiss toasts
  document.querySelectorAll('.toast').forEach(t => {
    setTimeout(() => { t.style.transition='opacity 0.4s'; t.style.opacity='0'; setTimeout(()=>t.remove(),400); }, 3500);
  });
  // Menu tab active tracking
  const tabs = document.querySelectorAll('.menu-tab[data-target]');
  const cats = document.querySelectorAll('.menu-category');
  if (tabs.length && cats.length) {
    const obs = new IntersectionObserver(entries => {
      entries.forEach(e => { if(e.isIntersecting) { const id=e.target.id; tabs.forEach(t=>t.classList.toggle('active',t.dataset.target===id)); }});
    }, { rootMargin:'-30% 0px -60% 0px' });
    cats.forEach(c=>obs.observe(c));
    tabs.forEach(t=>t.addEventListener('click',e=>{e.preventDefault();document.getElementById(t.dataset.target)?.scrollIntoView({behavior:'smooth'});}));
  }
  // Confirm dialogs
  document.querySelectorAll('[data-confirm]').forEach(el => {
    el.addEventListener('click', e => { if(!confirm(el.dataset.confirm)) e.preventDefault(); });
  });
});
