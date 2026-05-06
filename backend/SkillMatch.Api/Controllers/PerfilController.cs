using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkillMatch.Api.Dtos;
using SkillMatch.Api.Services;

namespace SkillMatch.Api.Controllers;

[ApiController]
[Route("api/perfil")]
[Authorize]
public class PerfilController : ControllerBase
{
    private readonly IPerfilService _perfilService;

    public PerfilController(IPerfilService perfilService)
    {
        _perfilService = perfilService;
    }

    private int GetUsuarioId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return int.TryParse(userIdClaim?.Value, out var userId) ? userId : 0;
    }

    [HttpGet]
    public async Task<ActionResult<PerfilDto>> GetPerfil()
    {
        var usuarioId = GetUsuarioId();
        if (usuarioId == 0)
            return Unauthorized();

        var perfil = await _perfilService.GetPerfilAsync(usuarioId);
        if (perfil == null)
            return NotFound(new { message = "Perfil não encontrado" });

        return Ok(perfil);
    }

    [HttpPut]
    public async Task<ActionResult<PerfilDto>> SalvarPerfil([FromBody] PerfilDto dto)
    {
        var usuarioId = GetUsuarioId();
        if (usuarioId == 0)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Email))
            return BadRequest(new { message = "Nome e email são obrigatórios" });

        try
        {
            var success = await _perfilService.SalvarPerfilAsync(usuarioId, dto);
            if (!success)
                return NotFound(new { message = "Perfil não encontrado" });

            var perfilAtualizado = await _perfilService.GetPerfilAsync(usuarioId);
            return Ok(perfilAtualizado);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = $"Erro ao salvar perfil: {ex.Message}" });
        }
    }
}
