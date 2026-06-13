namespace Argus.Styx.Security;

public class IngestOptions
{
    public const string SectionName = "Ingest";

    /// <summary>Allowlist of raw per-agent API keys. Remove a key here to revoke an agent.</summary>
    public List<string> ApiKeys { get; set; } = new();

    /// <summary>Precomputed hash set of the allowlisted keys.</summary>
    public HashSet<string> ApiKeyHashes =>
        ApiKeys.Select(ApiKeyHasher.Hash).ToHashSet();
}
