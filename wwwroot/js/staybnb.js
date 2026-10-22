// Collapse / expand the Host and Admin sidebar
document.addEventListener('click', function (e) {
    var toggle = e.target.closest('[data-sb-toggle]');
    if (toggle) {
        document.body.classList.toggle('sb-collapsed');
    }
});

// Wishlist hearts: save or remove a property without reloading the page
document.addEventListener('click', function (e) {
    var heart = e.target.closest('.sb-heart');
    if (!heart) return;

    e.preventDefault();
    e.stopPropagation();

    var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    if (!tokenInput) return;

    var body = new URLSearchParams();
    body.append('propertyId', heart.getAttribute('data-property-id'));
    body.append('__RequestVerificationToken', tokenInput.value);

    fetch('/Wishlist/Toggle', { method: 'POST', body: body })
        .then(function (r) { return r.ok ? r.json() : Promise.reject(); })
        .then(function (data) {
            var icon = heart.querySelector('i');
            icon.className = 'bi ' + (data.wishlisted ? 'bi-heart-fill' : 'bi-heart');
        })
        .catch(function () { /* leave the heart as it was */ });
});