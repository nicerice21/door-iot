using System.Text.Json;

namespace DoorMonitor.Gateway.Configuration;

/// <summary>設定ファイルの読み書き。壊れた設定ファイルでも起動を止めない。</summary>
public sealed class GatewaySettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _path;

    public GatewaySettingsStore(string? path = null)
    {
        _path = path ?? GatewaySettings.DefaultPath;
    }

    public string Path => _path;

    public GatewaySettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                return JsonSerializer.Deserialize<GatewaySettings>(json, JsonOptions) ?? new GatewaySettings();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // 設定が壊れていても既定値で起動し、設定画面から保存し直せるようにする。
        }

        var defaults = new GatewaySettings();
        TrySave(defaults);
        return defaults;
    }

    public void Save(GatewaySettings settings)
    {
        var directory = System.IO.Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private void TrySave(GatewaySettings settings)
    {
        try
        {
            Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // 保存できない環境でもメモリ上の既定値で動作を継続する。
        }
    }
}
