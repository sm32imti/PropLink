namespace PropLink.Application.Common.Interfaces;

public interface ICloudStorageService
{
    /// <summary>
    /// Uploads a public image (e.g., property photo) to the cloud storage bucket and returns its permanent URL.
    /// </summary>
    Task<string> UploadPublicImageAsync(Stream fileStream, string fileName, string contentType);

    /// <summary>
    /// Uploads a sensitive private document (e.g., Seller NID, Title Deed) to a private cloud bucket.
    /// </summary>
    Task<string> UploadPrivateDocumentAsync(Stream fileStream, string fileName, string contentType, string subfolder = "verifications");

    /// <summary>
    /// Retrieves a private document stream from cloud storage for authorized backend proxy streaming.
    /// </summary>
    Task<(byte[] FileBytes, string ContentType, string FileName)?> GetPrivateDocumentAsync(string storageReference);

    /// <summary>
    /// Generates a temporary secure signed URL for authorized access.
    /// </summary>
    Task<string> GenerateSecureDocumentAccessUrlAsync(string storageReference, TimeSpan validity);

    /// <summary>
    /// Deletes a file from cloud storage.
    /// </summary>
    Task DeleteFileAsync(string storageReference);
}
