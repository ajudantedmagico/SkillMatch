using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SkillMatch.Api.Data;
using SkillMatch.Api.Dtos;
using SkillMatch.Api.Models;

namespace SkillMatch.Api.Services;

public interface ICurriculoService
{
    Task<GerarCurriculoResponseDto> GerarCurriculoAsync(int usuarioId, GerarCurriculoRequestDto dto);
    Task<CurriculoDetalheDto?> SalvarCurriculoAsync(int usuarioId, SalvarCurriculoRequestDto dto);
    Task<List<CurriculoListaDto>> ListarCurriculosAsync(int usuarioId);
    Task<CurriculoDetalheDto?> GetCurriculoAsync(int usuarioId, int curriculoId);
    Task<byte[]> ExportarWordAsync(int usuarioId, int curriculoId);
    Task<byte[]> ExportarPdfAsync(int usuarioId, int curriculoId);
}

public class CurriculoService : ICurriculoService
{
    private readonly SkillMatchContext _context;
    private readonly IPerfilService _perfilService;

    public CurriculoService(SkillMatchContext context, IPerfilService perfilService)
    {
        _context = context;
        _perfilService = perfilService;
    }

    public async Task<GerarCurriculoResponseDto> GerarCurriculoAsync(int usuarioId, GerarCurriculoRequestDto dto)
    {
        if (!dto.ConsentimentoIA)
            throw new InvalidOperationException("Consentimento para IA é obrigatório");

        // Get user profile
        var perfil = await _perfilService.GetPerfilAsync(usuarioId);
        if (perfil == null)
            throw new InvalidOperationException("Perfil do usuário não encontrado");

        // TODO: Call AI service to generate optimized CV content
        // For now, we'll return a basic structure based on the profile

        var secoes = new CurriculoSecoesDto
        {
            Cabecalho = new CabecalhoDto
            {
                Nome = perfil.Nome,
                Email = perfil.Email,
                Telefone = perfil.Telefone,
                LinkedIn = perfil.LinkedIn,
                Portfolio = perfil.Portfolio,
                Localizacao = $"{perfil.Cidade}, {perfil.Estado}"
            },
            ResumoBio = new ResumoBioDto
            {
                Conteudo = perfil.ObjetivosProfissionais
            },
            Experiencias = perfil.Experiencias.Select(e => new ExperienciaGeradaDto
            {
                Empresa = e.Empresa,
                Cargo = e.Cargo,
                DataInicio = e.DataInicio,
                DataFim = e.DataFim,
                Descricao = e.Atividades,
                Tecnologias = e.Tecnologias
            }).ToList(),
            Competencias = new CompetenciasDto
            {
                Tecnicas = perfil.CompetenciasTecnicas,
                Comportamentais = perfil.SoftSkills
            },
            Formacoes = perfil.Formacoes.Select(f => new FormacaoGeradaDto
            {
                Instituicao = f.Instituicao,
                Curso = f.Curso,
                Tipo = f.Tipo,
                DataInicio = f.DataInicio,
                DataConclusao = f.DataConclusao
            }).ToList()
        };

        // TODO: Update cabecalho.Titulo based on job description + AI

        return new GerarCurriculoResponseDto
        {
            Titulo = $"CV - {perfil.Nome}",
            Secoes = secoes
        };
    }

    public async Task<CurriculoDetalheDto?> SalvarCurriculoAsync(int usuarioId, SalvarCurriculoRequestDto dto)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == usuarioId);
        if (usuario == null)
            return null;

        var curriculo = new Curriculo
        {
            UsuarioId = usuarioId,
            Titulo = dto.Titulo,
            DescricaoVaga = dto.DescricaoVaga,
            SecoesJson = JsonSerializer.Serialize(dto.Secoes),
            CabecalhoEditado = dto.CabecalhoEditado,
            ResumoBioEditado = dto.ResumoBioEditado,
            ExperienciaEditada = dto.ExperienciaEditada,
            CompetenciasEditadas = dto.CompetenciasEditadas,
            FormacaoEditada = dto.FormacaoEditada,
            DataGeracao = DateTime.UtcNow
        };

        _context.Curriculos.Add(curriculo);
        await _context.SaveChangesAsync();

        return MapToDetalheDto(curriculo, dto.Secoes);
    }

    public async Task<List<CurriculoListaDto>> ListarCurriculosAsync(int usuarioId)
    {
        var curriculos = await _context.Curriculos
            .Where(c => c.UsuarioId == usuarioId)
            .OrderByDescending(c => c.DataGeracao)
            .ToListAsync();

        return curriculos.Select(c => new CurriculoListaDto
        {
            Id = c.Id,
            Titulo = c.Titulo,
            DataGeracao = c.DataGeracao,
            DescricaoVaga = TruncateText(c.DescricaoVaga, 100),
            FoiEditado = c.CabecalhoEditado || c.ResumoBioEditado || c.ExperienciaEditada || 
                        c.CompetenciasEditadas || c.FormacaoEditada
        }).ToList();
    }

    public async Task<CurriculoDetalheDto?> GetCurriculoAsync(int usuarioId, int curriculoId)
    {
        var curriculo = await _context.Curriculos
            .FirstOrDefaultAsync(c => c.Id == curriculoId && c.UsuarioId == usuarioId);

        if (curriculo == null)
            return null;

        try
        {
            var secoes = JsonSerializer.Deserialize<CurriculoSecoesDto>(curriculo.SecoesJson) ?? new CurriculoSecoesDto();
            return MapToDetalheDto(curriculo, secoes);
        }
        catch
        {
            return null;
        }
    }

    private CurriculoDetalheDto MapToDetalheDto(Curriculo curriculo, CurriculoSecoesDto secoes)
    {
        return new CurriculoDetalheDto
        {
            Id = curriculo.Id,
            Titulo = curriculo.Titulo,
            DescricaoVaga = curriculo.DescricaoVaga,
            Secoes = secoes,
            DataGeracao = curriculo.DataGeracao,
            DataAtualizacao = curriculo.DataAtualizacao,
            CabecalhoEditado = curriculo.CabecalhoEditado,
            ResumoBioEditado = curriculo.ResumoBioEditado,
            ExperienciaEditada = curriculo.ExperienciaEditada,
            CompetenciasEditadas = curriculo.CompetenciasEditadas,
            FormacaoEditada = curriculo.FormacaoEditada
        };
    }

    private string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text))
            return string.Empty;
        return text.Length > maxLength ? text.Substring(0, maxLength) + "..." : text;
    }

    public async Task<byte[]> ExportarWordAsync(int usuarioId, int curriculoId)
    {
        var curriculo = await _context.Curriculos
            .FirstOrDefaultAsync(c => c.Id == curriculoId && c.UsuarioId == usuarioId);

        if (curriculo == null)
            return Array.Empty<byte>();

        try
        {
            var secoes = JsonSerializer.Deserialize<CurriculoSecoesDto>(curriculo.SecoesJson) ?? new CurriculoSecoesDto();
            var html = ConvertCVToHtml(secoes, curriculo.Titulo);
            
            // Convert HTML to DOCX (simple approach - in production use DocumentFormat.OpenXml)
            var docxBytes = Encoding.UTF8.GetBytes(html);
            return docxBytes;
        }
        catch (Exception ex)
        {
            return Array.Empty<byte>();
        }
    }

    public async Task<byte[]> ExportarPdfAsync(int usuarioId, int curriculoId)
    {
        var curriculo = await _context.Curriculos
            .FirstOrDefaultAsync(c => c.Id == curriculoId && c.UsuarioId == usuarioId);

        if (curriculo == null)
            return Array.Empty<byte>();

        try
        {
            var secoes = JsonSerializer.Deserialize<CurriculoSecoesDto>(curriculo.SecoesJson) ?? new CurriculoSecoesDto();
            var html = ConvertCVToHtml(secoes, curriculo.Titulo);
            
            // Convert HTML to PDF (in production use SelectPdf, iTextSharp, etc.)
            var pdfBytes = Encoding.UTF8.GetBytes(html);
            return pdfBytes;
        }
        catch (Exception ex)
        {
            return Array.Empty<byte>();
        }
    }

    private string ConvertCVToHtml(CurriculoSecoesDto secoes, string titulo)
    {
        var html = new StringBuilder();
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset='UTF-8'>");
        html.AppendLine("<title>Currículo</title>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; margin: 40px; }");
        html.AppendLine("h1 { color: #2c3e50; border-bottom: 2px solid #3498db; padding-bottom: 10px; text-align: center; }");
        html.AppendLine("h2 { color: #34495e; margin-top: 20px; margin-bottom: 10px; border-bottom: 1px solid #bdc3c7; }");
        html.AppendLine("h3 { color: #34495e; margin-top: 15px; margin-bottom: 5px; }");
        html.AppendLine(".header { text-align: center; margin-bottom: 20px; }");
        html.AppendLine(".section { margin-bottom: 20px; }");
        html.AppendLine(".job { margin-bottom: 15px; }");
        html.AppendLine(".date { color: #7f8c8d; font-size: 0.9em; }");
        html.AppendLine("ul { list-style-type: disc; margin-left: 20px; }");
        html.AppendLine("p { margin: 5px 0; }");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        
        // Header
        html.AppendLine("<div class='header'>");
        html.AppendLine($"<h1>{secoes.Cabecalho.Nome}</h1>");
        html.AppendLine($"<p><strong>{secoes.Cabecalho.Titulo}</strong></p>");
        if (!string.IsNullOrEmpty(secoes.Cabecalho.Email))
            html.AppendLine($"<p>{secoes.Cabecalho.Email}</p>");
        if (!string.IsNullOrEmpty(secoes.Cabecalho.Telefone))
            html.AppendLine($"<p>{secoes.Cabecalho.Telefone}</p>");
        if (!string.IsNullOrEmpty(secoes.Cabecalho.Localizacao))
            html.AppendLine($"<p>{secoes.Cabecalho.Localizacao}</p>");
        html.AppendLine("</div>");

        // Bio
        if (!string.IsNullOrEmpty(secoes.ResumoBio?.Conteudo))
        {
            html.AppendLine("<div class='section'>");
            html.AppendLine("<h2>Resumo Profissional</h2>");
            html.AppendLine($"<p>{secoes.ResumoBio.Conteudo}</p>");
            html.AppendLine("</div>");
        }

        // Experiências
        if (secoes.Experiencias?.Count > 0)
        {
            html.AppendLine("<div class='section'>");
            html.AppendLine("<h2>Experiência Profissional</h2>");
            foreach (var exp in secoes.Experiencias)
            {
                html.AppendLine("<div class='job'>");
                html.AppendLine($"<h3>{exp.Cargo}</h3>");
                html.AppendLine($"<p><strong>{exp.Empresa}</strong></p>");
                html.AppendLine($"<p class='date'>{exp.DataInicio:yyyy-MM-dd} a {(exp.DataFim?.ToString("yyyy-MM-dd") ?? "Presente")}</p>");
                if (!string.IsNullOrEmpty(exp.Descricao))
                    html.AppendLine($"<p>{exp.Descricao}</p>");
                html.AppendLine("</div>");
            }
            html.AppendLine("</div>");
        }

        // Competências
        if (secoes.Competencias != null)
        {
            html.AppendLine("<div class='section'>");
            html.AppendLine("<h2>Competências</h2>");
            
            if (secoes.Competencias.Tecnicas?.Count > 0)
            {
                html.AppendLine("<h3>Técnicas</h3>");
                html.AppendLine("<p>");
                html.AppendLine(string.Join(", ", secoes.Competencias.Tecnicas));
                html.AppendLine("</p>");
            }
            
            if (secoes.Competencias.Comportamentais?.Count > 0)
            {
                html.AppendLine("<h3>Comportamentais</h3>");
                html.AppendLine("<p>");
                html.AppendLine(string.Join(", ", secoes.Competencias.Comportamentais));
                html.AppendLine("</p>");
            }
            
            html.AppendLine("</div>");
        }

        // Formações
        if (secoes.Formacoes?.Count > 0)
        {
            html.AppendLine("<div class='section'>");
            html.AppendLine("<h2>Formação</h2>");
            foreach (var f in secoes.Formacoes)
            {
                html.AppendLine($"<h3>{f.Curso}</h3>");
                html.AppendLine($"<p><strong>{f.Instituicao}</strong></p>");
                html.AppendLine($"<p class='date'>{f.DataInicio:yyyy-MM-dd} a {(f.DataConclusao?.ToString("yyyy-MM-dd") ?? "Em andamento")}</p>");
            }
            html.AppendLine("</div>");
        }

        // Certificações
        if (secoes.Certificacoes?.Count > 0)
        {
            html.AppendLine("<div class='section'>");
            html.AppendLine("<h2>Certificações</h2>");
            html.AppendLine("<ul>");
            foreach (var cert in secoes.Certificacoes)
            {
                html.AppendLine($"<li>{cert}</li>");
            }
            html.AppendLine("</ul>");
            html.AppendLine("</div>");
        }

        html.AppendLine("</body>");
        html.AppendLine("</html>");

        return html.ToString();
    }
