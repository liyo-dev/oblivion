# Checklist — Subir la demo de "El Sendero de las Estrellas" a Steam

Partiendo de cero (sin cuenta de Steamworks todavía). Orden real de dependencias: hay pasos que bloquean a otros (el fee bloquea la app, la app bloquea la demo, la store page bloquea el lanzamiento).

---

## Fase 1 — Cuenta y papeleo (Valve, no técnico)

- [ ] Tener una cuenta de Steam normal con **al menos $5 gastados** (requisito para poder crear cuenta de Steamworks).
- [ ] Crear la cuenta de socio en **partner.steamgames.com**.
- [ ] Rellenar el cuestionario fiscal (tax interview) y datos bancarios para poder cobrar.
- [ ] Verificar identidad cuando Steamworks lo pida.
- [ ] Pagar el **fee de Steam Direct: $100** por el juego base (no hace falta pagar otro fee para la demo, se cuelga de la misma app).
  - Es reembolsable una vez el juego alcance $1,000 de ingresos brutos ajustados.
- [ ] Tras el pago hay una **espera obligatoria de 30 días** antes de poder publicar nada. Valve la usa para verificar quién eres. **Esto es lo primero que deberías arrancar**, porque corre en paralelo a todo lo demás.

---

## Fase 2 — Crear la app y la app de demo

- [ ] En Steamworks, crear la **app del juego base** (aunque el lanzamiento completo esté lejos, la demo cuelga de esta app).
- [ ] Crear una **segunda app separada de tipo "Demo"**.
  - En su configuración general hay que introducir el **App ID del juego base** para enlazarlas.
  - Se crea un depot automáticamente para la demo (debería verse exactamente uno en la pantalla de depots).
- [ ] Decidir la store page de la demo: se puede configurar una página propia completa, o simplemente aportar assets para que la demo aparezca dentro de la store page del juego base. Para una demo temprana, lo normal es la segunda opción (más simple, menos mantenimiento).

---

## Fase 3 — Contenido de la demo (esto ya lo tienes decidido)

Ya que sabes qué escenas/tramo entra en la demo, solo queda:

- [ ] Confirmar que ese tramo es jugable de principio a fin sin dependencias de sistemas que aún no estén cerrados (guardado, quests, etc. — revisa `TDD.md` § 13 por si hay bugs conocidos que afecten justo a esas escenas).
- [ ] Añadir una pantalla o mensaje de "fin de la demo" al terminar el tramo (evita que el jugador se quede colgado o salga del contenido probado).
- [ ] Revisar que el flujo de arranque (`Start.unity` con los managers persistentes) funciona igual en un build standalone que en el editor — probar el build, no solo Play en el editor.

---

## Fase 4 — Store page (assets y textos)

- [ ] Al menos **5 capturas de pantalla**.
- [ ] Un **tráiler** (recomendado, casi obligatorio en la práctica para conversión de la página).
- [ ] Descripción corta y descripción larga del juego.
- [ ] Tags / categorías.
- [ ] Precio (o marcar como gratis si la demo se distribuye independiente, aunque normalmente la demo es gratis por definición y el juego base lleva su precio).
- [ ] Assets gráficos con tamaños exactos (Valve rechaza por 1-2 px de diferencia):
  - Header Capsule: 920×430 px
  - Small Capsule: 462×174 px
  - Main Capsule: 1232×706 px
  - Vertical Capsule: 748×896 px
  - Library Capsule: 600×900 px
  - Todos JPG o PNG, máx. 2 MB.
- [ ] Desde septiembre 2022 la capsule base solo puede llevar: arte del juego, nombre del juego, subtítulo oficial. Nada de puntuaciones, premios ni texto de marketing — si no, Valve penaliza visibilidad en tienda.
- [ ] **La store page debe estar publicada (visible) al menos 2 semanas antes de poder lanzar** cualquier build, incluida la demo.

---

## Fase 5 — Build técnico y subida (SteamPipe)

- [ ] Descargar el **Steamworks SDK** (ContentBuilder).
- [ ] Generar el build de Unity para Windows (mínimo; valorar Mac/Linux según alcance).
  - Revisar Player Settings: `companyName: Liyodev`, `productName: El Sendero de las Estrellas`, versión actual `0.1.0` — decidir si la demo lleva su propio número de versión visible.
- [ ] Configurar los scripts `.vdf` de `app_build` y `depot_build` con el App ID de la demo y el depot creado en Fase 2.
- [ ] Subir con `steamcmd` (o Web Upload / ZIP si se prefiere algo más simple para una primera subida).
- [ ] Probar el build subido desde una **branch privada de Steamworks** (no la `default`) antes de hacerlo público, para no exponer una demo rota.
- [ ] Cuando esté validado, mover a la branch pública.

---

## Fase 6 — Lanzamiento

- [ ] Confirmar que ya pasaron los 30 días de espera de Valve.
- [ ] Confirmar que la store page lleva ≥2 semanas visible.
- [ ] Publicar la demo (cambiar de "coming soon" / oculta a visible/jugable).
- [ ] Anunciar (redes, Discord, etc. — fuera del alcance técnico, pero es el paso que de verdad mueve wishlists).

---

## Notas

- El fee y la espera de 30 días son lo más largo del proceso y no dependen de ti una vez pagado — conviene arrancar la Fase 1 ya, en paralelo a pulir el tramo jugable de la demo.
- Los tamaños de capsule y las reglas de contenido cambian de vez en cuando; si tardas meses en llegar a la Fase 4, vale la pena revalidar contra la documentación oficial de Steamworks antes de subir el arte final.

**Fuentes:**
- [Steam Direct Fee — Steamworks Documentation](https://partner.steamgames.com/doc/gettingstarted/appfee)
- [Demos — Steamworks Documentation](https://partner.steamgames.com/doc/store/application/demos)
- [Store Graphical Assets — Steamworks Documentation](https://partner.steamgames.com/doc/store/assets/standard)
- [Testing On Steam — Steamworks Documentation](https://partner.steamgames.com/doc/store/testing)
