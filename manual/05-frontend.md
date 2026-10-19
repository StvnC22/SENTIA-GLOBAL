# Frontend, estado y funciones JavaScript

## Archivos

- `src/Web/wwwroot/index.html`: estructura de vistas, formularios y diálogos.
- `src/Web/wwwroot/styles.css`: tema, layout, tablas, estados y responsive.
- `src/Web/wwwroot/app.js`: estado, navegación, API, jobs, historial y eventos.

No hay framework ni bundler. El orden de carga y los IDs HTML son parte del contrato.
Si se cambia un `id`, cambiar también `app.js` y revisar accesibilidad.

## Estado global `state`

| Propiedad | Tipo | Uso |
|---|---|---|
| `activeNav` | string | dashboard, red, global, history, settings |
| `comments` | array | Comentarios actuales en memoria |
| `summary` | object/null | Resumen calculado/renderizado |
| `platform` | string | Filtro/plataforma |
| `prompt` | string | Prompt de clasificación |
| `query` | string | Búsqueda |
| `sentiment` | string | all/positive/neutral/negative |
| `sort` | string | Orden de tabla |
| `periodDays` | number | Ventana temporal; default 400 |
| `sources` | object | URLs por red |
| `prompts` | object | Prompts por red/global |
| `credentialStatus` | object | Estado no sensible |
| `credentialMasks` | object | Máscaras visuales |
| `jobs` | object | Trabajos por plataforma |
| `history` | array | Historial en localStorage |
| `theme` | string | dark/light |

## Utilidades y normalización

| Función | Uso |
|---|---|
| `escapeHtml` | Evita inyección HTML al renderizar texto externo |
| `initials` | Iniciales de avatar |
| `percent` | Porcentaje redondeado |
| `percentageBreakdown` | Porcentajes cuya suma visible es 100 |
| `formatDay`, `formatTime` | Formato `es-EC` |
| `platformLabel` | Etiqueta visible |
| `normalizePlatform` | Unifica twitter/X y variantes |
| `commentIdentity` | Clave de deduplicación |
| `deduplicateComments` | Elimina duplicados conservando el último |

## Navegación y renderizado

| Función | Uso |
|---|---|
| `initTheme`/`setTheme` | Lee/escribe tema en localStorage |
| `initNavigation` | Conecta botones `data-nav` |
| `navigateTo` | Cambia panel y repinta |
| `updateNotch` | Estado, jobs y contador superior |
| `renderActiveView` | Punto central de repintado |
| `renderSidebarCounts` | Conteos por red |
| `renderMetrics` | Tarjetas de resumen |
| `renderDashboard` | Dashboard ejecutivo |
| `renderNetworkModule` | Vista de una red |
| `renderGlobalView` | Vista global |
| `renderCommentRow` | Fila; debe escapar texto |
| `renderSemanticCloud` | Nube de términos |
| `renderCalendar` | Actividad por día |

## Filtros, historial y exportación

| Función | Uso |
|---|---|
| `commentsInPeriod` | Filtra por `publishedAt` |
| `filterComments` | Red, sentimiento, búsqueda y orden |
| `loadHistory` | Lee historial local |
| `saveHistoryRecord` | Guarda resultado del job |
| `renderHistoryTable` | Lista de ejecuciones |
| `renderHistoryPlatformBreakdown` | Modal de detalle |
| `clearAllHistory` | Borra historial local |
| `exportFullHistoryLog` | Exporta historial |
| `downloadCsv` | Descarga CSV |

## Credenciales y fuentes

| Función | Uso |
|---|---|
| `refreshCredentialStatus` | GET de estado seguro |
| `saveApiKeysPayload` | PUT de claves cifradas |
| `buildCredentialPayload` | Convierte FormData en contrato |
| `applyCredentialInputs` | Replica máscaras |
| `renderCredentialStatus` | Presenta estados |
| `validateSourceUrl` | Valida esquema, host y ruta |
| `isDirectPostUrl` | Decide refresh de post directo |
| `syncSourceInput` | Sincroniza inputs módulo/global |
| `loadSavedSources` | Lee fuentes del navegador |
| `syncPromptInputs` | Sincroniza prompts |

## Jobs asíncronos

| Función | Uso |
|---|---|
| `getActiveJob`/`getRunningJobs` | Inspección |
| `startAnalysisJob` | Valida URLs y crea job |
| `executeJobAsync` | Consulta, fusiona, guarda historial |
| `fetchPlatformComments` | Construye query y llama API |
| `cancelAnalysisJob` | Aborta `AbortController` |
| `renderJobBanners` | Muestra progreso |
| `loadComments` | Entrada de análisis global/módulo |

Un job vive en el navegador. Si se cierra la pestaña se pierde; no es un proceso de
servidor. `startAnalysisJob` evita repetir el mismo job por plataforma.

## Inicialización y seguridad

`initEventHandlers` conecta inputs, análisis, cancelación, filtros, exportación, historial,
credenciales y tema. `initializeFluentSelects` implementa selectores visuales.
`initSidebarCollapse` conserva el sidebar.

- Todo texto externo debe pasar por `escapeHtml`.
- Nunca guardar tokens en `localStorage`.
- `localStorage` contiene fuentes, tema, collapse e historial.
- Si un control falla, verificar primero su ID HTML, listener, estado y repintado.
