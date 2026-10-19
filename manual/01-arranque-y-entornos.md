# Arranque, dependencias y entornos

## Requisitos

- SDK .NET compatible con `global.json` (`10.0.201` o superior dentro del canal).
- NuGet con acceso a internet para restaurar paquetes.
- Aspire CLI solo para el flujo AppHost.
- Docker/Podman si se usan recursos Aspire que lo requieran. Web directo no necesita
  contenedores.

## Ejecución recomendada para desarrollo/demo

Desde la raíz:

```powershell
dotnet restore AnalisisSentimiento.slnx
dotnet build AnalisisSentimiento.slnx --configuration Debug
dotnet run --project .\src\Web\Web.csproj -- --no-open
```

La aplicación imprime `Sentia Global -> http://127.0.0.1:5388`. Si está ocupado puede
usar otro puerto entre 5388 y 5407; abrir la URL que muestre la terminal.

Para abrir navegador automáticamente, quitar `--no-open`.

## Ejecución con Aspire

```powershell
aspire start --apphost src\AppHost\AppHost.csproj
aspire wait webapi
```

También se puede ejecutar `dotnet run --project .\src\AppHost\AppHost.csproj`.
`src/AppHost/Program.cs` crea un entorno de Azure Container Apps, un recurso SQLite y
registra Web como `webapi`.

## Modos de ejecución

| Modo | Detección | Entorno | Telemetría | OpenAPI/Scalar |
|---|---|---|---|---|
| Desktop directo | no existe `DOTNET_RESOURCE_SERVICE_ENDPOINT_URL` | Development forzado | No | No |
| Aspire | variable presente | el del host | Sí | Sí |
| Publicación | depende del proceso | no debe ser Development | configurable | Sí fuera de desktop |

## Variables y argumentos de host

| Variable/argumento | Uso | Limitación |
|---|---|---|
| `DOTNET_RESOURCE_SERVICE_ENDPOINT_URL` | Distingue Aspire de desktop | Lo gestiona Aspire |
| `ASPNETCORE_ENVIRONMENT` | Entorno fuera de desktop | Desktop fuerza Development |
| `ASPNETCORE_URLS` | URLs estándar de Kestrel | Desktop limpia `app.Urls` |
| `AZURE_KEY_VAULT_ENDPOINT` | Activa Key Vault no desktop | Requiere identidad Azure |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Activa exporter OTLP | Desktop desactiva telemetría |
| `--no-open` | Evita abrir navegador | No cambia servidor |

## Validación

```powershell
dotnet --version
aspire --version
dotnet test AnalisisSentimiento.slnx --configuration Debug
Invoke-WebRequest http://127.0.0.1:5388/health
Invoke-WebRequest http://127.0.0.1:5388/
```

## Publicación Windows

`PUBLICAR_EXE.cmd` crea `dist\SentiaGlobal\SentiaGlobal.exe`. Distribuir la carpeta
completa, no solo el EXE: contiene `wwwroot`, configuración, SQLite nativo y hosting.
