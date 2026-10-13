document.addEventListener('scroll', () => {
    document.body.classList.toggle('scrolled', window.scrollY > 12);
}, { passive: true });
