using System.Text.Json;

namespace Argus.Herald.LogShipping;

/// <summary>
/// Persists per-file tail offsets to a local JSON file so the agent resumes where it
/// left off after a restart without re-shipping or losing lines.
/// </summary>
public class CheckpointStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private Dictionary<string, long> _offsets;

    public CheckpointStore(string path)
    {
        _path = path;
        _offsets = Load(path);
    }

    public long Get(string file) => _offsets.TryGetValue(file, out var v) ? v : -1;

    public void Set(string file, long offset)
    {
        lock (_gate)
        {
            _offsets[file] = offset;
            Persist();
        }
    }

    private static Dictionary<string, long> Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<Dictionary<string, long>>(File.ReadAllText(path)) ?? new();
        }
        catch { /* corrupt/missing -> start fresh */ }
        return new();
    }

    private void Persist()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(_path, JsonSerializer.Serialize(_offsets));
    }
}
