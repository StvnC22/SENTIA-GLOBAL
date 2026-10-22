using System.Text.Json.Serialization;
using AnalisisSentimiento.Application.ApiCredentials.Commands.SaveApiCredentials;
using AnalisisSentimiento.Application.ApiCredentials.Queries.GetApiCredentialStatus;
using AnalisisSentimiento.Application.SocialListening.Queries.GetSocialComments;

namespace AnalisisSentimiento.Web.Serialization;

[JsonSerializable(typeof(SaveApiCredentialsCommand))]
[JsonSerializable(typeof(ApiCredentialStatus))]
[JsonSerializable(typeof(SocialListeningDashboard))]
[JsonSerializable(typeof(SocialComment))]
[JsonSerializable(typeof(SentimentResult))]
[JsonSerializable(typeof(SentimentSummary))]
[JsonSerializable(typeof(DashboardInsights))]
[JsonSerializable(typeof(TemporalTrendPoint))]
[JsonSerializable(typeof(GeoMentionRank))]
[JsonSerializable(typeof(WordCloudTerm))]
[JsonSerializable(typeof(NetworkSentiment))]
[JsonSerializable(typeof(AspectSummary))]
[JsonSerializable(typeof(AspectSentiment))]
[JsonSerializable(typeof(LabelCount))]
[JsonSerializable(typeof(AnalisisSentimiento.Web.Endpoints.TerritoryCatalogResponse))]
[JsonSerializable(typeof(AnalisisSentimiento.Web.Endpoints.TerritoryCantonDto))]
internal partial class AppJsonSerializerContext : JsonSerializerContext;
