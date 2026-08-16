public class PatientDb
{
    // A stand-in for a real database: every id exists, and id 1 is slow.
    public async Task<Patient> FindAsync(int id)
    {
        if (id == 1)
        {
            await Task.Delay(3000);
        }
        return new Patient(id, $"Patient {id}");
    }
}