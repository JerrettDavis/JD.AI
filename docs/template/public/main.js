export default {
  iconLinks: [
    {
      icon: 'github',
      href: 'https://github.com/JerrettDavis/JD.AI',
      title: 'GitHub'
    },
    {
      icon: 'box-seam',
      href: 'https://www.nuget.org/packages/JD.AI',
      title: 'NuGet'
    }
  ],
  start: () => {
    // Inject Google Fonts
    const link = document.createElement('link');
    link.rel = 'stylesheet';
    link.href = 'https://fonts.googleapis.com/css2?family=Instrument+Sans:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500;600&display=swap';
    document.head.appendChild(link);

    // Animate elements into view on scroll
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('jdai-visible');
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.08, rootMargin: '0px 0px -40px 0px' }
    );

    document.querySelectorAll(
      'article h2, article h3, article table, article img, article pre, article .alert'
    ).forEach((el) => {
      el.classList.add('jdai-reveal');
      observer.observe(el);
    });
  }
}
