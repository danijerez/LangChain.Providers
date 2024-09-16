// ReSharper disable once CheckNamespace
namespace LangChain.Providers;

#pragma warning disable CA2225

/// <summary>
/// 
/// </summary>
public class ChatToolCall
{
    /// <summary>
    /// The ID of the tool call.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The name of the tool to call.
    /// </summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>
    /// The arguments to call the function with, as generated by the model usually in JSON format.
    /// Note that the model does not always generate valid JSON,
    /// and may hallucinate parameters not defined by your function schema.
    /// Validate the arguments in your code before calling your function.
    /// </summary>
    public string ToolArguments { get; set; } = string.Empty;
}