namespace UniConnect.RoutePlanning.Options;

public class PlanningRulesOptions
{
    public const string SectionName = "PlanningRules";

    /// <summary>When true and OpenAiApiKey is set, markdown is compiled with an LLM on save.</summary>
    public bool EnableAiCompiler { get; set; } = true;

    public string? OpenAiApiKey { get; set; }

    public string OpenAiModel { get; set; } = "gpt-4o-mini";

    public string OpenAiBaseUrl { get; set; } = "https://api.openai.com/v1/";

    /// <summary>When true and OpenAiApiKey is set, plan runs can request a plain-English summary.</summary>
    public bool EnablePlanExplanation { get; set; } = true;
}
