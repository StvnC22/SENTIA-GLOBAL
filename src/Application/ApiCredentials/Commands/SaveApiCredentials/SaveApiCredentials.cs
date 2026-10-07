using AnalisisSentimiento.Application.ApiCredentials.Queries.GetApiCredentialStatus;
using AnalisisSentimiento.Application.Common.Interfaces;

namespace AnalisisSentimiento.Application.ApiCredentials.Commands.SaveApiCredentials;

public record SaveApiCredentialsCommand(
    string? Instagram,
    string? Facebook,
    string? TikTok,
    string? X,
    string? ArtificialIntelligence
) : ICommand<ApiCredentialStatus>
{
    public override string ToString() =>
        $"{nameof(SaveApiCredentialsCommand)} {{ Credentials = [REDACTED] }}";
}

public class SaveApiCredentialsCommandValidator : AbstractValidator<SaveApiCredentialsCommand>
{
    public SaveApiCredentialsCommandValidator()
    {
        RuleFor(x => x.Instagram).MaximumLength(4096);
        RuleFor(x => x.Facebook).MaximumLength(4096);
        RuleFor(x => x.TikTok).MaximumLength(4096);
        RuleFor(x => x.X).MaximumLength(4096);
        RuleFor(x => x.ArtificialIntelligence).MaximumLength(4096);
    }
}

internal class SaveApiCredentialsCommandHandler(
    IApiCredentialStore credentialStore,
    TimeProvider timeProvider
) : IRequestHandler<SaveApiCredentialsCommand, Result<ApiCredentialStatus>>
{
    public async Task<Result<ApiCredentialStatus>> Handle(
        SaveApiCredentialsCommand request,
        CancellationToken cancellationToken
    )
    {
        var current = await credentialStore.GetAsync(cancellationToken);
        var updated = current with
        {
            Instagram = KeepCurrentWhenBlank(request.Instagram, current.Instagram),
            Facebook = KeepCurrentWhenBlank(request.Facebook, current.Facebook),
            TikTok = KeepCurrentWhenBlank(request.TikTok, current.TikTok),
            X = KeepCurrentWhenBlank(request.X, current.X),
            ArtificialIntelligence = KeepCurrentWhenBlank(
                request.ArtificialIntelligence,
                current.ArtificialIntelligence
            ),
            UpdatedAt = timeProvider.GetUtcNow(),
        };

        try
        {
            await credentialStore.SaveAsync(updated, cancellationToken);
            return Result.Ok(ApiCredentialStatus.From(updated));
        }
        catch (Exception exception)
        {
            return Result.Fail(
                $"No se pudieron guardar las credenciales en este equipo. {exception.Message}"
            );
        }
    }

    private static string? KeepCurrentWhenBlank(string? candidate, string? current) =>
        string.IsNullOrWhiteSpace(candidate) || LooksLikeMaskedPlaceholder(candidate)
            ? current
            : candidate.Trim();

    private static bool LooksLikeMaskedPlaceholder(string value) =>
        value.Contains('•', StringComparison.Ordinal) || value.All(static c => c is '•' or '*');
}
