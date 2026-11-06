namespace DmsDemo.Dms;

public sealed class JsonStore
{
    public string FilePath { get; }

    public JsonStore(string? filePath = null)
    {
        FilePath = filePath
            ?? Environment.GetEnvironmentVariable("DMS_DATA")
            ?? Path.Combine("data", "dms_data.json");
    }

    public bool Exists() => File.Exists(FilePath);

    public string? Load() => Exists() ? File.ReadAllText(FilePath) : null;

    public void Save(string json)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, FilePath, overwrite: true);
    }
}