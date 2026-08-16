namespace AppointmentApi.DTOs;

public record AppointmentDto(string PatientName, string DoctorName, string Reason);