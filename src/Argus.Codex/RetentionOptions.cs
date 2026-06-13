namespace Argus.Codex;

public class RetentionOptions
{
    public const string SectionName = "Retention";

    private int _days = 7;

    /// <summary>Retention window in days. Clamped to [1, 30]; defaults to 7.</summary>
    public int Days
    {
        get => _days;
        set => _days = Math.Clamp(value, 1, 30);
    }
}
