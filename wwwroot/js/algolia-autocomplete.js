(function () {
    'use strict';

    var searchbox = document.getElementById('algolia-searchbox');
    if (!searchbox) {
        return;
    }

    var appId = searchbox.getAttribute('data-algolia-app-id');
    var searchKey = searchbox.getAttribute('data-algolia-search-key');
    var indexName = searchbox.getAttribute('data-algolia-index');

    if (!appId || !searchKey || !indexName) {
        return;
    }
    if (!window.algoliasearch || !window['@algolia/autocomplete-js']) {
        return;
    }

    var client = window.algoliasearch(appId, searchKey);
    var index = client.initIndex(indexName);
    var autocomplete = window['@algolia/autocomplete-js'].autocomplete;

    function formatearPrecio(valor) {
        var numero = parseFloat(valor);
        if (isNaN(numero)) {
            return 'S/ 0.00';
        }
        return 'S/ ' + numero.toLocaleString('es-PE', {
            minimumFractionDigits: 2,
            maximumFractionDigits: 2
        });
    }

    function urlDetalle(id) {
        return window.location.origin + '/Productos/Detalle/' + id;
    }

    var templateItem = function (params) {
        var item = params.item;
        var html = params.html;
        var components = params.components;

        var stock = parseInt(item.Stock, 10);
        var estadoStock = stock > 0
            ? 'Stock: ' + stock
            : 'Sin stock';

        var imagen = item.ImagenUrl
            ? html`<img src="${item.ImagenUrl}" alt="" width="40" height="40" loading="lazy">`
            : '';

        return html`<div class="aa-ItemWrapper">
            <div class="aa-ItemContent">
                <div class="aa-ItemIcon aa-ItemIcon--alignTop">${imagen}</div>
                <div class="aa-ItemContentBody">
                    <div class="aa-ItemContentTitle">${components.Highlight({ hit: item, attribute: 'Nombre' })}</div>
                    <div class="aa-ItemContentSubtitle">${formatearPrecio(item.Precio)} · ${estadoStock}</div>
                </div>
            </div>
        </div>`;
    };

    autocomplete({
        container: searchbox,
        placeholder: 'Buscar producto...',
        debug: false,
        openOnFocus: false,
        navigator: {
            navigate: function (args) {
                window.location.href = urlDetalle(args.item.Id);
            },
            navigateNewTab: function (args) {
                window.open(urlDetalle(args.item.Id), '_blank');
            },
            navigateNewWindow: function (args) {
                window.open(urlDetalle(args.item.Id));
            }
        },
        onSelect: function (args) {
            window.location.href = urlDetalle(args.item.Id);
        },
        getSources: function () {
            return [
                {
                    sourceId: 'productos',
                    getItems: async function (params) {
                        var query = (params.query || '').trim();
                        if (query.length < 2) {
                            return [];
                        }

                        var res = await index.search(query, {
                            hitsPerPage: 6,
                            attributesToRetrieve: ['Id', 'Nombre', 'Precio', 'ImagenUrl', 'Stock', 'CategoriaId'],
                            highlightPreTag: '__aa-highlight__',
                            highlightPostTag: '__/aa-highlight__'
                        });
                        return res.hits || [];
                    },
                    templates: {
                        noResults: 'No se encontraron productos.',
                        item: templateItem
                    }
                }
            ];
        }
    });
})();