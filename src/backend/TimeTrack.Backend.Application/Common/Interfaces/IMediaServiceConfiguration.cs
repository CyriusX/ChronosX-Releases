namespace TimeTrack.Backend.Application.Common.Interfaces;

public interface IMediaServiceConfiguration
{
    string BaseUrl { get; }
    string ApiKey { get; }
    int RequestTimeoutSeconds { get; }
}
