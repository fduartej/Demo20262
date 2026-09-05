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
