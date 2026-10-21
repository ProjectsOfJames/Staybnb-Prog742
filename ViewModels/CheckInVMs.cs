using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Staybnb.Models;

namespace Staybnb.ViewModels;

/// <summary>One rule/step in a property's check-in process (stored as JSON in CheckInProcess.StepsJson).</summary>
public class CheckInStepDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class GuestDocumentRowVM
{
    public int Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
}

/// <summary>Step 1: read and accept the property's rules.</summary>
public class CheckInRulesVM
{
    public int BookingId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string ProcessTitle { get; set; } = string.Empty;
    public List<CheckInStepDto> Steps { get; set; } = new();

    [Display(Name = "I have read and accept the property rules")]
    public bool Agree { get; set; }
}

/// <summary>Step 2: upload an ID or passport.</summary>
public class CheckInUploadVM
{
    public int BookingId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Document type")]
    public string DocumentType { get; set; } = "ID";

    [Display(Name = "File (JPG, PNG or PDF, max 5 MB)")]
    public IFormFile? File { get; set; }

    public List<GuestDocumentRowVM> Documents { get; set; } = new();
}

/// <summary>Step 3: review and submit for host verification.</summary>
public class CheckInConfirmVM
{
    public int BookingId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public CheckInStatus Status { get; set; }
    public List<GuestDocumentRowVM> Documents { get; set; } = new();

    public bool CanSubmit => Documents.Count > 0 &&
        (Status == CheckInStatus.InProgress || Status == CheckInStatus.Rejected);
}