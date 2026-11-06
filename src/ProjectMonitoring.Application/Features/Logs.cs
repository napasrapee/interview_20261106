using System.Text.Json;
using FluentValidation;
using ProjectMonitoring.Application.Common;
using ProjectMonitoring.Domain;

namespace ProjectMonitoring.Application.Features;

public sealed record SystemLogDto(
    long Id,
    string Level,
    string EventType,
    string Message,
    string? HttpMethod,
    string? Path,
    int? StatusCode,
    int? DurationMs,
    string? TraceId,
    long? UserId,
    JsonElement? Details,
    DateTimeOffset CreatedAt);

public sealed record LogListQuery(
    string? Level = null,
    string? EventType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20) : PageQuery(Page, PageSize);

public interface ISystemLogRepository
{
    Task<PagedResult<SystemLog>> ListAsync(LogListQuery query, CancellationToken cancellationToken);
}

/// <summary>Best effort: a failure to write a log must never fail the request being logged.</summary>
public interface ISystemLogWriter
{
    Task WriteAsync(SystemLog log);
}

public sealed class LogListQueryValidator : AbstractValidator<LogListQuery>
{
    public LogListQueryValidator()
    {
        this.ApplyPageRules();
        RuleFor(query => query.Level)
            .Must(EnumCodes.IsValid<SystemLogLevel>)
            .WithMessage("Level must be info, warning or error.")
            .When(query => query.Level is not null);
        RuleFor(query => query.EventType).MaximumLength(50);
        RuleFor(query => query.To)
            .GreaterThanOrEqualTo(query => query.From)
            .When(query => query.From is not null && query.To is not null);
    }
}

public sealed class LogService(ISystemLogRepository logs, IValidator<LogListQuery> validator)
{
    public async Task<PagedResult<SystemLogDto>> ListAsync(LogListQuery query, CancellationToken cancellationToken)
    {
        await validator.EnsureValidAsync(query, cancellationToken);
        return (await logs.ListAsync(query, cancellationToken)).Map(log => new SystemLogDto(
            log.Id,
            log.Level.ToCode(),
            log.EventType,
            log.Message,
            log.HttpMethod,
            log.Path,
            log.StatusCode,
            log.DurationMs,
            log.TraceId,
            log.UserId,
            log.Details is null ? null : JsonDocument.Parse(log.Details).RootElement.Clone(),
            log.CreatedAt));
    }
}
