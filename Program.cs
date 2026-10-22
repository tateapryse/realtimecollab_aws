using ApryseCollaborationPoc.Hubs;
using ApryseCollaborationPoc.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR(options =>
{
	options.MaximumReceiveMessageSize = 1024 * 1024;
});
builder.Services.AddSingleton<IAnnotationRepository, InMemoryAnnotationRepository>();
builder.Services.AddHealthChecks();

var app = builder.Build();

var configuredAssetRoot = builder.Configuration["WEBVIEWER_ASSET_ROOT"];
var packageRoot = Path.Combine(app.Environment.ContentRootPath, "node_modules", "@pdftron", "webviewer");
var assetRoot = string.IsNullOrWhiteSpace(configuredAssetRoot)
	? app.Environment.ContentRootPath
	: Path.GetFullPath(configuredAssetRoot);
var webViewerAssets = string.IsNullOrWhiteSpace(configuredAssetRoot)
	? Path.Combine(packageRoot, "public")
	: Path.Combine(assetRoot, "lib");
var webViewerScript = string.IsNullOrWhiteSpace(configuredAssetRoot)
	? Path.Combine(packageRoot, "webviewer.min.js")
	: Path.Combine(webViewerAssets, "webviewer.min.js");
var sampleFiles = Path.Combine(assetRoot, "sample-files");
var signalRAssets = Path.Combine(app.Environment.ContentRootPath, "node_modules", "@microsoft", "signalr", "dist", "browser");

if (!Directory.Exists(webViewerAssets) || !File.Exists(webViewerScript))
{
	throw new InvalidOperationException("WebViewer assets are missing. Run npm install before starting the application.");
}

var webViewerContentTypes = new FileExtensionContentTypeProvider();
webViewerContentTypes.Mappings[".res"] = "application/octet-stream";
webViewerContentTypes.Mappings[".mem"] = "application/octet-stream";

app.Use(async (context, next) =>
{
	context.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
	context.Response.Headers["Cross-Origin-Embedder-Policy"] = "require-corp";
	await next();
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
	FileProvider = new PhysicalFileProvider(webViewerAssets),
	RequestPath = "/lib",
	ContentTypeProvider = webViewerContentTypes,
	OnPrepareResponse = context =>
	{
		var fileName = context.File.Name;
		if (fileName.Contains(".br.", StringComparison.Ordinal) || fileName.EndsWith(".br", StringComparison.Ordinal))
		{
			context.Context.Response.Headers.ContentEncoding = "br";
		}
		else if (fileName.Contains(".gz.", StringComparison.Ordinal) || fileName.EndsWith(".gz", StringComparison.Ordinal))
		{
			context.Context.Response.Headers.ContentEncoding = "gzip";
		}
	},
});
app.UseStaticFiles(new StaticFileOptions
{
	FileProvider = new PhysicalFileProvider(sampleFiles),
	RequestPath = "/sample-files",
});
app.UseStaticFiles(new StaticFileOptions
{
	FileProvider = new PhysicalFileProvider(signalRAssets),
	RequestPath = "/vendor/signalr",
});

app.MapGet("/lib/webviewer.min.js", () => Results.File(webViewerScript, "application/javascript"));
app.MapHealthChecks("/health");
app.MapGet("/api/documents/{documentId}/annotations", async (
	string documentId,
	IAnnotationRepository repository,
	CancellationToken cancellationToken) =>
	Results.Ok(await repository.GetDocumentAsync(documentId, cancellationToken)));
app.MapHub<CollaborationHub>("/hubs/collaboration");
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program;
