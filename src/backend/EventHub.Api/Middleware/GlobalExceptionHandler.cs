using EventHub.Application.Exceptions;
using EventHub.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EventHub.Api.Middleware;

internal sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (statusCode, title, errors) = exception switch
        {
            NotFoundException ex => (
                StatusCodes.Status404NotFound,
                ex.Message,
                null),

            ForbiddenException ex => (
                StatusCodes.Status403Forbidden,
                ex.Message,
                null),

            DomainException ex => (
                StatusCodes.Status409Conflict,
                ex.Message,
                null),

            ValidationException ex => (
                StatusCodes.Status422UnprocessableEntity,
                "One or more validation errors occurred.",
                (IDictionary<string, string[]>?)ex.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray())),

            BadHttpRequestException ex => (
                ex.StatusCode,
                ex.Message,
                null),

            // Row was deleted or modified by another process between load and save.
            // The client should re-fetch and retry.
            DbUpdateConcurrencyException => (
                StatusCodes.Status409Conflict,
                "The resource was modified or deleted by another request. Please refresh and try again.",
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.",
                null)
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title
        };

        if (errors is not null)
        {
            problemDetails.Extensions["errors"] = errors;
        }

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
