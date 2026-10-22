using System.Collections.Concurrent;
using ApryseCollaborationPoc.Models;

namespace ApryseCollaborationPoc.Services;

public sealed class InMemoryAnnotationRepository : IAnnotationRepository
{
    private readonly ConcurrentDictionary<(string DocumentId, string AnnotationId), AnnotationChange> annotations = new();
    private long revision;

    public Task<IReadOnlyCollection<AnnotationChange>> GetDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyCollection<AnnotationChange> result = annotations
            .Where(entry => entry.Key.DocumentId == documentId)
            .Select(entry => entry.Value)
            .OrderBy(change => change.Revision)
            .ToArray();
        return Task.FromResult(result);
    }

    public Task<AnnotationChange> ApplyAsync(
        AnnotationChange change,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var persistedChange = change with { Revision = Interlocked.Increment(ref revision) };
        var key = (change.DocumentId, change.AnnotationId);

        if (string.Equals(change.Operation, "delete", StringComparison.Ordinal))
        {
            annotations.TryRemove(key, out _);
        }
        else
        {
            annotations[key] = persistedChange;
        }

        return Task.FromResult(persistedChange);
    }
}