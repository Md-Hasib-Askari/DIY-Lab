namespace AppointmentApi.Models;

public record Appointment(int Id, string PatientName, string DoctorName, string Reason);