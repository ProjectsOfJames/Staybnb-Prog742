namespace Staybnb.Services;

/// <summary>Folder names used for uploads, defined once so every controller agrees.</summary>
public static class StorageFolders
{
    /// <summary>Private: IDs and passports (App_Data/uploads/guest-documents).</summary>
    public const string GuestDocuments = "guest-documents";

    /// <summary>Public: property photos (wwwroot/uploads/properties).</summary>
    public const string PropertyImages = "properties";
}