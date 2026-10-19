namespace Staybnb.Models;

public enum BookingStatus { Pending, Approved, Rejected, CheckedIn, Completed, Cancelled }
public enum PaymentStatus { Pending, Paid, Refunded, Failed }
public enum ApplicationStatus { Pending, Approved, Rejected }
public enum ActivityType { Registration, Login, Logout, RoleChange, ApplicationSubmitted, ApplicationDecision, BookingStatusChange, PropertyUpdate, CheckIn, Other }
public enum NotificationType { Info, Success, Warning, BookingUpdate, NewMessage, ApplicationUpdate }
public enum CheckInStatus { NotStarted, InProgress, DocumentsSubmitted, Verified, Rejected }
public enum DocumentStatus { Pending, Verified, Rejected }