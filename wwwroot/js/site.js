// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

if (window.jQuery) {
    (function ($) {
        var RECORDADOS_KEY = 'ProductosRecordados';

        function leerRecordados() {
            try {
                var raw = localStorage.getItem(RECORDADOS_KEY);
                return raw ? (JSON.parse(raw) || []) : [];
            } catch (e) {
                return [];
            }
        }

        function escribirRecordados(ids) {
            localStorage.setItem(RECORDADOS_KEY, JSON.stringify(ids));
        }

        function actualizarContador(ids) {
            var badge = document.getElementById('contador-recordados');
            if (badge) {
                var count = ids.length;
                badge.textContent = count;
                badge.style.display = count > 0 ? 'inline-block' : 'none';
            }
        }

        window.recordarProductoEnCliente = function (id) {
            var ids = leerRecordados();
            if (ids.indexOf(id) === -1) {
                ids.push(id);
                escribirRecordados(ids);
            }
            actualizarContador(ids);
        };

        window.quitarProductoEnCliente = function (id) {
            var ids = leerRecordados().filter(function (x) { return x !== id; });
            escribirRecordados(ids);
            actualizarContador(ids);
        };

        window.mostrarContadorProductosRecordados = function () {
            actualizarContador(leerRecordados());
        };

        window.comprarProducto = function (id, btn) {
            if (btn.disabled) return;
            var original = btn.dataset.original || btn.textContent;
            btn.dataset.original = original;
            btn.disabled = true;
            btn.textContent = 'Enviando...';

            fetch('/api/pedidos', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ productoId: id })
            })
                .then(function (res) {
                    return res.json().then(function (data) {
                        if (!res.ok) {
                            throw new Error(data.title || ('HTTP ' + res.status));
                        }
                        return data;
                    });
                })
                .then(function (data) {
                    btn.textContent = 'Pedido en cola';
                })
                .catch(function (err) {
                    console.error('Error al comprar el producto ' + id, err);
                    btn.disabled = false;
                    btn.textContent = original;
                });
        };

        $(function () {
            $(document).on('click', '.btn-comprar', function () {
                var btn = this;
                var id = parseInt(btn.getAttribute('data-producto-id'), 10);
                if (!isNaN(id)) {
                    window.comprarProducto(id, btn);
                }
            });
        });

        $(function () {
            $('form[data-recordar]').on('submit', function () {
                var form = $(this);
                var id = parseInt(form.data('id'), 10);
                var accion = form.data('recordar');
                if (accion === 'Recordar') {
                    window.recordarProductoEnCliente(id);
                } else if (accion === 'NoRecordar') {
                    window.quitarProductoEnCliente(id);
                }
            });

            window.mostrarContadorProductosRecordados();
        });
    })(window.jQuery);
}
