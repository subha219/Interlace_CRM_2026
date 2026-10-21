using Application.Common.Repositories;
using Application.Common.Services.FileDocumentManager;
using Domain.Entities;
using Microsoft.Extensions.Options;

namespace Infrastructure.FileDocumentManager;

public class FileDocumentService : IFileDocumentService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "doc", "docx", "xls", "xlsx", "ppt", "pptx", "txt", "csv", "rtf", "odt", "ods",
        "png", "jpg", "jpeg", "gif", "webp"
    };

    private readonly IUnitOfWork _unitOfWork;
    private readonly string _folderPath;
    private readonly int _maxFileSizeInBytes;
    private readonly ICommandRepository<FileDocument> _docRepository;

    public FileDocumentService(
        IUnitOfWork unitOfWork,
        IOptions<FileDocumentSettings> settings,
        ICommandRepository<FileDocument> docRepository
        )
    {
        _unitOfWork = unitOfWork;
        _folderPath = Path.Combine(Directory.GetCurrentDirectory(), settings.Value.PathFolder);
        _maxFileSizeInBytes = settings.Value.MaxFileSizeInMB * 1024 * 1024;
        _docRepository = docRepository;
    }

    public async Task<string> UploadAsync(
        string? originalFileName,
        string? docExtension,
        byte[]? fileData,
        long? size,
        string? description = "",
        string? createdById = "",
        CancellationToken cancellationToken = default)
    {

        if (string.IsNullOrWhiteSpace(docExtension) || docExtension.Contains(Path.DirectorySeparatorChar) || docExtension.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new Exception($"Invalid file extension: {nameof(docExtension)}");
        }

        //>>> only document and image types; anything that a browser could run (html, svg, js) or a server could execute is refused
        docExtension = docExtension.TrimStart('.').ToLowerInvariant();
        if (!AllowedExtensions.Contains(docExtension))
        {
            throw new Exception($"Files of type .{docExtension} are not allowed. Allowed: {string.Join(", ", AllowedExtensions.OrderBy(x => x))}.");
        }

        if (fileData == null || fileData.Length == 0)
        {
            throw new Exception($"File data cannot be null or empty: {nameof(fileData)}");
        }

        if (fileData.Length > _maxFileSizeInBytes)
        {
            throw new Exception($"File size exceeds the maximum allowed size of {_maxFileSizeInBytes / (1024 * 1024)} MB");
        }

        var fileName = $"{Guid.NewGuid():N}.{docExtension}";

        if (!Directory.Exists(_folderPath))
        {
            Directory.CreateDirectory(_folderPath);
        }

        var filePath = Path.Combine(_folderPath, fileName);

        await File.WriteAllBytesAsync(filePath, fileData, cancellationToken);

        var doc = new FileDocument();
        doc.Name = fileName;
        doc.OriginalName = originalFileName;
        doc.Extension = docExtension;
        doc.GeneratedName = fileName;
        doc.FileSize = size;
        doc.Description = description;
        doc.CreatedById = createdById;

        await _docRepository.CreateAsync(doc, cancellationToken);
        await _unitOfWork.SaveAsync(cancellationToken);

        return fileName;
    }

    public async Task<byte[]> GetFileAsync(string fileName, CancellationToken cancellationToken = default)
    {
        //>>> only a bare file name is accepted: no folders, no "..", so nothing outside the document folder can be read
        var safeName = Path.GetFileName(fileName ?? string.Empty);
        var filePath = Path.GetFullPath(Path.Combine(_folderPath, safeName));

        if (safeName.Length == 0 || !filePath.StartsWith(Path.GetFullPath(_folderPath) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
        {
            //>>> files uploaded before the folder moved out of wwwroot
            var legacy = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "app_data", "docs", safeName);
            filePath = safeName.Length > 0 && File.Exists(legacy)
                ? legacy
                : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "nodocument.txt");
        }

        var result = await File.ReadAllBytesAsync(filePath, cancellationToken);

        return result;
    }

}
