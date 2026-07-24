using Fido2NetLib;
using RedisSample.Shared.Features.Statistics;
using RedisSample.Server.Api.Features.Identity.Services;

namespace RedisSample.Server.Api.Infrastructure.Services;

/// <summary>
/// https://devblogs.microsoft.com/dotnet/try-the-new-system-text-json-source-generator/
/// </summary>
[JsonSourceGenerationOptions(
  AllowTrailingCommas = true,
  PropertyNameCaseInsensitive = true,
  GenerationMode = JsonSourceGenerationMode.Default,
  DictionaryKeyPolicy = JsonKnownNamingPolicy.CamelCase,
  PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
)]
[JsonSerializable(typeof(NugetStatsDto))]
[JsonSerializable(typeof(GoogleRecaptchaVerificationResponse))]
[JsonSerializable(typeof(AuthenticatorResponse))]
public partial class ServerJsonContext : JsonSerializerContext
{
}
