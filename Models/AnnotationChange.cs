namespace ApryseCollaborationPoc.Models;

public sealed record AnnotationChange(
    string DocumentId,
    string AnnotationId,
    string Operation,
    string Xfdf,
    string AuthorId,
    long Revision = 0);