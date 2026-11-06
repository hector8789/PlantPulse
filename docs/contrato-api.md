# PlantPulse — Contrato API v1

Contrato entre el API (.NET) y el frontend (React). Swagger, en `/swagger`, tiene el detalle de cada esquema.

```
══════════ CONVENCIONES ══════════
- Base URL (dev): https://localhost:7177/api   (perfil http: http://localhost:5077/api). CORS permite http://localhost:5173.
- JSON camelCase. Ids = GUID en texto. Enums como texto.
- Fechas: instantes en ISO 8601 UTC ("2026-11-04T13:00:00Z"); fechas de calendario "YYYY-MM-DD".
- fechaOperativa = fecha LOCAL de la planta en que INICIA el turno (el nocturno del lunes 22:00–06:00 cuenta como lunes).
- Zona de las plantas: America/Mexico_City. La UI muestra horas locales y manda los paros en UTC
  (convertir hora local de la planta → UTC). La ventana válida de un turno es [inicioUtc, finUtc] que devuelve el API.
- Porcentajes: fracción 0–1 con 4 decimales (0.7321 = 73.21%). Tiempos en minutos con 2 decimales. Dinero con 2 decimales.
- Auth: header "Authorization: Bearer <accessToken>". El token dura 8 h y NO hay refresh token: ante 401 → ir al login.

══════════ ERRORES (siempre ProblemDetails, content-type application/problem+json) ══════════
400 JSON mal formado · 401 sin token / token inválido o vencido · 403 rol insuficiente o planta ajena · 404 no existe
409 regla de negocio → { title, status: 409, detail, codigo }      (mostrar detail; usar codigo para lógica)
422 validación      → { title, status: 422, errors: { campo: ["mensaje"] } }   (campos en camelCase → pintar bajo el input)
503 servicio externo → { title, status: 503, detail, servicio: "ERP" | "Banxico" | "Power BI" }

Códigos 409 frecuentes:
 turno_linea.ya_existe · turno_linea.fecha_futura · turno_linea.fuera_de_ventana_captura (máx. 7 días atrás)
 turno_linea.turno_no_iniciado · turno_linea.turno_no_terminado (cerrar antes del fin) · turno_linea.cerrado
 turno_linea.no_cerrado · turno_linea.paro_fuera_de_turno (si cruza turno: capturar 2 paros) · turno_linea.paro_traslapado
 turno_linea.paro_futuro · turno_linea.paro_invalido · turno_linea.causa_inactiva
 turno_linea.orden_ya_registrada (usar PUT de corrección) · turno_linea.orden_de_otra_linea · turno_linea.orden_no_acepta_produccion
 turno_linea.registro_no_existe · turno_linea.paro_no_existe · turno_linea.linea_inactiva · planta.turno_no_existe
 conflicto.duplicado · conflicto.concurrencia (recargar y reintentar)

══════════ ROLES (claim "role" del JWT) ══════════
Admin      → todas las plantas; único que REABRE turnos.
Supervisor → captura (abrir, producción, paros, cerrar) y sincroniza ERP en SUS plantas.
Consulta   → solo lectura (catálogos, turnos, KPIs, Power BI) en sus plantas.
Usuarios demo (pass PlantPulse2026!): admin@ · supervisor.leon@ · supervisor.silao@ · gerente@plantpulse.mx (Consulta)

══════════ VALORES DE DOMINIO ══════════
estadoTurno "Abierto" | "Cerrado" · tipoParo "Planificado" | "NoPlanificado"
categoriaParo "Programado" | "Mecanica" | "Electrica" | "Material" | "Calidad" | "CambioModelo" | "Operacion"
estadoOrden "Liberada" | "EnProceso" | "Terminada"

══════════ ENDPOINTS ══════════
── Salud ──
GET /api/health (anónimo) → { service, status, timestampUtc }

── Auth ──
POST /api/auth/login (anónimo) { email, password }
 → 200 { accessToken, expiresAtUtc, user: { id, nombre, email, roles: string[], plantaIds: string[] } }
 · 401 credenciales · 422 errors.email / errors.password
GET /api/auth/me → { id, roles, plantaIds }

── Catálogos (cualquier rol; filtrados a las plantas del usuario) ──
GET /api/catalogos/plantas
 → [{ id, codigo, nombre, zonaHoraria, turnos: [{ id, numero, nombre, horaInicio: "06:00", horaFin: "14:00" }] }]
GET /api/catalogos/lineas?plantaId=
 → [{ id, plantaId, codigo, nombre, tiempoCicloEstandarSeg, activa }]
GET /api/catalogos/causas-paro
 → [{ id, codigo, descripcion, tipo, categoria, activa }]
GET /api/catalogos/lineas/{lineaId}/ordenes?soloAbiertas=true
 → [{ id, numero, lineaId, materialCodigo, materialDescripcion, cantidadPlaneada, fechaInicioPlan, fechaFinPlan,
      tiempoCicloIdealSeg, tiempoCicloPropio, costoUnitarioMxn, estado }]

── Turnos de línea ──
GET  /api/turnos-linea?plantaId=&lineaId=&desde=YYYY-MM-DD&hasta=YYYY-MM-DD      (máx. 62 días; cualquier rol)
GET  /api/turnos-linea/{id} → TurnoLinea
POST /api/turnos-linea { lineaId, turnoId, fechaOperativa }                                  [Admin|Supervisor] → 201 TurnoLinea
POST /api/turnos-linea/{id}/produccion { ordenProduccionId, piezasTotales, piezasBuenas }     [Admin|Supervisor] → 200 TurnoLinea
PUT  /api/turnos-linea/{id}/produccion/{registroId} { piezasTotales, piezasBuenas }           [Admin|Supervisor] → 200 TurnoLinea
POST /api/turnos-linea/{id}/paros { causaParoId, inicioUtc, finUtc, comentario? }             [Admin|Supervisor] → 200 TurnoLinea
DELETE /api/turnos-linea/{id}/paros/{paroId}                                                  [Admin|Supervisor] → 200 TurnoLinea
POST /api/turnos-linea/{id}/cerrar                                                            [Admin|Supervisor] → 200 TurnoLinea
POST /api/turnos-linea/{id}/reabrir                                                           [Admin]            → 200 TurnoLinea
 Toda escritura devuelve el turno COMPLETO con el OEE recalculado.
 Validación 422: piezasTotales 0–100000, piezasBuenas 0..piezasTotales, finUtc > inicioUtc, comentario ≤ 500.

Oee = { duracionTurnoMin, parosPlanificadosMin, tiempoPlanificadoMin, parosNoPlanificadosMin, tiempoOperativoMin,
  piezasTotales, piezasBuenas, scrap, disponibilidad, desempeno, calidad, oee, desempenoExcedido }

── KPIs (cualquier rol; máx. 92 días) ──
GET /api/kpis/oee?plantaId=&lineaId=&desde=&hasta=
 → { desde, hasta, turnos, total: Oee, porLinea: [...], porLineaTurno: [...] }
GET /api/kpis/costo-scrap?plantaId=&lineaId=&desde=&hasta=
 → { desde, hasta, serieTipoCambio: "SF43718", tipoCambioDesactualizado, piezasScrap, piezasScrapSinCosto,
     costoMxn, costoUsd, porLinea: [...], porDia: [{ fecha, piezasScrap, costoMxn, tipoCambio, costoUsd }] }

── ERP (Admin|Supervisor) ──
POST /api/erp/ordenes/sincronizar { plantaId }
 → { plantaId, plantaCodigo, recibidas, creadas, actualizadas, sinCambios, omitidas: [{ numero, motivo }], sincronizadoEnUtc }
 · 503 servicio "ERP" (el API ya reintentó 3 veces con Polly).

── Power BI Embedded (cualquier rol) ──
GET /api/powerbi/reportes → [{ key, nombre }]    keys: "oee", "pareto-paros", "scrap", "plan-vs-real"
GET /api/powerbi/embed/{reportKey}?plantaId=
 → { reportId, embedUrl, accessToken, expiresAtUtc }
 · Sin plantaId: RLS con todas las plantas del usuario. Con plantaId: solo esa (403 si no es suya).
 · El RLS lo aplica Power BI con el embed token: la UI no filtra planta dentro del reporte.
```
