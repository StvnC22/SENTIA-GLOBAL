using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;
using AnalisisSentimiento.Application.SocialListening.Territory;
using Microsoft.AspNetCore.Http.HttpResults;

namespace AnalisisSentimiento.Web.Endpoints;

public class SocialComments : IEndpointGroup
{
    public static string RoutePrefix => "/api/social-comments";

    public static void Map(RouteGroupBuilder groupBuilder)
    {
        groupBuilder.RequireAuthorization();
        groupBuilder.MapGet(GetSocialComments);
        groupBuilder.MapGet(GetTerritoryCatalog, "/territory");
        groupBuilder.MapGet(ExportSocialComments, "/export");
    }

    [EndpointSummary("Obtener comentarios y análisis de sentimiento")]
    [EndpointDescription(
        "Devuelve comentarios sociales clasificados por sentimiento, con filtros de tema (Y/O/NO), fechas y territorio."
    )]
    public static async Task<Ok<SocialListeningDashboard>> GetSocialComments(
        ISender sender,
        string? platform,
        bool refresh = false,
        string? topic = null,
        string? prompt = null,
        string? instagramUrl = null,
        string? facebookUrl = null,
        string? tiktokUrl = null,
        string? xUrl = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        string? province = null,
        string? canton = null,
        string? parish = null
    )
    {
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddSource(sources, "instagram", instagramUrl);
        AddSource(sources, "facebook", facebookUrl);
        AddSource(sources, "tiktok", tiktokUrl);
        AddSource(sources, "x", xUrl);
        var dashboard = await sender.Send(
            new GetSocialCommentsQuery(
                platform,
                refresh,
                topic,
                prompt,
                sources,
                fromDate,
                toDate,
                province,
                canton,
                parish
            )
        );
        return TypedResults.Ok(dashboard);
    }

    [EndpointSummary("Catálogo territorial de Manabí")]
    public static Ok<TerritoryCatalogResponse> GetTerritoryCatalog()
    {
        var cantons = ManabiTerritoryCatalog.Cantons
            .Select(c => new TerritoryCantonDto(c.Name, c.Parishes))
            .ToArray();

        return TypedResults.Ok(
            new TerritoryCatalogResponse(ManabiTerritoryCatalog.Province, cantons)
        );
    }

    [EndpointSummary("Exportar resultados de escucha social")]
    [EndpointDescription("Exporta CSV, Excel (SpreadsheetML) o un informe HTML imprimible (PDF).")]
    public static async Task<Results<FileContentHttpResult, BadRequest<string>>> ExportSocialComments(
        ISender sender,
        string format = "csv",
        string? platform = null,
        string? topic = null,
        string? prompt = null,
        string? instagramUrl = null,
        string? facebookUrl = null,
        string? tiktokUrl = null,
        string? xUrl = null,
        DateTimeOffset? fromDate = null,
        DateTimeOffset? toDate = null,
        string? province = null,
        string? canton = null,
        string? parish = null
    )
    {
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddSource(sources, "instagram", instagramUrl);
        AddSource(sources, "facebook", facebookUrl);
        AddSource(sources, "tiktok", tiktokUrl);
        AddSource(sources, "x", xUrl);

        var dashboard = await sender.Send(
            new GetSocialCommentsQuery(
                platform,
                false,
                topic,
                prompt,
                sources,
                fromDate,
                toDate,
                province,
                canton,
                parish
            )
        );

        var normalized = format.Trim().ToLowerInvariant();
        return normalized switch
        {
            "csv" => TypedResults.File(
                System.Text.Encoding.UTF8.GetPreamble()
                    .Concat(System.Text.Encoding.UTF8.GetBytes(SocialCommentsExport.ToCsv(dashboard)))
                    .ToArray(),
                "text/csv; charset=utf-8",
                $"sentia-observatorio-{DateTime.UtcNow:yyyyMMddHHmmss}.csv"
            ),
            "excel" or "xlsx" or "xls" => TypedResults.File(
                System.Text.Encoding.UTF8.GetBytes(SocialCommentsExport.ToExcelXml(dashboard)),
                "application/vnd.ms-excel",
                $"sentia-observatorio-{DateTime.UtcNow:yyyyMMddHHmmss}.xls"
            ),
            "pdf" or "html" => TypedResults.File(
                System.Text.Encoding.UTF8.GetBytes(SocialCommentsExport.ToPrintableHtml(dashboard, topic)),
                "text/html; charset=utf-8",
                $"sentia-observatorio-{DateTime.UtcNow:yyyyMMddHHmmss}.html"
            ),
            _ => TypedResults.BadRequest("Formato no soportado. Use csv, excel o pdf."),
        };
    }

    private static void AddSource(IDictionary<string, string> sources, string platform, string? url)
    {
        if (!string.IsNullOrWhiteSpace(url))
            sources[platform] = url.Trim();
    }
}

public record TerritoryCatalogResponse(string Province, IReadOnlyList<TerritoryCantonDto> Cantons);

public record TerritoryCantonDto(string Name, IReadOnlyList<string> Parishes);

internal static class SocialCommentsExport
{
    public static string ToCsv(SocialListeningDashboard dashboard)
    {
        var rows = new List<string>
        {
            "Red,Usuario,Comentario,Publicacion,URL_comentario,URL_publicacion,Sentimiento,Confianza,Provincia,Canton,Parroquia,Reacciones,Temas,Entidades,Emociones,Fecha"
        };

        foreach (var c in dashboard.Comments)
        {
            rows.Add(
                string.Join(
                    ",",
                    Csv(c.Platform),
                    Csv(c.AuthorName),
                    Csv(c.Text),
                    Csv(c.PostTitle),
                    Csv(c.CommentUrl),
                    Csv(c.PostUrl),
                    Csv(c.Sentiment.DisplayName),
                    c.Sentiment.Confidence,
                    Csv(c.Province),
                    Csv(c.Canton),
                    Csv(c.Parish),
                    c.ReactionCount,
                    Csv(string.Join("|", c.Topics ?? [])),
                    Csv(string.Join("|", c.Entities ?? [])),
                    Csv(string.Join("|", c.Emotions ?? [])),
                    Csv(c.PublishedAt.ToString("o"))
                )
            );
        }

        rows.Add("");
        rows.Add(Csv("Resumen ejecutivo"));
        rows.Add(Csv(dashboard.Insights?.ExecutiveSummary ?? ""));
        return string.Join("\n", rows);
    }

    public static string ToExcelXml(SocialListeningDashboard dashboard)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("""<?xml version="1.0"?>""");
        sb.AppendLine("""<?mso-application progid="Excel.Sheet"?>""");
        sb.AppendLine("""<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet" xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">""");
        sb.AppendLine("<Worksheet ss:Name=\"Opiniones\"><Table>");
        sb.AppendLine(
            "<Row><Cell><Data ss:Type=\"String\">Red</Data></Cell><Cell><Data ss:Type=\"String\">Usuario</Data></Cell><Cell><Data ss:Type=\"String\">Comentario</Data></Cell><Cell><Data ss:Type=\"String\">Sentimiento</Data></Cell><Cell><Data ss:Type=\"String\">Confianza</Data></Cell><Cell><Data ss:Type=\"String\">Provincia</Data></Cell><Cell><Data ss:Type=\"String\">Canton</Data></Cell><Cell><Data ss:Type=\"String\">Parroquia</Data></Cell><Cell><Data ss:Type=\"String\">URL</Data></Cell><Cell><Data ss:Type=\"String\">Fecha</Data></Cell></Row>"
        );

        foreach (var c in dashboard.Comments)
        {
            sb.Append("<Row>");
            sb.Append(Cell(c.Platform));
            sb.Append(Cell(c.AuthorName));
            sb.Append(Cell(c.Text));
            sb.Append(Cell(c.Sentiment.DisplayName));
            sb.Append(CellNumber(c.Sentiment.Confidence));
            sb.Append(Cell(c.Province));
            sb.Append(Cell(c.Canton));
            sb.Append(Cell(c.Parish));
            sb.Append(Cell(c.PostUrl));
            sb.Append(Cell(c.PublishedAt.ToString("yyyy-MM-dd HH:mm")));
            sb.AppendLine("</Row>");
        }

        sb.AppendLine("</Table></Worksheet>");
        sb.AppendLine("<Worksheet ss:Name=\"Resumen\"><Table>");
        sb.AppendLine("<Row><Cell><Data ss:Type=\"String\">Resumen ejecutivo</Data></Cell></Row>");
        sb.Append("<Row>").Append(Cell(dashboard.Insights?.ExecutiveSummary ?? "")).AppendLine("</Row>");
        sb.AppendLine(
            $"<Row><Cell><Data ss:Type=\"String\">Total</Data></Cell><Cell><Data ss:Type=\"Number\">{dashboard.Summary.Total}</Data></Cell></Row>"
        );
        sb.AppendLine(
            $"<Row><Cell><Data ss:Type=\"String\">Positivos</Data></Cell><Cell><Data ss:Type=\"Number\">{dashboard.Summary.Positive}</Data></Cell></Row>"
        );
        sb.AppendLine(
            $"<Row><Cell><Data ss:Type=\"String\">Neutrales</Data></Cell><Cell><Data ss:Type=\"Number\">{dashboard.Summary.Neutral}</Data></Cell></Row>"
        );
        sb.AppendLine(
            $"<Row><Cell><Data ss:Type=\"String\">Negativos</Data></Cell><Cell><Data ss:Type=\"Number\">{dashboard.Summary.Negative}</Data></Cell></Row>"
        );
        sb.AppendLine("</Table></Worksheet></Workbook>");
        return sb.ToString();
    }

    public static string ToPrintableHtml(SocialListeningDashboard dashboard, string? topic)
    {
        var summary = dashboard.Insights?.ExecutiveSummary ?? "";
        var rows = string.Join(
            "",
            dashboard.Comments.Select(c =>
                $"<tr><td>{Html(c.Platform)}</td><td>{Html(c.AuthorName)}</td><td>{Html(c.Text)}</td><td>{Html(c.Sentiment.DisplayName)} ({c.Sentiment.Confidence}%)</td><td>{Html(c.Canton ?? c.Province)}</td><td><a href=\"{Html(c.PostUrl)}\">Ver publicación</a></td><td>{c.PublishedAt:yyyy-MM-dd}</td></tr>"
            )
        );

        return string.Concat(
            "<!doctype html><html lang=\"es\"><head><meta charset=\"utf-8\"><title>Informe Observatorio Territorial</title>",
            "<style>body{font-family:Segoe UI,Arial,sans-serif;margin:2rem;color:#1b1b1b}",
            "h1,h2{color:#0f4c81}table{border-collapse:collapse;width:100%;margin-top:1rem}",
            "th,td{border:1px solid #d0d0d0;padding:.45rem;font-size:.9rem;vertical-align:top}",
            "th{background:#eef5fb;text-align:left}.summary{background:#f7fafc;padding:1rem;border-radius:8px}",
            "@media print{button{display:none}}</style></head><body>",
            "<button onclick=\"window.print()\">Imprimir / Guardar PDF</button>",
            "<h1>Observatorio Territorial · Análisis de sentimientos</h1>",
            $"<p><strong>Tema:</strong> {Html(topic ?? "General")} · <strong>Actualizado:</strong> {dashboard.UpdatedAt:yyyy-MM-dd HH:mm} UTC</p>",
            $"<div class=\"summary\"><h2>Resumen ejecutivo</h2><p>{Html(summary)}</p>",
            $"<p>Total {dashboard.Summary.Total} · Positivos {dashboard.Summary.Positive} · Neutrales {dashboard.Summary.Neutral} · Negativos {dashboard.Summary.Negative}</p></div>",
            "<h2>Opiniones</h2><table><thead><tr><th>Red</th><th>Usuario</th><th>Comentario</th><th>Sentimiento</th><th>Territorio</th><th>Origen</th><th>Fecha</th></tr></thead>",
            $"<tbody>{rows}</tbody></table>",
            "<script>window.addEventListener('load',function(){setTimeout(function(){window.print();},400);});</script>",
            "</body></html>"
        );
    }

    private static string Csv(string? value) =>
        $"\"{(value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string Cell(string? value) =>
        $"<Cell><Data ss:Type=\"String\">{System.Security.SecurityElement.Escape(value ?? string.Empty)}</Data></Cell>";

    private static string CellNumber(int value) =>
        $"<Cell><Data ss:Type=\"Number\">{value}</Data></Cell>";

    private static string Html(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
