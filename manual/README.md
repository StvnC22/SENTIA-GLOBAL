# Manual técnico de Sentia Global

## Propósito

Este manual es el mapa operativo para que un ingeniero externo pueda entender,
ejecutar, diagnosticar y modificar Sentia Global sin tener que recorrer todas las
pestañas, carpetas o archivos de la solución. Describe el estado real del código
inspeccionado, no solo la intención original de la plantilla.

Fecha de referencia: 2026-10-07.

## Ruta de lectura

1. Empieza por [00-indice-y-reglas.md](00-indice-y-reglas.md).
2. Para arrancar usa [01-arranque-y-entornos.md](01-arranque-y-entornos.md).
3. Para monitoreo social usa [03-flujo-social-y-sentimiento.md](03-flujo-social-y-sentimiento.md).
4. Antes de cambiar una ruta revisa [04-api-y-contratos.md](04-api-y-contratos.md).
5. Antes de cambiar la interfaz revisa [05-frontend.md](05-frontend.md).
6. Antes de publicar revisa [06-seguridad-datos-y-despliegue.md](06-seguridad-datos-y-despliegue.md).
7. Después de cualquier cambio sigue [07-guia-de-modificaciones.md](07-guia-de-modificaciones.md).
8. Para fallos conocidos consulta [08-limitaciones-y-troubleshooting.md](08-limitaciones-y-troubleshooting.md).
9. Para encontrar símbolos concretos usa [09-catalogo-del-backend.md](09-catalogo-del-backend.md).
10. Para ubicar archivos por responsabilidad usa [10-mapa-de-archivos.md](10-mapa-de-archivos.md).

## Resumen ejecutivo

Sentia Global es una aplicación web ASP.NET Core sobre .NET 10. El frontend es
HTML/CSS/JavaScript servido desde `src/Web/wwwroot`. El backend expone Minimal APIs
agrupadas por endpoint y usa MediatR para enviar consultas/comandos a Application.
La integración social obtiene publicaciones y comentarios desde Apify; el análisis de
sentimiento usa Gemini si existe una clave de IA y, si no, un clasificador local.

La solución incluye un AppHost de .NET Aspire, pero también puede ejecutarse de forma
independiente con `src/Web`, que es la modalidad más sencilla para una demostración.
La base de datos es SQLite y se usa principalmente para Identity y las entidades Todo.
Los comentarios sociales no se guardan en SQLite: viven en memoria, en caché temporal
del backend y en `localStorage` del navegador para el historial.

## Estado que no debe perderse

- El modo escritorio fuerza entorno `Development`, usa HTTP local y busca el puerto
  5388; si está ocupado prueba hasta 20 puertos.
- En Desarrollo, `ApplicationDbContextInitialiser` ejecuta `EnsureDeletedAsync()` y
  `EnsureCreatedAsync()` en cada arranque. Esto borra y recrea la base SQLite.
- Las claves de API no se guardan en `appsettings`: se cifran en un archivo local con
  ASP.NET Data Protection.
- `DataProtection` conserva deliberadamente el nombre histórico `EmocionesULEAM` para
  mantener legibles archivos cifrados existentes.
- El frontend no inicia análisis automáticamente; el usuario debe pulsar analizar y
  proporcionar una URL válida por red.
- Sin clave de IA, el sistema clasifica localmente. Sin URLs/tokens de Apify, no hay
  comentarios reales.
- El README histórico dice que siempre hay datos demo, pero la lógica actual puede
  devolver `live` con cero comentarios. Está explicado como limitación conocida.

## Convenciones

- `src/...` identifica una ruta dentro del repositorio.
- Las rutas de código se citan con clase o función; usar `rg` si las líneas cambiaron.
- “Contrato” significa que frontend, endpoint y DTO dependen de ese nombre o forma.
- “No cambiar sin migración” significa que puede romper datos aunque compile.
