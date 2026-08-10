using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Models.Messages;

namespace Huddle.Scenarios;

/// <summary>
/// The metered path: builds the SDK request and calls <c>Messages.Create</c>.
/// Enforces the <see cref="NudgeDraft"/> schema via structured output, and when
/// the request asks for effort, applies both the effort level and adaptive
/// thinking — reproducing the high-reasoning scenarios exactly.
/// </summary>
internal sealed class ApiBackend : IScenarioBackend
{
    private readonly AnthropicClient _client = new();

    public async Task<BackendResult> CompleteAsync(ScenarioRequest request, CancellationToken ct)
    {
        var format = new JsonOutputFormat { Schema = request.JsonSchema };
        // Effort and adaptive thinking are init-only, so build them into the
        // initializers. Effort scenarios (LinkedIn) also run adaptive thinking —
        // off by default on Opus 4.8 when omitted — so the two travel together.
        var outputConfig = request.Effort is { } effort
            ? new OutputConfig { Format = format, Effort = effort }
            : new OutputConfig { Format = format };

        var parameters = new MessageCreateParams
        {
            Model = request.Model,
            MaxTokens = request.MaxTokens,
            System = request.SystemPrompt,
            OutputConfig = outputConfig,
            Thinking = request.Effort is not null ? new ThinkingConfigAdaptive() : null,
            Messages = new List<MessageParam>
            {
                new()
                {
                    Role = Role.User,
                    Content = new List<ContentBlockParam>
                    {
                        new TextBlockParam { Text = request.UserText },
                    },
                },
            },
        };

        Message response = await _client.Messages.Create(parameters, cancellationToken: ct)
            .ConfigureAwait(false);

        string? text = response.Content
            .Select(b => b.Value)
            .OfType<TextBlock>()
            .Select(t => t.Text)
            .FirstOrDefault();

        return new BackendResult(text, response.Usage?.InputTokens, response.Usage?.OutputTokens);
    }
}
