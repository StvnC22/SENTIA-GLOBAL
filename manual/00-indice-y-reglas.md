# Índice operativo y reglas de arquitectura

## Mapa de carpetas

```text
src/
  Domain/          Entidades, value objects, eventos y reglas puras.
  Application/     Casos de uso, CQRS, validación y contratos.
  Infrastructure/  EF Core, Identity, SQLite, Apify, Gemini y almacenamiento local.
  Web/             Host HTTP, endpoints, middleware y frontend wwwroot.
  ServiceDefaults/ Aspire, health checks, resiliencia y OpenTelemetry.
  AppHost/         Orquestador de Aspire y recurso SQLite.
  Shared/          Constantes compartidas entre AppHost y servicios.
tests/             Unitarios, funcionales, integración y arquitectura.
manual/            Este manual.
```

## Dependencias entre capas

```text
Web -> Application -> Domain
Web -> Infrastructure -> Application -> Domain
AppHost -> Web + Shared
ServiceDefaults -> Web/AppHost mediante referencia de proyecto
Tests -> la capa concreta que verifican
```

Domain no conoce infraestructura; Application conoce interfaces, no clientes concretos;
Infrastructure implementa esas interfaces; Web traduce HTTP a comandos/consultas y
resultados a respuestas.

## Dónde modificar cada cosa

| Necesidad | Archivo/carpeta principal | No tocar primero |
|---|---|---|
| Regla de negocio | `src/Application` o `src/Domain` | `app.js` |
| Nuevo origen social | `src/Infrastructure/SocialListening/Apify` | endpoint, salvo nuevo parámetro |
| Configuración | `SocialListeningOptions`, `appsettings*.json` | código hardcodeado |
| Ruta HTTP | `src/Web/Endpoints` | `Program.cs`, salvo middleware |
| Pantalla/interacción | `src/Web/wwwroot` | backend si no cambia contrato |
| Persistencia | `src/Infrastructure/Data` | archivos de publicación |
| Auth/autorización | `src/Infrastructure/Identity`, behaviours y endpoints | CSS |
| Health/telemetría | `src/ServiceDefaults/Extensions.cs` | lógica de negocio |
| Inicio de servicios | `src/Web/Program.cs`, `src/AppHost/Program.cs` | dominio |

## Reglas de modificación

1. Mantener requests/responses cerca del feature cuando sean específicos.
2. No enviar claves de API al navegador; solo estado o máscaras.
3. No llamar Apify/Gemini desde frontend; siempre pasar por Infrastructure.
4. Toda consulta externa debe aceptar `CancellationToken` y respetar límites.
5. Todo cambio de API actualiza frontend, pruebas y este manual.
6. Las rutas protegidas deben conservar `RequireAuthorization()`.
7. No agregar migraciones EF mientras siga activo el patrón borrar/recrear en Desarrollo.
8. Nunca copiar secretos a `appsettings`, logs, historial o documentación.
9. Al cambiar plataformas, actualizar backend, validadores, etiquetas, CSV y tests.
10. Ejecutar build y pruebas antes de entregar.

## Inventario de proyectos

| Proyecto | Responsabilidad |
|---|---|
| `src/Domain/Domain.csproj` | Modelo de dominio puro |
| `src/Application/Application.csproj` | CQRS/MediatR y casos de uso |
| `src/Infrastructure/Infrastructure.csproj` | Integraciones y persistencia |
| `src/Web/Web.csproj` | Host, API y frontend |
| `src/ServiceDefaults/ServiceDefaults.csproj` | Observabilidad y defaults Aspire |
| `src/AppHost/AppHost.csproj` | Orquestación Aspire |
| `src/Shared/Shared.csproj` | Nombres de recursos |

## Proyectos de prueba

- `Domain.UnitTests`: value objects y enums.
- `Application.UnitTests`: comportamientos y casos de uso.
- `Infrastructure.IntegrationTests`: Apify, Gemini y cifrado.
- `Application.FunctionalTests`: endpoints/casos completos con AppHost de prueba.
- `Web.UnitTests`: infraestructura HTTP.
- `ArchitectureTests`: capas, visibilidad y convenciones.
