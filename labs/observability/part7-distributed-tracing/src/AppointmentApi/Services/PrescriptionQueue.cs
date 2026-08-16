using System.Threading.Channels;
using AppointmentApi.Models;

namespace AppointmentApi.Services;

// Used by the STEP 4 fix: the slow validation is handed to the background
// worker through this channel instead of running on the request path.
public class PrescriptionQueue
{
    private readonly Channel<Appointment> _channel = Channel.CreateUnbounded<Appointment>();

    public void Enqueue(Appointment appointment) => _channel.Writer.TryWrite(appointment);

    public ChannelReader<Appointment> Reader => _channel.Reader;
}