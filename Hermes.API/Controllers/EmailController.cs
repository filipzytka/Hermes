using Hermes.Application.Abstraction;
using Hermes.Application.Entities.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hermes.API.Controllers;

[ApiController]
[Route("api/email")]
public class EmailController : Controller
{
    private readonly IEmailSender _emailService;

    public EmailController(IEmailSender emailService)
    {
        _emailService = emailService;
    }

    [Authorize(Roles = "admin")]
    [HttpPost]
    public async Task<IActionResult> SendEmail([FromBody] EmailDto dto)
    {
        var status = await _emailService.SendEmail(dto.ReceiverEmail, dto.Topic, dto.Body);

        return status ? Ok(new { message = "Email has been sent succesfully" }) :
         BadRequest(new { message = "Sending email has failed" });
    }
}