namespace AnalisisSentimiento.Application.ObservatoryDemo;

internal static class ObservatoryDemoSnapshot
{
    public static ObservatoryDemoDashboard Create(DateTimeOffset now)
    {
        return new ObservatoryDemoDashboard(
            "demo",
            "Esta vista es una demostración del observatorio territorial. No modifica el monitoreo social, las credenciales ni los análisis reales.",
            new ObservatoryKpis(1840, 41.2, 33.6, 25.2, 7, 3, "Manabí"),
            CreateTools(),
            CreateTerritories(),
            CreateSearches(now),
            CreateAlerts(now),
            CreateRoles(),
            CreateReport(),
            CreateAudit(now),
            new QualitySnapshot(126, 38, 22, 18, 91.4),
            CreateTrend()
        );
    }

    public static byte[] CreateCsv(DateTimeOffset now)
    {
        var dashboard = Create(now);
        var lines = new List<string>
        {
            "provincia,canton,parroquia,menciones,negatividad_pct,sentimiento",
        };

        lines.AddRange(
            dashboard.Territories.Select(item =>
                $"{Escape(item.Province)},{Escape(item.Canton)},{Escape(item.Parish)},{item.Mentions},{item.NegativityPercent:0.0},{item.DominantSentiment}"
            )
        );

        var csv = string.Join("\n", lines) + "\n";
        return [.. System.Text.Encoding.UTF8.GetPreamble(), .. System.Text.Encoding.UTF8.GetBytes(csv)];
    }

    private static IReadOnlyList<ObservatoryTool> CreateTools() =>
        [
            new(
                "Georreferenciación",
                "Catálogo provincial de Manabí en esta demo",
                "Tablas INEC DPA + Nominatim (OpenStreetMap) o Bing Maps con licencia; lat/lon y códigos DPA en cada mención"
            ),
            new(
                "Monitoreo programado",
                "Consultas y próximas ejecuciones simuladas",
                "Hangfire o Quartz.NET en AppHost, historial de MonitoringRun en PostgreSQL"
            ),
            new(
                "Alertas",
                "Panel interno con umbral de negatividad",
                "Hangfire + correo SMTP, Microsoft Graph (Teams) o WhatsApp Cloud API oficial"
            ),
            new(
                "Roles",
                "Matriz Administrador / Analista / Consultor",
                "ASP.NET Core Identity, políticas por endpoint y vista; Entra ID si la institución lo exige"
            ),
            new(
                "Reportes",
                "CSV descargable y resumen ejecutivo en pantalla",
                "ClosedXML o EPPlus para Excel; QuestPDF para PDF generado en backend"
            ),
            new(
                "Auditoría",
                "Bitácora de ejemplo",
                "Interceptor de EF Core + tabla AuditEvent; sin tokens ni PII en logs"
            ),
            new(
                "Calidad",
                "Conteos de duplicados, spam y bots de ejemplo",
                "Reglas locales + Gemini para sospechosos; priorizar cuentas oficiales de Apify/APIs"
            ),
            new(
                "Dashboards",
                "KPIs y barras por territorio",
                "Agregaciones EF Core + Chart.js en wwwroot, o Fluent UI React si se migra el frontend"
            ),
        ];

    private static IReadOnlyList<TerritoryIndicator> CreateTerritories() =>
        [
            new("Manabí", "Manta", "Tarqui", 412, 38.1, "negative", -0.95, -80.73),
            new("Manabí", "Manta", "Eloy Alfaro", 286, 29.4, "neutral", -0.95, -80.71),
            new("Manabí", "Portoviejo", "Portoviejo", 354, 22.6, "positive", -1.05, -80.45),
            new("Manabí", "Portoviejo", "Colón", 128, 31.2, "neutral", -1.07, -80.42),
            new("Manabí", "Montecristi", "Montecristi", 167, 18.4, "positive", -1.05, -80.66),
            new("Manabí", "Jaramijó", "Jaramijó", 94, 41.8, "negative", -0.95, -80.64),
            new("Manabí", "Chone", "Chone", 189, 27.0, "neutral", -0.70, -80.09),
            new("Guayas", "Guayaquil", "Tarqui", 121, 24.8, "positive", -2.19, -79.89),
            new("Pichincha", "Quito", "Iñaquito", 89, 16.9, "positive", -0.18, -78.48),
        ];

    private static IReadOnlyList<ScheduledSearch> CreateSearches(DateTimeOffset now) =>
        [
            new(
                "bus-01",
                "ULEAM OR universidad AND Manabí",
                "Manabí",
                ["facebook", "instagram", "x"],
                "cada 6 horas",
                now.AddHours(-2),
                now.AddHours(4),
                "ok"
            ),
            new(
                "bus-02",
                "#Manta OR playa NOT spam",
                "Manta",
                ["tiktok", "instagram"],
                "cada 12 horas",
                now.AddHours(-8),
                now.AddHours(4),
                "ok"
            ),
            new(
                "bus-03",
                "salud pública OR hospital",
                "Portoviejo",
                ["facebook", "x"],
                "diario 07:00",
                now.AddDays(-1),
                now.Date.AddDays(1).AddHours(12),
                "pendiente"
            ),
        ];

    private static IReadOnlyList<SentimentAlert> CreateAlerts(DateTimeOffset now) =>
        [
            new(
                "alt-01",
                "alta",
                "Jaramijó",
                "Incremento de negatividad +18 pts en 6 h sobre obras viales",
                94,
                41.8,
                "panel",
                now.AddMinutes(-42)
            ),
            new(
                "alt-02",
                "media",
                "Tarqui (Manta)",
                "Volumen inusual de menciones sobre matrículas ULEAM",
                412,
                38.1,
                "panel",
                now.AddHours(-3)
            ),
            new(
                "alt-03",
                "baja",
                "Portoviejo",
                "Rebote positivo tras anuncio de becas",
                354,
                22.6,
                "panel",
                now.AddHours(-9)
            ),
        ];

    private static IReadOnlyList<RoleCapability> CreateRoles() =>
        [
            new("Administrador", "Configura tokens, usuarios, filtros y seguridad", true, true, true, true),
            new("Analista", "Diseña búsquedas, analiza y exporta reportes", false, true, true, true),
            new("Consultor", "Solo visualiza dashboards autorizados", false, false, false, false),
        ];

    private static ExecutiveSummary CreateReport() =>
        new(
            "Percepción territorial de ULEAM y servicios locales en Manabí",
            "La conversación se concentra en Manta y Portoviejo. Jaramijó y Tarqui concentran la negatividad; Montecristi y Portoviejo sostienen el tono positivo. El volumen creció 12 % frente a la semana anterior.",
            ["matrícula", "becas", "vías", "ULEAM", "hospital", "playa"],
            0.16
        );

    private static IReadOnlyList<AuditEvent> CreateAudit(DateTimeOffset now) =>
        [
            new(now.AddMinutes(-12), "analista.uleam", "exportó CSV territorial", "reporte"),
            new(now.AddHours(-1), "consultor.observatorio", "abrió dashboard Manabí", "consulta"),
            new(now.AddHours(-4), "administrador", "revisó políticas de rol Analista", "seguridad"),
            new(now.AddHours(-6), "analista.uleam", "guardó búsqueda #Manta", "monitoreo"),
        ];

    private static IReadOnlyList<TrendPoint> CreateTrend() =>
        [
            new("Lun", 210, 28),
            new("Mar", 248, 31),
            new("Mié", 266, 29),
            new("Jue", 301, 36),
            new("Vie", 274, 33),
            new("Sáb", 198, 24),
            new("Dom", 143, 22),
        ];

    private static string Escape(string value) =>
        value.Contains(',') ? $"\"{value}\"" : value;
}

public record ObservatoryDemoDashboard(
    string Mode,
    string Disclaimer,
    ObservatoryKpis Kpis,
    IReadOnlyList<ObservatoryTool> Tools,
    IReadOnlyList<TerritoryIndicator> Territories,
    IReadOnlyList<ScheduledSearch> Searches,
    IReadOnlyList<SentimentAlert> Alerts,
    IReadOnlyList<RoleCapability> Roles,
    ExecutiveSummary Report,
    IReadOnlyList<AuditEvent> Audit,
    QualitySnapshot Quality,
    IReadOnlyList<TrendPoint> Trend
);

public record ObservatoryKpis(
    int Mentions,
    double PositivePercent,
    double NeutralPercent,
    double NegativePercent,
    int ActiveSearches,
    int OpenAlerts,
    string HotspotProvince
);

public record ObservatoryTool(string Need, string InThisDemo, string ProductionTool);

public record TerritoryIndicator(
    string Province,
    string Canton,
    string Parish,
    int Mentions,
    double NegativityPercent,
    string DominantSentiment,
    double Latitude,
    double Longitude
);

public record ScheduledSearch(
    string Id,
    string Query,
    string Territory,
    IReadOnlyList<string> Networks,
    string Cadence,
    DateTimeOffset LastRunUtc,
    DateTimeOffset NextRunUtc,
    string Status
);

public record SentimentAlert(
    string Id,
    string Severity,
    string Territory,
    string Message,
    int Volume,
    double NegativityPercent,
    string Channel,
    DateTimeOffset RaisedAtUtc
);

public record RoleCapability(
    string Role,
    string Summary,
    bool ConfigureTokens,
    bool DesignSearches,
    bool ExportReports,
    bool ManageUsers
);

public record ExecutiveSummary(
    string Title,
    string Findings,
    IReadOnlyList<string> Topics,
    double AverageSentiment
);

public record AuditEvent(DateTimeOffset AtUtc, string Actor, string Action, string Resource);

public record QualitySnapshot(
    int DuplicatesRemoved,
    int SpamFlagged,
    int BotsFlagged,
    int UnofficialSources,
    double ReliablePercent
);

public record TrendPoint(string Label, int Volume, int NegativeShare);

public record ObservatoryDemoExportFile(string FileName, string ContentType, byte[] Content);
