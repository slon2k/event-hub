using EventHub.Application.Exceptions;
using EventHub.Domain.Common;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EventHub.Application.Behaviours;

/// <summary>
/// Logs the command/query name, execution duration, and exceptions.
/// Expected business-rule exceptions (domain, validation, not-found, forbidden, invalid token)
/// are logged at Warning level — they are handled by GlobalExceptionHandler and must not
/// appear as errors in CI logs. Only truly unexpected exceptions are logged at Error level.
/// </summary>
public sealed class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}", requestName);
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();
            logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms", requestName, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (IsExpectedException(ex))
                logger.LogWarning("{RequestName} raised a handled exception after {ElapsedMs}ms: {Message}",
                    requestName, sw.ElapsedMilliseconds, ex.Message);
            else
                logger.LogError(ex, "Unhandled exception in {RequestName} after {ElapsedMs}ms",
                    requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }

    private static bool IsExpectedException(Exception ex) => ex is
        DomainException or
        ValidationException or
        NotFoundException or
        ForbiddenException or
        InvalidTokenException;
}
