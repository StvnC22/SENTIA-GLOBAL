using AnalisisSentimiento.Application.Monitoring.Commands.LogActivity;
using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;
using AnalisisSentimiento.Application.SocialListening.Territory;
using AnalisisSentimiento.Web.Reports;
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
        var cantons = ManabiTerritoryCatalog
            .Cantons.Select(c => new TerritoryCantonDto(c.Name, c.Parishes))
            .ToArray();

        return TypedResults.Ok(
            new TerritoryCatalogResponse(ManabiTerritoryCatalog.Province, cantons)
        );
    }

    [EndpointSummary("Exportar resultados de escucha social")]
    [EndpointDescription("Exporta CSV, Excel (.xlsx) o un informe PDF con resumen ejecutivo.")]
    public static async Task<
        Results<FileContentHttpResult, BadRequest<string>>
    > ExportSocialComments(
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

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var normalized = format.Trim().ToLowerInvariant();
        FileContentHttpResult? file = normalized switch
        {
            "csv" => TypedResults.File(
                System
                    .Text.Encoding.UTF8.GetPreamble()
                    .Concat(
                        System.Text.Encoding.UTF8.GetBytes(SocialCommentsExport.ToCsv(dashboard))
                    )
                    .ToArray(),
                "text/csv; charset=utf-8",
                $"sentia-observatorio-{stamp}.csv"
            ),
            "excel" or "xlsx" or "xls" => TypedResults.File(
                SocialCommentsExport.ToXlsx(dashboard, topic),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"sentia-observatorio-{stamp}.xlsx"
            ),
            "pdf" => TypedResults.File(
                SocialCommentsExport.ToPdf(dashboard, topic, fromDate, toDate, canton ?? province),
                "application/pdf",
                $"sentia-observatorio-{stamp}.pdf"
            ),
            _ => null,
        };

        if (file is null)
        {
            return TypedResults.BadRequest("Formato no soportado. Use csv, excel o pdf.");
        }

        await sender.Send(
            new LogActivityCommand(
                "report.exported",
                $"Exportación {normalized.ToUpperInvariant()} · tema «{topic ?? "general"}» · {dashboard.Summary.Total} opiniones"
            )
        );
        return file;
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
            "Red,Usuario,Comentario,Publicacion,URL_comentario,URL_publicacion,Sentimiento,Confianza,Provincia,Canton,Parroquia,Reacciones,Temas,Aspectos,Entidades,Emociones,Fecha",
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
                    Csv(FormatAspects(c)),
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

    public static byte[] ToXlsx(SocialListeningDashboard dashboard, string? topic)
    {
        var opinions = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                "Red",
                "Usuario",
                "Comentario",
                "Publicación",
                "Sentimiento",
                "Confianza %",
                "Provincia",
                "Cantón",
                "Parroquia",
                "Reacciones",
                "Temas",
                "Aspectos",
                "Emociones",
                "Entidades",
                "Fecha",
                "Origen",
            },
        };
        opinions.AddRange(
            dashboard.Comments.Select(c =>
                (IReadOnlyList<object?>)
                    new object?[]
                    {
                        NetworkName(c.Platform),
                        c.AuthorName,
                        c.Text,
                        c.PostTitle,
                        c.Sentiment.DisplayName,
                        c.Sentiment.Confidence,
                        c.Province,
                        c.Canton,
                        c.Parish,
                        c.ReactionCount,
                        string.Join(", ", c.Topics ?? []),
                        FormatAspects(c),
                        string.Join(", ", c.Emotions ?? []),
                        string.Join(", ", c.Entities ?? []),
                        EcuadorTime(c.PublishedAt, "yyyy-MM-dd HH:mm"),
                        new XlsxLink(
                            string.IsNullOrWhiteSpace(c.CommentUrl) ? c.PostUrl : c.CommentUrl,
                            "Ver publicación"
                        ),
                    }
            )
        );

        var insights = dashboard.Insights;
        var summary = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Indicador", "Valor" },
            new object?[] { "Tema", string.IsNullOrWhiteSpace(topic) ? "General" : topic },
            new object?[]
            {
                "Generado (UTC)",
                dashboard.UpdatedAt.ToString("yyyy-MM-dd HH:mm", Invariant),
            },
            new object?[]
            {
                "Fuente de datos",
                dashboard.DataSource == "live" ? "API (datos reales)" : "Demostrativa",
            },
            new object?[] { "Total opiniones", dashboard.Summary.Total },
            new object?[] { "Positivas", dashboard.Summary.Positive },
            new object?[] { "Neutrales", dashboard.Summary.Neutral },
            new object?[] { "Negativas", dashboard.Summary.Negative },
            new object?[] { "Reacciones totales", insights?.TotalReactions ?? 0 },
            new object?[] { "Resumen ejecutivo", insights?.ExecutiveSummary ?? "" },
        };

        var networks = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                "Red",
                "Total",
                "Positivos",
                "Neutrales",
                "Negativos",
                "% negativo",
                "Reacciones",
            },
        };
        networks.AddRange(
            (insights?.NetworkBreakdown ?? []).Select(n =>
                (IReadOnlyList<object?>)
                    new object?[]
                    {
                        NetworkName(n.Platform),
                        n.Total,
                        n.Positive,
                        n.Neutral,
                        n.Negative,
                        Pct(n.Negative, n.Total),
                        n.Reactions,
                    }
            )
        );

        var aspects = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Aspecto", "Menciones", "Positivos", "Neutrales", "Negativos" },
        };
        aspects.AddRange(
            (insights?.AspectBreakdown ?? []).Select(a =>
                (IReadOnlyList<object?>)
                    new object?[] { a.Aspect, a.Mentions, a.Positive, a.Neutral, a.Negative }
            )
        );

        var territory = new List<IReadOnlyList<object?>>
        {
            new object?[]
            {
                "Provincia",
                "Cantón",
                "Parroquia",
                "Menciones",
                "Positivos",
                "Neutrales",
                "Negativos",
            },
        };
        territory.AddRange(
            (insights?.GeoRanking ?? []).Select(g =>
                (IReadOnlyList<object?>)
                    new object?[]
                    {
                        g.Province,
                        g.Canton,
                        g.Parish,
                        g.Mentions,
                        g.Positive,
                        g.Neutral,
                        g.Negative,
                    }
            )
        );

        var trend = new List<IReadOnlyList<object?>>
        {
            new object?[] { "Fecha", "Total", "Positivos", "Neutrales", "Negativos" },
        };
        trend.AddRange(
            (insights?.TemporalTrend ?? []).Select(t =>
                (IReadOnlyList<object?>)
                    new object?[]
                    {
                        t.Date.ToString("yyyy-MM-dd", Invariant),
                        t.Total,
                        t.Positive,
                        t.Neutral,
                        t.Negative,
                    }
            )
        );

        return XlsxWorkbookWriter.Build([
            new XlsxSheet("Resumen", summary, [26, 110]),
            new XlsxSheet(
                "Opiniones",
                opinions,
                [11, 22, 60, 30, 12, 11, 11, 14, 14, 11, 22, 30, 18, 22, 17, 18]
            ),
            new XlsxSheet("Por red", networks, [14, 10, 11, 11, 11, 11, 12]),
            new XlsxSheet("Aspectos", aspects, [20, 12, 11, 11, 11]),
            new XlsxSheet("Territorio", territory, [14, 16, 16, 12, 11, 11, 11]),
            new XlsxSheet("Tendencia", trend, [12, 10, 11, 11, 11]),
        ]);
    }

    public static byte[] ToPdf(
        SocialListeningDashboard dashboard,
        string? topic,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        string? territory
    )
    {
        var pdf = new PdfDocumentWriter();
        var insights = dashboard.Insights;
        var period =
            fromDate is null && toDate is null
                ? "todo el periodo disponible"
                : $"{fromDate?.ToString("dd/MM/yyyy", Invariant) ?? "inicio"} – {toDate?.ToString("dd/MM/yyyy", Invariant) ?? "hoy"}";

        pdf.Title("Informe del Observatorio Territorial · Análisis de sentimientos");
        pdf.Paragraph(
            $"Tema: {(string.IsNullOrWhiteSpace(topic) ? "General" : topic)} · Territorio: {territory ?? "Todos"} · Periodo: {period}",
            9.5
        );
        pdf.Paragraph(
            $"Generado: {EcuadorTime(dashboard.UpdatedAt, "dd/MM/yyyy HH:mm")} (Ecuador) · Fuente: {(dashboard.DataSource == "live" ? "API de redes sociales" : "datos demostrativos")}",
            9.5,
            color: (0.35, 0.35, 0.4)
        );

        pdf.Heading("Resumen ejecutivo");
        pdf.Paragraph(insights?.ExecutiveSummary ?? "Sin datos.", 10.5);

        var total = dashboard.Summary.Total;
        pdf.Heading("Sentimiento global");
        pdf.Paragraph(
            $"Total {total} opiniones · Positivas {dashboard.Summary.Positive} ({Pct(dashboard.Summary.Positive, total)}%) · Neutrales {dashboard.Summary.Neutral} ({Pct(dashboard.Summary.Neutral, total)}%) · Negativas {dashboard.Summary.Negative} ({Pct(dashboard.Summary.Negative, total)}%) · Reacciones {insights?.TotalReactions ?? 0}",
            10
        );
        pdf.StackedBar(
            "Global",
            dashboard.Summary.Positive,
            dashboard.Summary.Neutral,
            dashboard.Summary.Negative
        );

        if (insights?.NetworkBreakdown is { Count: > 0 } networks)
        {
            pdf.Heading("Sentimiento por red social");
            foreach (var network in networks)
            {
                pdf.StackedBar(
                    $"{NetworkName(network.Platform)} ({network.Total})",
                    network.Positive,
                    network.Neutral,
                    network.Negative
                );
            }
        }

        if (insights?.AspectBreakdown is { Count: > 0 } aspects)
        {
            pdf.Heading("Análisis por aspecto");
            foreach (var aspect in aspects)
            {
                pdf.StackedBar(
                    $"{aspect.Aspect} ({aspect.Mentions})",
                    aspect.Positive,
                    aspect.Neutral,
                    aspect.Negative
                );
            }
        }

        if (insights?.GeoRanking is { Count: > 0 } geo)
        {
            pdf.Heading("Ranking territorial");
            foreach (var row in geo)
            {
                var label = string.Join(
                    " / ",
                    new[]
                    {
                        row.Canton ?? row.Province,
                        row.Parish == row.Canton ? null : row.Parish,
                    }.Where(x => !string.IsNullOrWhiteSpace(x))
                );
                pdf.StackedBar(
                    $"{label} ({row.Mentions})",
                    row.Positive,
                    row.Neutral,
                    row.Negative
                );
            }
        }

        if (insights?.TemporalTrend is { Count: > 0 } trend)
        {
            pdf.Heading("Tendencia temporal");
            foreach (var point in trend.TakeLast(20))
            {
                pdf.StackedBar(
                    $"{point.Date.ToString("dd/MM/yyyy", Invariant)} ({point.Total})",
                    point.Positive,
                    point.Neutral,
                    point.Negative
                );
            }
        }

        if (insights is not null)
        {
            pdf.Heading("Temas, emociones y entidades");
            if (insights.WordCloud.Count > 0)
            {
                pdf.Paragraph(
                    "Términos frecuentes: "
                        + string.Join(
                            ", ",
                            insights.WordCloud.Take(20).Select(t => $"{t.Term} ({t.Count})")
                        ),
                    9.5
                );
            }

            if (insights.EmotionBreakdown.Count > 0)
            {
                pdf.Paragraph(
                    "Emociones: "
                        + string.Join(
                            ", ",
                            insights.EmotionBreakdown.Select(e => $"{e.Label} ({e.Count})")
                        ),
                    9.5
                );
            }

            if (insights.TopEntities.Count > 0)
            {
                pdf.Paragraph(
                    "Entidades: "
                        + string.Join(
                            ", ",
                            insights.TopEntities.Select(e => $"{e.Label} ({e.Count})")
                        ),
                    9.5
                );
            }
        }

        pdf.Heading($"Opiniones analizadas ({dashboard.Comments.Count})");
        foreach (var c in dashboard.Comments.Take(300))
        {
            var place = string.Join(
                " · ",
                new[] { c.Canton, c.Parish }.Where(x => !string.IsNullOrWhiteSpace(x))
            );
            pdf.Paragraph(
                $"{NetworkName(c.Platform)} · {c.AuthorName} · {c.Sentiment.DisplayName} ({c.Sentiment.Confidence}%) · {EcuadorTime(c.PublishedAt, "dd/MM/yyyy HH:mm")}"
                    + (string.IsNullOrWhiteSpace(place) ? "" : $" · {place}")
                    + (c.ReactionCount > 0 ? $" · {c.ReactionCount} reacciones" : ""),
                8.5,
                bold: true,
                spacingAfter: 0
            );
            pdf.Paragraph(c.Text, 9, spacingAfter: 0, indent: 8);
            pdf.Link(
                "Ver publicación de origen: "
                    + (string.IsNullOrWhiteSpace(c.CommentUrl) ? c.PostUrl : c.CommentUrl),
                string.IsNullOrWhiteSpace(c.CommentUrl) ? c.PostUrl : c.CommentUrl,
                8,
                indent: 8
            );
            pdf.Spacer(4);
        }

        return pdf.Build();
    }

    private static readonly System.Globalization.CultureInfo Invariant = System
        .Globalization
        .CultureInfo
        .InvariantCulture;

    private static string EcuadorTime(DateTimeOffset value, string format) =>
        value.ToOffset(TimeSpan.FromHours(-5)).ToString(format, Invariant);

    private static string FormatAspects(SocialComment comment) =>
        string.Join(
            "|",
            (comment.Aspects ?? []).Select(a =>
                $"{a.Aspect}:{a.Label switch { "positive" => "positivo", "negative" => "negativo", _ => "neutral" }}"
            )
        );

    private static string NetworkName(string platform) =>
        platform.ToLowerInvariant() switch
        {
            "instagram" => "Instagram",
            "facebook" => "Facebook",
            "tiktok" => "TikTok",
            "x" => "X",
            _ => platform,
        };

    private static int Pct(int value, int total) =>
        total == 0 ? 0 : (int)Math.Round(value * 100.0 / total);

    private static string Csv(string? value) =>
        $"\"{(value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
