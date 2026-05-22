using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillMatch.Api.Dtos;
using SkillMatch.Api.Services;

namespace SkillMatch.Api.Controllers;

[ApiController]
[Route("api/curriculos")]
[Authorize]
public class CurriculoController : ControllerBase
{
    private readonly ICurriculoService _curriculoService;

    public CurriculoController(ICurriculoService curriculoService)
    {
        _curriculoService = curriculoService;
    }

    private int GetUsuarioId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim?.Value, out var userId) ? userId : 0;
    }

    [HttpPost("gerar")]
    public async Task<ActionResult<GerarCurriculoResponseClienteDto>> GerarCurriculo([FromBody] GerarCurriculoRequestDto dto)
    {
        var usuarioId = GetUsuarioId();
        if (usuarioId == 0)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.DescricaoVaga))
            return BadRequest(new { message = "Descrição da vaga é obrigatória" });

        try
        {
            var resultado = await _curriculoService.GerarCurriculoAsync(usuarioId, dto);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("salvar")]
    public async Task<ActionResult<CurriculoDetalheDto>> SalvarCurriculo([FromBody] SalvarCurriculoRequestDto dto)
    {
        var usuarioId = GetUsuarioId();
        if (usuarioId == 0)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Titulo))
            return BadRequest(new { message = "Título do currículo é obrigatório" });

        var resultado = await _curriculoService.SalvarCurriculoAsync(usuarioId, dto);
        if (resultado == null)
            return NotFound(new { message = "Usuário não encontrado" });

        return Ok(resultado);
    }

    [HttpGet]
    public async Task<ActionResult<List<CurriculoListaDto>>> ListarCurriculos()
    {
        var usuarioId = GetUsuarioId();
        if (usuarioId == 0)
            return Unauthorized();

        var curriculos = await _curriculoService.ListarCurriculosAsync(usuarioId);
        return Ok(curriculos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CurriculoDetalheDto>> GetCurriculo(int id)
    {
        var usuarioId = GetUsuarioId();
        if (usuarioId == 0)
            return Unauthorized();

        var curriculo = await _curriculoService.GetCurriculoAsync(usuarioId, id);
        if (curriculo == null)
            return NotFound(new { message = "Currículo não encontrado" });

        return Ok(curriculo);
    }

    [HttpGet("{id}/download/word")]
    public async Task<IActionResult> DownloadWord(int id)
    {
        var usuarioId = GetUsuarioId();
        if (usuarioId == 0)
            return Unauthorized();

        var bytes = await _curriculoService.ExportarWordAsync(usuarioId, id);
        if (bytes.Length == 0)
            return NotFound(new { message = "Currículo não encontrado" });

        return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"curriculo_{id}.docx");
    }

    [HttpGet("{id}/download/pdf")]
    public async Task<IActionResult> DownloadPdf(int id)
    {
        var usuarioId = GetUsuarioId();
        if (usuarioId == 0)
            return Unauthorized();

        var bytes = await _curriculoService.ExportarPdfAsync(usuarioId, id);
        if (bytes.Length == 0)
        {
            // Tenta fallback para Word caso a geração de PDF falhe
            var wordBytes = await _curriculoService.ExportarWordAsync(usuarioId, id);
            if (wordBytes.Length == 0)
                return NotFound(new { message = "Currículo não encontrado" });

            return File(wordBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", $"curriculo_{id}.docx");
        }

        return File(bytes, "application/pdf", $"curriculo_{id}.pdf");
    }
}
