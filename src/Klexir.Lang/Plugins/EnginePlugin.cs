using Klexir.Engine;
using Klexir.Engine.Abstractions;
using MonadicSharp;

namespace Klexir.Lang.Plugins;

/// <summary>
/// Bridges <c>Klexir.Engine</c>'s real page-backed B+Tree into Klexir — the only plugin so far that outlives one
/// <c>klexir run</c>: <c>dbOpen</c> opens (or creates) a real file on disk, and data written with <c>dbPut</c> is
/// still there the next time a program opens the same path. Keys and values are both <c>Int</c> — <see cref="PagedBTree"/>
/// is keyed by <c>long</c> already, so unlike every other plugin here, no String/type marshaling is needed at all.
/// </summary>
public sealed class EnginePlugin : IKlexirPlugin, IAsyncDisposable
{
    /// <summary>Node capacity — fixed so a file this plugin created can always be reopened and decoded the same way.</summary>
    private const int MinDegree = 32;

    private IPageStore? _store;
    private BufferPool? _pool;
    private PagedBTree? _tree;

    public EnginePlugin()
    {
        Functions =
        [
            new KlexirNativeFunctionDef(
                "dbOpen",
                new FunctionType(KlexirType.String, KlexirType.Bool),
                async args =>
                {
                    var path = ((StringValue)args[0]).Value;
                    var isNewFile = !File.Exists(path) || new FileInfo(path).Length == 0;

                    var storeResult = await FilePageStore.OpenAsync(path);
                    if (storeResult.IsFailure)
                    {
                        return Result<KlexirValue>.Failure(storeResult.Error);
                    }

                    await DisposeCurrentAsync();
                    _store = storeResult.Value;
                    _pool = new BufferPool(_store, capacity: 64);

                    var treeResult = isNewFile
                        ? await PagedBTree.CreateAsync(_store, _pool, MinDegree)
                        : Result<PagedBTree>.Success(PagedBTree.Open(_store, _pool, MinDegree, new PageId(0)));

                    if (treeResult.IsFailure)
                    {
                        return Result<KlexirValue>.Failure(treeResult.Error);
                    }

                    _tree = treeResult.Value;
                    return Result<KlexirValue>.Success(new BoolValue(true));
                }),

            new KlexirNativeFunctionDef(
                "dbPut",
                new FunctionType(KlexirType.Int, new FunctionType(KlexirType.Int, new ResultType(KlexirType.Bool, KlexirType.String))),
                async args =>
                {
                    if (_tree is null)
                    {
                        return Result<KlexirValue>.Failure(Error.Create("no database open — call dbOpen first."));
                    }

                    var key = ((IntValue)args[0]).Value;
                    var value = ((IntValue)args[1]).Value;

                    // The tree is insert-only (no upsert) — writing an existing key is a real, expected domain
                    // outcome, not a plugin-infrastructure problem, so it surfaces as a Klexir-catchable Err rather
                    // than a hard evaluation failure (unlike "no database open", which is a caller mistake).
                    var inserted = await _tree.InsertAsync(key, value);
                    KlexirValue outcome = inserted.IsSuccess
                        ? new OkValue(new BoolValue(true))
                        : new ErrValue(new StringValue(inserted.Error.Message));

                    return Result<KlexirValue>.Success(outcome);
                }),

            new KlexirNativeFunctionDef(
                "dbGet",
                new FunctionType(KlexirType.Int, new ResultType(KlexirType.Int, KlexirType.String)),
                async args =>
                {
                    if (_tree is null)
                    {
                        return Result<KlexirValue>.Failure(Error.Create("no database open — call dbOpen first."));
                    }

                    var key = ((IntValue)args[0]).Value;

                    var lookup = await _tree.TryGetAsync(key);
                    if (lookup.IsFailure)
                    {
                        return Result<KlexirValue>.Failure(lookup.Error);
                    }

                    KlexirValue outcome = lookup.Value.Found
                        ? new OkValue(new IntValue(lookup.Value.Value))
                        : new ErrValue(new StringValue($"no value for key {key}"));

                    return Result<KlexirValue>.Success(outcome);
                }),
        ];
    }

    public string Name => "Engine";

    public IReadOnlyList<OpaqueType> Types => [];

    public IReadOnlyList<KlexirNativeFunctionDef> Functions { get; }

    public async ValueTask DisposeAsync() => await DisposeCurrentAsync();

    private async Task DisposeCurrentAsync()
    {
        if (_pool is not null)
        {
            await _pool.DisposeAsync();
        }

        if (_store is not null)
        {
            await _store.DisposeAsync();
        }

        _pool = null;
        _store = null;
        _tree = null;
    }
}
