﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Amazon.BedrockRuntime.Model;
using LangChain.Providers.Amazon.Bedrock.Internal;

// ReSharper disable once CheckNamespace
namespace LangChain.Providers.Amazon.Bedrock;

public class MetaLlamaChatModel(
    BedrockProvider provider,
    string id)
    : ChatModel(id)
{
    /// <summary>
    /// Generates a chat response based on the provided `ChatRequest`.
    /// </summary>
    /// <param name="request">The `ChatRequest` containing the input messages and other parameters.</param>
    /// <param name="settings">Optional `ChatSettings` to override the model's default settings.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A `ChatResponse` containing the generated messages and usage information.</returns>
    public override async IAsyncEnumerable<ChatResponse> GenerateAsync(
        ChatRequest request,
        ChatSettings? settings = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request = request ?? throw new ArgumentNullException(nameof(request));

        var watch = Stopwatch.StartNew();
        var prompt = request.Messages.ToSimplePrompt();
        var messages = request.Messages.ToList();

        var stringBuilder = new StringBuilder();

        var usedSettings = MetaLlama2ChatSettings.Calculate(
            requestSettings: settings,
            modelSettings: Settings,
            providerSettings: provider.ChatSettings);

        var bodyJson = CreateBodyJson(prompt, usedSettings);

        if (usedSettings.UseStreaming == true)
        {
            var streamRequest = BedrockModelRequest.CreateStreamRequest(Id, bodyJson);
            var response = await provider.Api.InvokeModelWithResponseStreamAsync(streamRequest, cancellationToken).ConfigureAwait(false);

            foreach (var payloadPart in response.Body)
            {
                var streamEvent = (PayloadPart)payloadPart;
                var chunk = await JsonSerializer.DeserializeAsync<JsonObject>(streamEvent.Bytes, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                var delta = chunk?["generation"]?.GetValue<string>() ?? string.Empty;

                OnDeltaReceived(new ChatResponseDelta
                {
                    Content = delta,
                });
                stringBuilder.Append(delta);

                var finished = chunk?["stop_reason"]?.GetValue<string>() ?? string.Empty;
                if (string.Equals(finished.ToUpperInvariant(), "STOP", StringComparison.Ordinal))
                {
                    messages.Add(stringBuilder.ToString().AsAiMessage());
                }
            }
        }
        else
        {
            var response = await provider.Api.InvokeModelAsync(Id, bodyJson, cancellationToken)
                .ConfigureAwait(false);

            var generatedText = response?["generation"]?
                .GetValue<string>() ?? string.Empty;

            messages.Add(generatedText.AsAiMessage());
        }

        var usage = Usage.Empty with
        {
            Time = watch.Elapsed,
        };
        AddUsage(usage);
        provider.AddUsage(usage);

        var chatResponse = new ChatResponse
        {
            Messages = messages,
            UsedSettings = usedSettings,
            Usage = usage,
        };
        OnResponseReceived(chatResponse);

        yield return chatResponse;
    }

    /// <summary>
    /// Creates the request body JSON for the Meta model based on the provided prompt and settings.
    /// </summary>
    /// <param name="prompt">The input prompt for the model.</param>
    /// <param name="usedSettings">The settings to use for the request.</param>
    /// <returns>A `JsonObject` representing the request body.</returns>
    private static JsonObject CreateBodyJson(string prompt, MetaLlama2ChatSettings usedSettings)
    {
        var bodyJson = new JsonObject
        {
            ["inputs"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = prompt
                }
            },
            ["parameters"] = new JsonObject
            {
                ["prompt"] = prompt,
                ["max_new_tokens"] = usedSettings.MaxTokens!.Value,
                ["temperature"] = usedSettings.Temperature!.Value,
                ["top_p"] = usedSettings.TopP!.Value,
            }
        };
        return bodyJson;
    }
}