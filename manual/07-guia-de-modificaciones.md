# Guía segura de modificaciones

## Checklist universal

Antes:

- Leer la sección relacionada de este manual.
- Ejecutar `git status --short` y no sobrescribir cambios ajenos.
- Buscar consumidores con `rg "NombreActual" src tests`.
- Identificar impacto en contrato, datos, secretos y servicios externos.

Durante:

- Mantener inyección y `CancellationToken`.
- No hardcodear secretos ni URLs que deban ser configurables.
- Actualizar appsettings de Desarrollo y producción.
- Añadir prueba al nivel correcto.
- Actualizar este manual al cambiar contratos o limitaciones.

Después:

```powershell
dotnet restore AnalisisSentimiento.slnx
dotnet build AnalisisSentimiento.slnx --configuration Debug
dotnet test AnalisisSentimiento.slnx --configuration Debug
dotnet run --project .\src\Web\Web.csproj -- --no-open
```

Comprobar `/`, `/health`, `/api/social-comments` y revisar logs sin secretos.

## Agregar una red social

1. Añadir nombre canónico a `Platforms` de `ApifySocialCommentProvider`.
2. Añadir URL y etiqueta a `SocialListeningOptions.Profiles` y ambos appsettings.
3. Añadir actor(es) a `ApifyActorOptions`.
4. Añadir validación en `TryValidateProfileUrl` y parsers de URL.
5. Implementar extracción y mapeo a `SocialCommentDraft`.
6. Añadir resolución de token si requiere credencial propia.
7. Actualizar `normalizePlatform`, `platformLabel` y `sources` en `app.js`.
8. Añadir input, módulo, filtros y exportación.
9. Añadir pruebas de JSON, URL inválida, vacío, timeout y deduplicación.

## Cambiar sentimiento

- Regla local: editar `SpanishSentimentAnalyzer` y añadir pruebas de frases positivas,
  negativas, neutrales, negaciones, contraste e ironía.
- Gemini: editar prompt, esquema o calibración; conservar validación de IDs y fallback.
- `confidence` no es probabilidad calibrada sin evidencia estadística.
- Si cambia una etiqueta, actualizar frontend, CSV, resumen, CSS y tests.

## Agregar/cambiar endpoint

1. Definir contrato en Application.
2. Crear handler internal cuando la arquitectura lo exige.
3. Registrar endpoint en `src/Web/Endpoints`.
4. Validar body y autorización.
5. Usar `Result` para errores esperables.
6. Añadir prueba funcional y actualizar API/manual.

## Cambiar base de datos

Antes de cambiar entidades decidir si el dato es persistente o de sesión; revisar
`ApplicationDbContext`, configuraciones, seed y el borrado de Development. Decidir
migración y backup. No asumir que una tabla sobrevivirá un reinicio mientras exista
`EnsureDeletedAsync`.

## Cambiar frontend

1. Buscar todos los IDs HTML usados por `getElementById`.
2. Mantener escape de contenido externo.
3. No mutar `state.comments` sin deduplicar/repintar.
4. Probar responsive y accesibilidad.
5. Probar navegación, filtros, cancelación, historial y CSV.

## Cambiar actor externo

Capturar una respuesta JSON real anonimizada. Desarrollar con campos opcionales y
fallbacks. Probar campos faltantes, fechas, autor, URLs y duplicados.

## Cambiar límites/concurrencia

Revisar Apify, `ServiceDefaults` y Gemini juntos. Aumentar límites puede causar timeout,
consumo de créditos, memoria o lentitud. Medir con logs y OpenTelemetry en Aspire.
