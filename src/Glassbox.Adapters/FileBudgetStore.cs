using System.Text.Json;
using Glassbox.Domain;

namespace Glassbox.Adapters;

public sealed class FileBudgetStore(string path) : IBudgetStore
{
    public async Task<BudgetDay?> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<BudgetDay>(stream, cancellationToken: ct).ConfigureAwait(false);
    }

    public async Task SaveAsync(BudgetDay day, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        await using (var stream = File.Create(tmp))
        {
            await JsonSerializer.SerializeAsync(stream, day, cancellationToken: ct).ConfigureAwait(false);
        }
        File.Move(tmp, path, overwrite: true);
    }
}
