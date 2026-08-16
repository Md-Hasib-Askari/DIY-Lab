using Microsoft.AspNetCore.Mvc;
using PrescriptionApi.Models;
using PrescriptionApi.Services;

namespace PrescriptionApi.Controllers;

[ApiController]
[Route("/prescriptions")]
public class PrescriptionController(ValidationService validation) : ControllerBase
{
    [HttpPost("validate")]
    public async Task<IActionResult> Validate(AppointmentReference dto)
    {
        await validation.ValidateAsync(dto);
        return Ok(new { dto.Id, Status = "valid" });
    }
}