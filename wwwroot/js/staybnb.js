// Collapse / expand the Host and Admin sidebar
document.addEventListener('click', function (e) {
    var toggle = e.target.closest('[data-sb-toggle]');
    if (toggle) {
        document.body.classList.toggle('sb-collapsed');
    }
});