// Asistente de la tienda (widget flotante). Respuestas en streaming desde /Asistente/Preguntar.
// El flujo combina texto (la respuesta del agente) con mensajes de control entre el carácter STX:
// { fuentes, contexto, productos }. Los productos se dibujan como tarjetas.
(function () {
    'use strict';

    var DELIMITADOR = '\u0002';

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

    function escaparHtml(texto) {
        return texto
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;');
    }

    // Convierte el marcado ligero del agente (**negrita**) en HTML, escapando primero el texto.
    function formatoTexto(texto) {
        return escaparHtml(texto).replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
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
        div.textContent = 'Hola, soy el asistente de Mundo Mascota. Pregúntame por la tienda, por el '
            + 'tipo de cambio del dólar o por el precio y el stock de un producto.';
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

    function mostrarFuentes(meta, bot) {
        var aviso = document.createElement('div');
        aviso.className = 'mm-fuentes';

        if (meta.fuentes && meta.fuentes.length) {
            aviso.textContent = 'Fuentes: ' + meta.fuentes.join(', ');
        } else if (meta.contexto) {
            aviso.classList.add('mm-fuentes-aviso');
            aviso.textContent = '\u26A0 Sin coincidencias en la base de conocimiento de la tienda.';
        } else {
            return;
        }

        bot.insertAdjacentElement('afterend', aviso);
        abajo();
    }

    function crearTarjeta(producto) {
        var tarjeta = document.createElement('article');
        tarjeta.className = 'mm-card';

        if (producto.imagenUrl) {
            var imagen = document.createElement('img');
            imagen.className = 'mm-card-img';
            imagen.src = producto.imagenUrl;
            imagen.alt = producto.nombre || 'Producto';
            imagen.loading = 'lazy';
            imagen.addEventListener('error', function () {
                imagen.remove();
            });
            tarjeta.appendChild(imagen);
        }

        var cuerpo = document.createElement('div');
        cuerpo.className = 'mm-card-cuerpo';

        if (producto.categoria) {
            var categoria = document.createElement('span');
            categoria.className = 'mm-card-cat';
            categoria.textContent = producto.categoria;
            cuerpo.appendChild(categoria);
        }

        var nombre = document.createElement('h4');
        nombre.className = 'mm-card-nombre';
        nombre.textContent = producto.nombre || 'Producto';
        cuerpo.appendChild(nombre);

        var precio = document.createElement('div');
        precio.className = 'mm-card-precio';
        precio.textContent = 'S/ ' + Number(producto.precio || 0).toFixed(2);
        cuerpo.appendChild(precio);

        var stock = Number(producto.stock);
        var disponibilidad = document.createElement('span');
        disponibilidad.className = 'mm-card-stock ' + (stock > 0 ? 'mm-card-stock-ok' : 'mm-card-stock-agotado');
        disponibilidad.textContent = stock > 0
            ? 'Stock: ' + stock + (stock === 1 ? ' unidad' : ' unidades')
            : 'Sin stock por el momento';
        cuerpo.appendChild(disponibilidad);

        if (producto.descripcion) {
            var descripcion = document.createElement('p');
            descripcion.className = 'mm-card-desc';
            descripcion.textContent = producto.descripcion;
            cuerpo.appendChild(descripcion);
        }

        if (producto.id) {
            var enlace = document.createElement('a');
            enlace.className = 'mm-card-link';
            enlace.href = '/Productos/Detalle/' + encodeURIComponent(producto.id);
            enlace.textContent = 'Ver producto';
            cuerpo.appendChild(enlace);
        }

        tarjeta.appendChild(cuerpo);
        return tarjeta;
    }

    function botonCarrusel(direccion) {
        var boton = document.createElement('button');
        boton.type = 'button';
        boton.className = 'mm-carrusel-btn mm-carrusel-' + direccion;
        boton.setAttribute('aria-label', direccion === 'anterior' ? 'Producto anterior' : 'Producto siguiente');
        boton.textContent = direccion === 'anterior' ? '\u2039' : '\u203A';
        return boton;
    }

    function crearCarrusel(productos) {
        var carrusel = document.createElement('div');
        carrusel.className = 'mm-carrusel';

        var pista = document.createElement('div');
        pista.className = 'mm-carrusel-pista';

        productos.forEach(function (producto) {
            pista.appendChild(crearTarjeta(producto));
        });
        carrusel.appendChild(pista);

        if (productos.length < 2) {
            return carrusel;
        }

        var puntos = document.createElement('div');
        puntos.className = 'mm-carrusel-puntos';

        var marcas = productos.map(function (_, indice) {
            var marca = document.createElement('button');
            marca.type = 'button';
            marca.className = 'mm-carrusel-punto';
            marca.setAttribute('aria-label', 'Ir al producto ' + (indice + 1));
            marca.addEventListener('click', function () {
                pista.scrollTo({ left: pista.clientWidth * indice, behavior: 'smooth' });
            });
            puntos.appendChild(marca);
            return marca;
        });

        function desplazar(paso) {
            pista.scrollBy({ left: pista.clientWidth * paso, behavior: 'smooth' });
        }

        var anterior = botonCarrusel('anterior');
        var siguiente = botonCarrusel('siguiente');
        anterior.addEventListener('click', function () { desplazar(-1); });
        siguiente.addEventListener('click', function () { desplazar(1); });

        function actualizarPuntos() {
            var indice = Math.round(pista.scrollLeft / Math.max(pista.clientWidth, 1));
            marcas.forEach(function (marca, posicion) {
                marca.classList.toggle('mm-carrusel-punto-activo', posicion === indice);
            });
        }

        pista.addEventListener('scroll', function () {
            window.clearTimeout(pista._mmTemporizador);
            pista._mmTemporizador = window.setTimeout(actualizarPuntos, 90);
        });

        carrusel.appendChild(anterior);
        carrusel.appendChild(siguiente);
        carrusel.appendChild(puntos);
        actualizarPuntos();

        return carrusel;
    }

    function mostrarTarjetas(productos) {
        if (!productos || !productos.length) {
            return;
        }

        lista.appendChild(crearCarrusel(productos));
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
                var visible = '';
                var indicador = true;
                var control = false;

                function limpiarIndicador() {
                    if (indicador) {
                        bot.innerHTML = '';
                        indicador = false;
                    }
                }

                function pintar() {
                    bot.innerHTML = formatoTexto(visible);
                    abajo();
                }

                function procesarControl(payload) {
                    var meta;
                    try {
                        meta = JSON.parse(payload);
                    } catch (e) {
                        return;
                    }

                    control = true;
                    limpiarIndicador();
                    mostrarFuentes(meta, bot);
                    mostrarTarjetas(meta.productos);
                }

                // Extrae del búfer los mensajes de control completos y devuelve solo el texto visible.
                function depurar() {
                    var salida = '';

                    while (true) {
                        var inicio = pendiente.indexOf(DELIMITADOR);
                        if (inicio < 0) {
                            salida += pendiente;
                            pendiente = '';
                            break;
                        }

                        salida += pendiente.slice(0, inicio);

                        var fin = pendiente.indexOf(DELIMITADOR, inicio + 1);
                        if (fin < 0) {
                            // Mensaje de control incompleto: se espera al siguiente fragmento.
                            pendiente = pendiente.slice(inicio);
                            break;
                        }

                        procesarControl(pendiente.slice(inicio + 1, fin));
                        pendiente = pendiente.slice(fin + 1);
                    }

                    return salida;
                }

                function volcar() {
                    var textoNuevo = depurar();
                    if (!textoNuevo) {
                        return;
                    }

                    limpiarIndicador();
                    visible += textoNuevo;
                    pintar();
                }

                function finalizar() {
                    volcar();

                    var resto = pendiente.indexOf(DELIMITADOR);
                    if (resto >= 0) {
                        pendiente = pendiente.slice(0, resto);
                    }
                    if (pendiente) {
                        limpiarIndicador();
                        visible += pendiente;
                        pendiente = '';
                        pintar();
                    }

                    if (!visible) {
                        if (control) {
                            // Solo llegaron tarjetas o fuentes: la burbuja de texto vacía no aporta nada.
                            bot.remove();
                        } else {
                            bot.classList.add('mm-msg-error');
                            bot.textContent = 'No obtuve respuesta del asistente. Intenta nuevamente.';
                        }
                    }

                    abajo();
                }

                function leer() {
                    return lector.read().then(function (resultado) {
                        if (resultado.done) {
                            finalizar();
                            return;
                        }

                        pendiente += decoder.decode(resultado.value, { stream: true });
                        volcar();
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
