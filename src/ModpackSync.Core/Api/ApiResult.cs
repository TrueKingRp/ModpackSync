namespace ModpackSync.Core.Api;

public sealed record ApiResult(
    bool Success,
    string Message);