namespace SkillMatch.Api.Dtos;

// ═══════════════════════════════════════════════════════════════════
// PROFILE DTOs
// ═══════════════════════════════════════════════════════════════════

public class PerfilDto
{
    // Step 1: Personal Data
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Cidade { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string LinkedIn { get; set; } = string.Empty;
    public string Portfolio { get; set; } = string.Empty;

    // Step 2: Education
    public List<FormacaoDto> Formacoes { get; set; } = [];

    // Step 3: Work Experience
    public List<ExperienciaDto> Experiencias { get; set; } = [];

    // Step 4: Technical Skills
    public List<string> CompetenciasTecnicas { get; set; } = [];

    // Step 5: Professional Objective
    public string ObjetivosProfissionais { get; set; } = string.Empty;

    // Step 6: Soft Skills
    public List<string> SoftSkills { get; set; } = [];
}

public class FormacaoDto
{
    public int? Id { get; set; } // Optional for new entries
    public string Instituicao { get; set; } = string.Empty;
    public string Curso { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty; // "Ensino Médio", "Tecnólogo", "Graduação", etc
    public DateTime DataInicio { get; set; }
    public DateTime? DataConclusao { get; set; }
}

public class ExperienciaDto
{
    public int? Id { get; set; } // Optional for new entries
    public string Empresa { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public bool EmpregoAtual { get; set; } = false;
    public DateTime DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
    public string Atividades { get; set; } = string.Empty;
    public List<string> Tecnologias { get; set; } = [];
    public string Resultados { get; set; } = string.Empty;
}

// ═══════════════════════════════════════════════════════════════════
// CURRICULUM DTOs
// ═══════════════════════════════════════════════════════════════════

public class GerarCurriculoRequestDto
{
    public string DescricaoVaga { get; set; } = string.Empty;
    public bool ConsentimentoIA { get; set; } = false;
}

public class GerarCurriculoResponseDto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public CurriculoSecoesDto Secoes { get; set; } = new();
}

public class CurriculoSecoesDto
{
    public CabecalhoDto Cabecalho { get; set; } = new();
    public ResumoBioDto ResumoBio { get; set; } = new();
    public List<ExperienciaGeradaDto> Experiencias { get; set; } = [];
    public CompetenciasDto Competencias { get; set; } = new();
    public List<FormacaoGeradaDto> Formacoes { get; set; } = [];
    public List<string> Certificacoes { get; set; } = [];
}

public class CabecalhoDto
{
    public string Nome { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty; // Job title
    public string Email { get; set; } = string.Empty;
    public string Telefone { get; set; } = string.Empty;
    public string LinkedIn { get; set; } = string.Empty;
    public string Portfolio { get; set; } = string.Empty;
    public string Localizacao { get; set; } = string.Empty;
}

public class ResumoBioDto
{
    public string Conteudo { get; set; } = string.Empty;
}

public class ExperienciaGeradaDto
{
    public string Empresa { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public DateTime DataInicio { get; set; }
    public DateTime? DataFim { get; set; }
    public string Descricao { get; set; } = string.Empty; // AI-optimized description
    public List<string> Tecnologias { get; set; } = [];
}

public class CompetenciasDto
{
    public List<string> Tecnicas { get; set; } = [];
    public List<string> Comportamentais { get; set; } = [];
}

public class FormacaoGeradaDto
{
    public string Instituicao { get; set; } = string.Empty;
    public string Curso { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public DateTime DataInicio { get; set; }
    public DateTime? DataConclusao { get; set; }
}

public class SalvarCurriculoRequestDto
{
    public string Titulo { get; set; } = string.Empty;
    public string DescricaoVaga { get; set; } = string.Empty;
    public CurriculoSecoesDto Secoes { get; set; } = new();
    
    // Edit tracking (RN-08)
    public bool CabecalhoEditado { get; set; } = false;
    public bool ResumoBioEditado { get; set; } = false;
    public bool ExperienciaEditada { get; set; } = false;
    public bool CompetenciasEditadas { get; set; } = false;
    public bool FormacaoEditada { get; set; } = false;
}

public class CurriculoListaDto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public DateTime DataGeracao { get; set; }
    public string DescricaoVaga { get; set; } = string.Empty; // Truncated for list view
    public int Visualizacoes { get; set; } = 0;
    public bool FoiEditado { get; set; } = false;
}

public class CurriculoDetalheDto
{
    public int Id { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string DescricaoVaga { get; set; } = string.Empty;
    public CurriculoSecoesClienteDto Secoes { get; set; } = new();
    public DateTime DataGeracao { get; set; }
    public DateTime? DataAtualizacao { get; set; }
    public bool CabecalhoEditado { get; set; }
    public bool ResumoBioEditado { get; set; }
    public bool ExperienciaEditada { get; set; }
    public bool CompetenciasEditadas { get; set; }
    public bool FormacaoEditada { get; set; }
}

// ═══════════════════════════════════════════════════════════════════
// DTOs FOR FRONTEND (adapted field names)
// ═══════════════════════════════════════════════════════════════════

/// <summary>
/// DTO returned to frontend from GerarCurriculo endpoint
/// Maps backend structure to frontend expected field names
/// </summary>
public class GerarCurriculoResponseClienteDto
{
    public string Titulo { get; set; } = string.Empty;
    public CurriculoSecoesClienteDto Secoes { get; set; } = new();
}

/// <summary>
/// Adapted curriculum sections for frontend consumption
/// </summary>
public class CurriculoSecoesClienteDto
{
    // Frontend expects resumoProfissional (not ResumoBio.Conteudo)
    public string ResumoProfissional { get; set; } = string.Empty;
    
    // Frontend expects experienciaProfissional (not Experiencias)
    public List<ExperienciaClienteDto> ExperienciaProfissional { get; set; } = [];
    
    // Frontend expects formacaoAcademica (not Formacoes)
    public List<FormacaoClienteDto> FormacaoAcademica { get; set; } = [];
    
    // Frontend expects competenciasTecnicas (not Competencias.Tecnicas)
    public List<string> CompetenciasTecnicas { get; set; } = [];
    
    // Frontend expects softSkills (not Competencias.Comportamentais)
    public List<string> SoftSkills { get; set; } = [];
}

public class ExperienciaClienteDto
{
    public string Empresa { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Periodo { get; set; } = string.Empty;
    public List<string> Atividades { get; set; } = [];
    public string Resultados { get; set; } = string.Empty;
}

public class FormacaoClienteDto
{
    public string Instituicao { get; set; } = string.Empty;
    public string Curso { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Periodo { get; set; } = string.Empty;
}
