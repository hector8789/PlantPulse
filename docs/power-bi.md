# PlantPulse — Power BI Embedded (App Owns Data)

Cómo está armado el dataset, el RLS por planta y el refresh automático al cerrar un turno. El archivo del reporte está en [`api/PlantPulse.pbix`](../api/PlantPulse.pbix).

## 1. Azure / Entra ID
1. **App registration** (`PlantPulse-PowerBI`) con *client secret*. El API se autentica como **service principal** (MSAL, client credentials).
2. **Grupo de seguridad** (`sg-plantpulse-powerbi`) con la aplicación como miembro.
3. En el portal de administración de Power BI → *Developer settings*: **"Service principals can use Fabric APIs"** habilitado para ese grupo.
4. **Workspace** `PlantPulse` con la aplicación como **Member**.
5. **Capacidad:** desarrollo en un workspace Pro (tokens de prueba); producción en Embedded (A SKU) o Fabric (F SKU). Los workspaces PPU no sirven para embed con service principal.

## 2. Modelo de datos
Las vistas viven en el esquema `bi` de PostgreSQL y se crean con la migración `VistasPowerBi`.

- Dimensiones: `dim_planta`, `dim_linea`, `dim_turno`, `dim_causa_paro`, `dim_orden`, `dim_fecha`.
- Hechos: `fact_turno`, `fact_paro`, `fact_produccion`, `fact_orden_plan`.
- Usuario de solo lectura `powerbi_reader`: solo ve el esquema `bi`, no las tablas de `public` (las vistas corren con los permisos de su dueño). En Neon el rol se crea desde la consola, que genera la contraseña, y luego se dan los permisos:
```sql
GRANT USAGE ON SCHEMA bi TO powerbi_reader;
GRANT SELECT ON ALL TABLES IN SCHEMA bi TO powerbi_reader;
-- Las vistas que creen migraciones futuras (dueño: el rol de la aplicación) nacen con SELECT.
ALTER DEFAULT PRIVILEGES FOR ROLE plantpulse_owner IN SCHEMA bi GRANT SELECT ON TABLES TO powerbi_reader;
```
En local (PostgreSQL propio) el rol se crea con `CREATE ROLE powerbi_reader LOGIN PASSWORD '...'` y `ALTER DEFAULT PRIVILEGES` va sin `FOR ROLE`.

Relaciones de uno a varios con filtro en una sola dirección, en copo de nieve para que el RLS baje por un solo camino: `dim_planta → dim_linea → hechos`, más `dim_turno`, `dim_fecha`, `dim_causa_paro` y `dim_orden` hacia sus hechos. Los hechos **no** se relacionan directo con `dim_planta` (crearía rutas ambiguas).

## 3. Medidas DAX
El OEE suma primero y luego divide, igual que el API, para que agregue bien por línea, turno o mes:
```DAX
Tiempo planificado (min) = SUM ( fact_turno[tiempo_planificado_min] )
Tiempo operativo (min)   = SUM ( fact_turno[tiempo_operativo_min] )
Disponibilidad = DIVIDE ( [Tiempo operativo (min)], [Tiempo planificado (min)], 0 )
Desempeño      = MIN ( DIVIDE ( SUM ( fact_turno[tiempo_ideal_min] ), [Tiempo operativo (min)], 0 ), 1 )
Calidad        = DIVIDE ( SUM ( fact_turno[piezas_buenas] ), SUM ( fact_turno[piezas_totales] ), 0 )
OEE            = [Disponibilidad] * [Desempeño] * [Calidad]

Minutos de paro    = SUM ( fact_paro[duracion_min] )
% acumulado Pareto =
VAR actual = [Minutos de paro]
VAR total  = CALCULATE ( [Minutos de paro], ALLSELECTED ( dim_causa_paro ) )
VAR acum   = SUMX ( FILTER ( ALLSELECTED ( dim_causa_paro ), CALCULATE ( [Minutos de paro] ) >= actual ), CALCULATE ( [Minutos de paro] ) )
RETURN DIVIDE ( acum, total )

Cumplimiento plan  = DIVIDE ( SUM ( fact_orden_plan[piezas_buenas_reales] ), SUM ( fact_orden_plan[cantidad_planeada] ), 0 )
```

**Validación cruzada API = Power BI**, igual en `GET /api/kpis/oee` y en el reporte:
- Local: OEE total 74.64% (León 73.21%, Silao 76.75%) y costo de scrap $940,829.25 MXN.
- Producción (Neon, 270 turnos): OEE total 75.40%; LEO-L01 75.92%, LEO-L02 70.54%, LEO-L03 76.26%, SIL-L01 77.94%, SIL-L02 76.39%; León embebido con RLS 74.23%.

## 4. Reporte (4 páginas)
| Clave (`reportKey`) | Página |
|---|---|
| `oee` | Tarjetas de OEE, Disponibilidad, Desempeño y Calidad; matriz línea × turno; tendencia diaria. |
| `pareto-paros` | Minutos de paro por causa (no planificados) con línea de % acumulado. |
| `scrap` | Tendencia de scrap en piezas y su costo en MXN/USD. |
| `plan-vs-real` | Plan, real y cumplimiento por orden de producción. |

## 5. RLS por planta
Rol **`PorPlanta`** con `PATHCONTAINS ( USERNAME (), <tabla>[codigo] )` en `dim_planta`, `dim_turno` y `dim_orden`. El API genera el embed token con una *effective identity* cuyo `username` es `"LEO"`, `"SIL"` o `"LEO|SIL"` según las plantas del usuario; un Admin recibe todas.

## 6. Refresh al cerrar turno
El cierre de un turno levanta `TurnoLineaCerrado` y el API pide un refresh del dataset. No hay proceso en segundo plano (el API corre en AWS Lambda, que se congela entre peticiones): el estado vive en la tabla `powerbi_refresh_estado`, una sola fila que se bloquea con `SELECT ... FOR UPDATE` para que dos instancias nunca lancen el mismo refresh.

- **Cierre de turno:** marca el refresh como pendiente (conserva la hora de la primera solicitud) e intenta lanzarlo.
- **Petición de embed token:** si hay un pendiente, intenta lanzarlo.
- **Política** (`PoliticaRefresh`, clase pura con pruebas): la primera solicitud pendiente debe tener al menos **60 s** (agrupa los cierres que llegan juntos), **30 min** desde el último refresh y menos de **8 al día** (UTC; los refreshes por API cuentan en el límite de Pro).
- Si Power BI falla, el refresh queda pendiente para el siguiente intento; el cierre del turno nunca falla por eso.

## 7. Configuración del API
```powershell
dotnet user-secrets set "PowerBi:TenantId"     "<tenant-id>"     --project src/PlantPulse.Api
dotnet user-secrets set "PowerBi:ClientId"     "<client-id>"     --project src/PlantPulse.Api
dotnet user-secrets set "PowerBi:ClientSecret" "<client-secret>" --project src/PlantPulse.Api
dotnet user-secrets set "PowerBi:WorkspaceId"  "<workspace-id>"  --project src/PlantPulse.Api
```
Los `ReportId` y las opciones de refresh están en `appsettings.Development.json` y `appsettings.Production.json` (sección `PowerBi`). En producción, los cuatro secretos vienen de SSM Parameter Store (`/plantpulse/PowerBi/...`). `powerbi/embed-test.html` permite probar un embed token sin el frontend.

## 8. Origen de datos y credenciales
| Entorno | Origen | Gateway |
|---|---|---|
| Local | PostgreSQL en `localhost` | Para refrescar en el servicio hace falta el *on-premises data gateway*. |
| Producción | Neon (`ep-...us-east-2.aws.neon.tech`, host **directo**, sin `-pooler`), base `plantpulse`, usuario `powerbi_reader` | **No**: el servicio se conecta directo. En *Configuración del modelo → Conexiones de puerta de enlace y nube* se usa una conexión en la nube, autenticación Básica, privacidad Organizativa. |

**Neon y SNI.** El proxy de Neon identifica el cómputo por SNI y el conector PostgreSQL del servicio de Power BI no lo envía (*"The endpoint ID is not specified"*). La solución documentada por Neon es poner el endpoint en la contraseña: `endpoint=<endpoint_id>$<contraseña>`. En Power BI Desktop funciona con la contraseña normal.

Al publicar: limpiar las segmentaciones (Power BI guarda la selección y el reporte embebido arrancaría filtrado) y elegir **Reemplazar** el modelo para conservar el `ReportId`.
