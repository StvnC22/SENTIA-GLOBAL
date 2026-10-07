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
internal partial class AppJsonSerializerContext : JsonSerializerContext;
