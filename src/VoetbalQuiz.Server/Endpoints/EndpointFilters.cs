using VoetbalQuiz.Server.Services;
using VoetbalQuiz.Shared;

namespace VoetbalQuiz.Server.Endpoints;

/// <summary>Filters die alle endpointgroepen delen.</summary>
public static class EndpointFilters
{
    /// <summary>Een <see cref="QuizException"/> (een spelregel of een ongeldige invoer) wordt een 400 met de uitleg voor de speler.</summary>
    public static async ValueTask<object?> HandleQuizErrors(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (QuizException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }
    }
}
