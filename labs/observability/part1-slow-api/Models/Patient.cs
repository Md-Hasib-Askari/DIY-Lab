namespace ObservabilityPart1.Models;

public class Patient
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Prescription> Prescriptions { get; set; } = [];
}

public class Prescription
{
    public int Id { get; set; }
    public string Drug { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }
}
