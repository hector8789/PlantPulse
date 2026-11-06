# PlantPulse — Reglas de negocio

Decisiones de negocio que implementa el backend.

## Turnos y captura
1. **Turnos y fecha operativa:** hasta 3 turnos por planta, con horario configurable. La fecha operativa es la del **inicio** del turno: el nocturno del lunes cuenta como lunes.
2. **Paros que cruzan de turno:** no se permiten. Un paro debe caer completo dentro de la ventana de su turno; si cruza, se capturan dos paros.
3. **Tiempo de ciclo ideal:** cada línea tiene un estándar y la orden lo puede sobrescribir. Al capturar, se guarda una copia en el registro de producción.
4. **Granularidad:** el agregado es `TurnoLinea` (línea + fecha operativa + turno) y es único. Tiene un registro de producción por orden y los paros del turno.
5. **Captura retroactiva y cierre:** se captura hasta 7 días atrás, nunca a futuro. Un turno cerrado es inmutable y solo un Admin lo reabre. El cierre levanta el evento de dominio `TurnoLineaCerrado`, que dispara el refresh del dataset de Power BI.

Reglas derivadas: los paros no se traslapan; un turno se abre después de su inicio y se cierra después de su fin; una orden Terminada no acepta producción; con denominador 0 → 0%; el catálogo de causas es global; los datos demo no tienen producción en domingo.

## Seguridad
- **Roles:** Admin (todas las plantas, único que reabre), Supervisor (captura en sus plantas) y Consulta (solo lectura en sus plantas). El acceso por planta viaja en el claim `planta` del JWT.
- **Usuarios demo:** `admin@`, `supervisor.leon@`, `supervisor.silao@` y `gerente@plantpulse.mx`; todos con la contraseña `PlantPulse2026!`.

## Órdenes, costos y KPIs
- Una orden pasa de Liberada a **EnProceso** con su primer registro de producción.
- **Costo del scrap:** piezas de scrap × costo estándar por pieza de la orden (MXN, viene del ERP). En USD se divide entre el FIX de Banxico (serie SF43718) vigente en la fecha operativa; en fin de semana o festivo se usa el último publicado. Si no hay costo, las piezas se reportan aparte (`piezasScrapSinCosto`).
- **Sincronización con el ERP:** crea las órdenes nuevas y actualiza el plan de las que no están terminadas. Una orden TECO en el ERP termina la orden local. Se omiten, con su motivo: las órdenes nuevas que ya vienen TECO, los centros de trabajo desconocidos, las de otra planta y los cambios de línea.
- **OEE agregado:** suma tiempos y piezas y recalcula; no promedia porcentajes.
