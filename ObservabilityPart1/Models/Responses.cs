namespace ObservabilityPart1.Models;

public record PrescriptionResponse(int Id, string Drug);

public record PatientResponse(int Id, string Name, List<PrescriptionResponse> Prescriptions);
