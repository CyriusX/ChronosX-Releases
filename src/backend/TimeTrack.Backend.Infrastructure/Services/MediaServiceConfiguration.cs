using Microsoft.Extensions.Configuration;
using TimeTrack.Backend.Application.Common.Interfaces;

namespace TimeTrack.Backend.Infrastructure.Services;

public sealed class MediaServiceConfiguration : IMediaServiceConfiguration
{
    public string BaseUrl { get; }
    public string ApiKey { get; }
    public int RequestTimeoutSeconds { get; }

    public MediaServiceConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("MediaService");

        BaseUrl = section["BaseUrl"] ?? "http://localhost:3003";
        ApiKey = section["ApiKey"] ?? string.Empty;
        RequestTimeoutSeconds = section.GetValue("RequestTimeoutSeconds", 30);
    }
}
