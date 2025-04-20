using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

using Dedup.Interfaces;

[ApiController]
[Route("api/deduplication")]
public class DeduplicationController : ControllerBase
{
    private readonly IDeduplicationService _deduplicationService;

    public DeduplicationController(IDeduplicationService deduplicationService)
    {
        _deduplicationService = deduplicationService;
    }

    [HttpPost("start")] // Swagger: Déclenche le processus
    public async Task<IActionResult> StartDeduplication()
    {
        try
        {
            // Démarrer le processus de déduplication
            var process = await _deduplicationService.StartDeduplicationProcessAsync();

            // Retourner une réponse avec les détails du processus
            return Ok(new
            {
                message = "Déduplication démarrée avec succès",
                processId = process.Id,
                username = process.Username,
                status = process.Status,
                createdAt = process.CreatedAt
            });
        }
        catch (Exception ex)
        {
            // Retourner une erreur en cas d'échec
            return BadRequest(new
            {
                message = "Échec du démarrage de la déduplication",
                error = ex.Message
            });
        }
    }



    [HttpGet("all")]
        public async Task<IActionResult> GetAllProcesses()
        {
            try
            {
                Console.WriteLine("inside controler");
                var processes = await _deduplicationService.GetAllProcesses();
                return Ok(processes);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = $"Error retrieving processes: {ex.Message}" });
            }
        }
}