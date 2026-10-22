using ApryseCollaborationPoc.Models;
using ApryseCollaborationPoc.Services;
using Microsoft.AspNetCore.SignalR;

namespace ApryseCollaborationPoc.Hubs;

public sealed class CollaborationHub(IAnnotationRepository repository) : Hub
{
    private const string JoinedDocumentsKey = "joined-documents";
    private const int MaximumXfdfLength = 1024 * 1024;

    public async Task<IReadOnlyCollection<AnnotationChange>> JoinDocument(string documentId)
    {
        ValidateIdentifier(documentId, nameof(documentId));
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(documentId));
        JoinedDocuments.Add(documentId);
        return await repository.GetDocumentAsync(documentId, Context.ConnectionAborted);
    }

    public async Task PublishAnnotation(AnnotationChange incoming)
    {
        ValidateIdentifier(incoming.DocumentId, nameof(incoming.DocumentId));
        ValidateIdentifier(incoming.AnnotationId, nameof(incoming.AnnotationId));

        if (!JoinedDocuments.Contains(incoming.DocumentId))
        {
            throw new HubException("Join the document before publishing annotation changes.");
        }

        if (incoming.Operation is not ("add" or "modify" or "delete"))
        {
            throw new HubException("Operation must be add, modify, or delete.");
        }

        if (string.IsNullOrWhiteSpace(incoming.Xfdf) || incoming.Xfdf.Length > MaximumXfdfLength)
        {
            throw new HubException("XFDF must be between 1 byte and 1 MB.");
        }

        var authorId = Context.GetHttpContext()?.Request.Query["authorId"].ToString();
        if (string.IsNullOrWhiteSpace(authorId))
        {
            throw new HubException("An authorId is required for this POC.");
        }

        var persisted = await repository.ApplyAsync(
            incoming with { AuthorId = authorId },
            Context.ConnectionAborted);
        await Clients.OthersInGroup(GroupName(incoming.DocumentId))
            .SendAsync("AnnotationChanged", persisted, Context.ConnectionAborted);
    }

    private HashSet<string> JoinedDocuments
    {
        get
        {
            if (Context.Items.TryGetValue(JoinedDocumentsKey, out var value) && value is HashSet<string> documents)
            {
                return documents;
            }

            var newDocuments = new HashSet<string>(StringComparer.Ordinal);
            Context.Items[JoinedDocumentsKey] = newDocuments;
            return newDocuments;
        }
    }

    private static string GroupName(string documentId) => $"document:{documentId}";

    private static void ValidateIdentifier(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 200)
        {
            throw new HubException($"{name} must be between 1 and 200 characters.");
        }
    }
}