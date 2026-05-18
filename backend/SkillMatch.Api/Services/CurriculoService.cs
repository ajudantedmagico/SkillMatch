using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.EntityFrameworkCore;
using SkillMatch.Api.Data;
using SkillMatch.Api.Dtos;
using SkillMatch.Api.Models;
using WordParagraph = DocumentFormat.OpenXml.Wordprocessing.Paragraph;
using PdfParagraph = iText.Layout.Element.Paragraph;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using PdfDocument = iText.Layout.Document;
using PdfTable = iText.Layout.Element.Table;
using WordText = DocumentFormat.OpenXml.Wordprocessing.Text;

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
    private readonly IOpenAIService _openAIService;

    public CurriculoService(SkillMatchContext context, IPerfilService perfilService, IOpenAIService openAIService)
    {
        _context = context;
        _perfilService = perfilService;
        _openAIService = openAIService;
    }

    public async Task<GerarCurriculoResponseDto> GerarCurriculoAsync(int usuarioId, GerarCurriculoRequestDto dto)
    {
        if (!dto.ConsentimentoIA)
            throw new InvalidOperationException("Consentimento para IA é obrigatório");

        // Get user profile
        var perfil = await _perfilService.GetPerfilAsync(usuarioId);
        if (perfil == null)
            throw new InvalidOperationException("Perfil do usuário não encontrado");

        // Call AI service to generate optimized CV content
        string resumoOtimizado = perfil.ObjetivosProfissionais;
        try
        {
            var promptResume = $"Com base na seguinte descrição de vaga:\n\n{dto.DescricaoVaga}\n\nOtimize este resumo profissional para se alinhar melhor com a vaga:\n\n{perfil.ObjetivosProfissionais}";
            resumoOtimizado = await _openAIService.OptimizarTextoAsync(perfil.ObjetivosProfissionais, dto.DescricaoVaga);
        }
        catch (Exception ex)
        {
            // Se houver erro na IA, usa o conteúdo original
            resumoOtimizado = perfil.ObjetivosProfissionais;
        }

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
                Conteudo = resumoOtimizado
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
            return GenerateDocxFromCV(secoes);
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
            return GeneratePdfFromCV(secoes);
        }
        catch (Exception ex)
        {
            return Array.Empty<byte>();
        }
    }

    private byte[] GenerateDocxFromCV(CurriculoSecoesDto secoes)
    {
        using (var memoryStream = new MemoryStream())
        {
            using (var wordDoc = WordprocessingDocument.Create(memoryStream, WordprocessingDocumentType.Document))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new WordDocument();
                var body = mainPart.Document.AppendChild(new Body());

                // Nome (h1 equivalent)
                var nomePara = new WordParagraph();
                var nomeRun = new Run();
                nomeRun.AppendChild(new RunProperties { Bold = new Bold(), FontSize = new FontSize { Val = "28" } });
                nomeRun.AppendChild(new WordText(secoes.Cabecalho?.Nome ?? "Currículo"));
                nomePara.AppendChild(nomeRun);
                nomePara.ParagraphProperties = new ParagraphProperties { Justification = new Justification { Val = JustificationValues.Center } };
                body.AppendChild(nomePara);

                // Contato
                var contactText = new List<string>();
                if (!string.IsNullOrEmpty(secoes.Cabecalho?.Email)) contactText.Add(secoes.Cabecalho.Email);
                if (!string.IsNullOrEmpty(secoes.Cabecalho?.Telefone)) contactText.Add(secoes.Cabecalho.Telefone);
                if (!string.IsNullOrEmpty(secoes.Cabecalho?.Localizacao)) contactText.Add(secoes.Cabecalho.Localizacao);

                if (contactText.Count > 0)
                {
                    var contactPara = new WordParagraph(new Run(new WordText(string.Join(" | ", contactText))));
                    contactPara.ParagraphProperties = new ParagraphProperties { Justification = new Justification { Val = JustificationValues.Center } };
                    body.AppendChild(contactPara);
                }

                body.AppendChild(new WordParagraph());

                // Resumo
                if (!string.IsNullOrEmpty(secoes.ResumoBio?.Conteudo))
                {
                    AddWordSection(body, "Resumo Profissional", secoes.ResumoBio.Conteudo);
                }

                // Experiência
                if (secoes.Experiencias?.Count > 0)
                {
                    body.AppendChild(CreateWordHeading("Experiência Profissional"));
                    foreach (var exp in secoes.Experiencias)
                    {
                        var jobPara = new WordParagraph();
                        var jobRun = new Run(new WordText(exp.Cargo));
                        jobRun.RunProperties = new RunProperties { Bold = new Bold() };
                        jobPara.AppendChild(jobRun);
                        body.AppendChild(jobPara);

                        body.AppendChild(new WordParagraph(new Run(new WordText($"{exp.Empresa}"))));
                        body.AppendChild(new WordParagraph(new Run(new WordText($"{exp.DataInicio:MMM yyyy} - {(exp.DataFim?.ToString("MMM yyyy") ?? "Presente")}"))));

                        if (!string.IsNullOrEmpty(exp.Descricao))
                            body.AppendChild(new WordParagraph(new Run(new WordText(exp.Descricao))));

                        body.AppendChild(new WordParagraph());
                    }
                }

                // Competências
                if (secoes.Competencias != null && (secoes.Competencias.Tecnicas?.Count > 0 || secoes.Competencias.Comportamentais?.Count > 0))
                {
                    body.AppendChild(CreateWordHeading("Competências"));

                    if (secoes.Competencias.Tecnicas?.Count > 0)
                    {
                        body.AppendChild(new WordParagraph(new Run(new WordText("Técnicas: " + string.Join(", ", secoes.Competencias.Tecnicas)))));
                    }

                    if (secoes.Competencias.Comportamentais?.Count > 0)
                    {
                        body.AppendChild(new WordParagraph(new Run(new WordText("Comportamentais: " + string.Join(", ", secoes.Competencias.Comportamentais)))));
                    }

                    body.AppendChild(new WordParagraph());
                }

                // Formação
                if (secoes.Formacoes?.Count > 0)
                {
                    body.AppendChild(CreateWordHeading("Formação"));
                    foreach (var f in secoes.Formacoes)
                    {
                        var coursePara = new WordParagraph();
                        var courseRun = new Run(new WordText(f.Curso));
                        courseRun.RunProperties = new RunProperties { Bold = new Bold() };
                        coursePara.AppendChild(courseRun);
                        body.AppendChild(coursePara);

                        body.AppendChild(new WordParagraph(new Run(new WordText($"{f.Instituicao}"))));
                        body.AppendChild(new WordParagraph(new Run(new WordText($"{f.DataInicio:yyyy} - {(f.DataConclusao?.ToString("yyyy") ?? "Em andamento")}"))));
                        body.AppendChild(new WordParagraph());
                    }
                }

                wordDoc.Save();
            }

            return memoryStream.ToArray();
        }
    }

    private void AddWordSection(Body body, string title, string content)
    {
        body.AppendChild(CreateWordHeading(title));
        body.AppendChild(new WordParagraph(new Run(new WordText(content))));
        body.AppendChild(new WordParagraph());
    }

    private WordParagraph CreateWordHeading(string text)
    {
        var heading = new WordParagraph();
        var run = new Run(new WordText(text));
        run.RunProperties = new RunProperties { Bold = new Bold(), FontSize = new FontSize { Val = "24" } };
        heading.AppendChild(run);
        heading.ParagraphProperties = new ParagraphProperties { SpacingBetweenLines = new SpacingBetweenLines { Before = "120", After = "120" } };
        return heading;
    }

    private byte[] GeneratePdfFromCV(CurriculoSecoesDto secoes)
    {
        using (var memoryStream = new MemoryStream())
        {
            using (var pdfWriter = new PdfWriter(memoryStream))
            {
                using (var pdfDocObj = new iText.Kernel.Pdf.PdfDocument(pdfWriter))
                {
                    var document = new PdfDocument(pdfDocObj);
                    document.SetMargins(36, 36, 36, 36);

                    // Nome
                    document.Add(new PdfParagraph(secoes.Cabecalho?.Nome ?? "Currículo")
                        .SetFontSize(24)
                        .SetBold()
                        .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER));

                    // Contato
                    var contactList = new List<string>();
                    if (!string.IsNullOrEmpty(secoes.Cabecalho?.Email)) contactList.Add(secoes.Cabecalho.Email);
                    if (!string.IsNullOrEmpty(secoes.Cabecalho?.Telefone)) contactList.Add(secoes.Cabecalho.Telefone);
                    if (!string.IsNullOrEmpty(secoes.Cabecalho?.Localizacao)) contactList.Add(secoes.Cabecalho.Localizacao);

                    if (contactList.Count > 0)
                    {
                        document.Add(new PdfParagraph(string.Join(" | ", contactList))
                            .SetTextAlignment(iText.Layout.Properties.TextAlignment.CENTER)
                            .SetFontSize(10));
                    }

                    document.Add(new PdfParagraph());

                    // Resumo
                    if (!string.IsNullOrEmpty(secoes.ResumoBio?.Conteudo))
                    {
                        document.Add(new PdfParagraph("RESUMO PROFISSIONAL").SetFontSize(12).SetBold());
                        document.Add(new PdfParagraph(secoes.ResumoBio.Conteudo).SetFontSize(10));
                        document.Add(new PdfParagraph());
                    }

                    // Experiência
                    if (secoes.Experiencias?.Count > 0)
                    {
                        document.Add(new PdfParagraph("EXPERIÊNCIA PROFISSIONAL").SetFontSize(12).SetBold());
                        foreach (var exp in secoes.Experiencias)
                        {
                            document.Add(new PdfParagraph(exp.Cargo).SetFontSize(11).SetBold());
                            document.Add(new PdfParagraph($"{exp.Empresa}")
                                .SetFontSize(10)
                                .SetMarginLeft(10));
                            document.Add(new PdfParagraph($"{exp.DataInicio:MMM yyyy} - {(exp.DataFim?.ToString("MMM yyyy") ?? "Presente")}")
                                .SetFontSize(9)
                                .SetMarginLeft(10));

                            if (!string.IsNullOrEmpty(exp.Descricao))
                            {
                                document.Add(new PdfParagraph(exp.Descricao)
                                    .SetFontSize(10)
                                    .SetMarginLeft(10));
                            }
                        }
                        document.Add(new PdfParagraph());
                    }

                    // Competências
                    if (secoes.Competencias != null && (secoes.Competencias.Tecnicas?.Count > 0 || secoes.Competencias.Comportamentais?.Count > 0))
                    {
                        document.Add(new PdfParagraph("COMPETÊNCIAS").SetFontSize(12).SetBold());

                        if (secoes.Competencias.Tecnicas?.Count > 0)
                        {
                            document.Add(new PdfParagraph("Técnicas: " + string.Join(", ", secoes.Competencias.Tecnicas))
                                .SetFontSize(10)
                                .SetMarginLeft(10));
                        }

                        if (secoes.Competencias.Comportamentais?.Count > 0)
                        {
                            document.Add(new PdfParagraph("Comportamentais: " + string.Join(", ", secoes.Competencias.Comportamentais))
                                .SetFontSize(10)
                                .SetMarginLeft(10));
                        }

                        document.Add(new PdfParagraph());
                    }

                    // Formação
                    if (secoes.Formacoes?.Count > 0)
                    {
                        document.Add(new PdfParagraph("FORMAÇÃO").SetFontSize(12).SetBold());
                        foreach (var f in secoes.Formacoes)
                        {
                            document.Add(new PdfParagraph(f.Curso).SetFontSize(11).SetBold());
                            document.Add(new PdfParagraph($"{f.Instituicao}")
                                .SetFontSize(10)
                                .SetMarginLeft(10));
                            document.Add(new PdfParagraph($"{f.DataInicio:yyyy} - {(f.DataConclusao?.ToString("yyyy") ?? "Em andamento")}")
                                .SetFontSize(9)
                                .SetMarginLeft(10));
                        }
                    }

                    document.Close();
                }
            }

            return memoryStream.ToArray();
        }
    }

    private WordParagraph AddHeadingWord(string text)
    {
        var heading = new WordParagraph();
        var run = new Run(new WordText(text));
        run.RunProperties = new RunProperties { Bold = new Bold(), FontSize = new FontSize { Val = "24" } };
        heading.AppendChild(run);
        heading.ParagraphProperties = new ParagraphProperties { SpacingBetweenLines = new SpacingBetweenLines { Before = "120", After = "120" } };
        return heading;
    }
}