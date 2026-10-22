using ApryseCollaborationPoc.Models;

namespace ApryseCollaborationPoc.Services;

public interface IAnnotationRepository
{
    Task<IReadOnlyCollection<AnnotationChange>> GetDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    Task<AnnotationChange> ApplyAsync(
        AnnotationChange change,
        CancellationToken cancellationToken = default);
}