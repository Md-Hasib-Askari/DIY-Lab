namespace ObservabilityPart4.DTOs;

// Incoming body for the CRM service
public record CustomerDto(string Name, string Email);

// Body the CRM service posts to the notification service
public record NotificationDto(int CustomerId, string Email);
