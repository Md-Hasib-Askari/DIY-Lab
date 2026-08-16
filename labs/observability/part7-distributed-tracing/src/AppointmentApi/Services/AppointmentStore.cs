using AppointmentApi.DTOs;
using AppointmentApi.Models;

namespace AppointmentApi.Services;

public class AppointmentStore
{
    private readonly Dictionary<int, Appointment> _items = new();
    private int _nextId = 1;

    public Appointment Add(AppointmentDto dto)
    {
        var appointment = new Appointment(
            _nextId++,
            dto.PatientName,
            dto.DoctorName,
            dto.Reason
        );
        _items[appointment.Id] = appointment;
        return appointment;
    }
}