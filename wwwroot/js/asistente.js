// Asistente de la tienda (widget flotante). Respuestas en streaming desde /Asistente/Preguntar.
(function () {
    'use strict';

    var root = document.getElementById('mm-asistente');
    if (!root) {
        return;
    }

    var disponible = root.dataset.disponible === '1';

    var toggle = document.getElementById('mm-asistente-toggle');
    var panel = document.getElementById('mm-asistente-panel');
    var botonCerrar = document.getElementById('mm-asistente-cerrar');
    var botonLimpiar = document.getElementById('mm-asistente-limpiar');
    var form = document.getElementById('mm-asistente-form');
    var input = document.getElementById('mm-asistente-input');
    var lista = document.getElementById('mm-asistente-mensajes');
    var botonEnviar = form ? form.querySelector('button[type="submit"]') : null;

    var enviando = false;

    function token() {
        var campo = root.querySelector('input[name="__RequestVerificationToken"]');
        return campo ? campo.value : '';
    }

    function abrir() {
        panel.hidden = false;
        if (toggle) {
            toggle.setAttribute('aria-expanded', 'true');
        }
        if (disponible && input) {
            input.focus();
        }
    }

    function cerrar() {
        panel.hidden = true;
        if (toggle) {
            toggle.setAttribute('aria-expanded', 'false');
        }
    }

    function burbuja(texto, tipo) {
        var div = document.createElement('div');
        div.className = 'mm-msg ' + tipo;
        if (texto) {
            div.textContent = texto;
        }
        lista.appendChild(div);
        return div;
    }

    function bienvenida() {
        var div = document.createElement('div');
        div.className = 'mm-asistente-bienvenida';
        div.textContent = 'Hola, soy el asistente de Mundo Mascota. Pregúntame sobre productos, pedidos, '
            + 'envíos, recomendaciones o servicios de la tienda.';
        return div;
    }

    function abajo() {
        lista.scrollTop = lista.scrollHeight;
    }

    function textoUsuario(texto) {
        var burbujaUsuario = burbuja(texto, 'mm-msg-user');
        abajo();
        return burbujaUsuario;
    }

    function estadoEnvio(activo) {
        enviando = activo;
        if (botonEnviar) {
            botonEnviar.disabled = activo;
        }
    }

    function mostrarFuentes(linea, bot) {
        var meta;
        try {
            meta = JSON.parse(linea);
        } catch (e) {
            return;
        }

        var aviso = document.createElement('div');
        aviso.className = 'mm-fuentes';

        if (!meta.contexto) {
            aviso.classList.add('mm-fuentes-aviso');
            aviso.textContent = '\u26A0 Sin coincidencias en la base de conocimiento de la tienda.';
        } else if (meta.fuentes && meta.fuentes.length) {
            aviso.textContent = 'Fuentes: ' + meta.fuentes.join(', ');
        } else {
            return;
        }

        bot.insertAdjacentElement('afterend', aviso);
        abajo();
    }

    if (toggle) {
        toggle.addEventListener('click', function () {
            if (panel.hidden) {
                abrir();
            } else {
                cerrar();
            }
        });
    }

    if (botonCerrar) {
        botonCerrar.addEventListener('click', cerrar);
    }

    if (botonLimpiar) {
        botonLimpiar.addEventListener('click', function () {
            fetch('/Asistente/Limpiar', {
                method: 'POST',
                headers: { 'RequestVerificationToken': token() }
            }).catch(function () { });

            lista.innerHTML = '';
            lista.appendChild(bienvenida());
            if (disponible && input) {
                input.focus();
            }
        });
    }

    if (form) {
        form.addEventListener('submit', function (evento) {
            evento.preventDefault();

            if (enviando) {
                return;
            }

            var texto = (input.value || '').trim();
            if (!texto) {
                return;
            }

            textoUsuario(texto);
            input.value = '';

            if (!disponible) {
                burbuja('El asistente aún no está configurado. Contacta al administrador.', 'mm-msg-bot mm-msg-error');
                abajo();
                return;
            }

            var bot = burbuja('', 'mm-msg-bot');
            bot.innerHTML = '<span class="mm-typing"><i></i><i></i><i></i></span>';
            abajo();

            estadoEnvio(true);

            fetch('/Asistente/Preguntar', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': token()
                },
                body: JSON.stringify({ mensaje: texto })
            }).then(function (respuesta) {
                if (!respuesta.ok || !respuesta.body) {
                    throw new Error('HTTP ' + respuesta.status);
                }

                var lector = respuesta.body.getReader();
                var decoder = new TextDecoder();
                var pendiente = '';
                var acumulado = '';
                var metaLeida = false;
                var primero = true;

                function volcar() {
                    if (!pendiente) {
                        return;
                    }

                    if (primero) {
                        bot.innerHTML = '';
                        primero = false;
                    }

                    acumulado += pendiente;
                    pendiente = '';
                    bot.textContent = acumulado;
                    abajo();
                }

                function empujar(trozo) {
                    pendiente += trozo;

                    if (!metaLeida) {
                        var salto = pendiente.indexOf('\n');
                        if (salto === -1) {
                            return;
                        }

                        var linea = pendiente.slice(0, salto);
                        pendiente = pendiente.slice(salto + 1);
                        metaLeida = true;
                        mostrarFuentes(linea, bot);
                    }

                    volcar();
                }

                function leer() {
                    return lector.read().then(function (resultado) {
                        if (resultado.done) {
                            volcar();
                            if (primero) {
                                bot.classList.add('mm-msg-error');
                                bot.textContent = 'No obtuve respuesta del asistente. Intenta nuevamente.';
                                abajo();
                            }
                            return;
                        }

                        empujar(decoder.decode(resultado.value, { stream: true }));
                        return leer();
                    });
                }

                return leer();
            }).catch(function (error) {
                bot.classList.add('mm-msg-error');
                bot.textContent = 'Lo siento, no pude responder ahora. Intenta nuevamente.';
                console.error('Asistente:', error);
            }).then(function () {
                estadoEnvio(false);
                if (disponible && input) {
                    input.focus();
                }
            });
        });
    }
})();
