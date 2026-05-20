using System.Net.Http.Json;

namespace SkillMatch.Api.Services;

public interface IOpenAIService
{
    Task<string> GenerarCVAsync(string prompt);
    Task<string> OptimizarTextoAsync(string texto, string contexto);
}

public class OpenAIService : IOpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OpenAIService> _logger;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _maxTokens;

    public OpenAIService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAIService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI:ApiKey não configurada");
        
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("OpenAI:ApiKey está vazio");
            
        _model = configuration["OpenAI:Model"] ?? "gpt-3.5-turbo";
        _maxTokens = int.Parse(configuration["OpenAI:MaxTokens"] ?? "2000");
        
        _logger.LogInformation("OpenAI Service inicializado com modelo: {model}", _model);
    }

    public async Task<string> GenerarCVAsync(string prompt)
    {
        try
        {
            var request = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = "Você é um especialista em recrutamento. Gere currículos profissionais em formato JSON válido." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.7,
                max_tokens = _maxTokens
            };

            _logger.LogInformation("Chamando OpenAI API para gerar CV...");
            var response = await _httpClient.PostAsJsonAsync(
                "https://api.openai.com/v1/chat/completions", 
                request,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null }
            );
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Erro da API OpenAI: {statusCode} - {error}", response.StatusCode, errorContent);
                throw new InvalidOperationException($"Erro ao chamar OpenAI API: {response.StatusCode} - {errorContent}");
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
            if (result?.Choices?.FirstOrDefault()?.Message?.Content == null)
            {
                _logger.LogError("Resposta vazia da OpenAI API");
                throw new InvalidOperationException("Resposta vazia da OpenAI API");
            }

            _logger.LogInformation("CV gerado com sucesso");
            return result.Choices[0].Message.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar CV com OpenAI");
            throw;
        }
    }

    public async Task<string> OptimizarTextoAsync(string texto, string contexto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return texto;
            
        try
        {
            var prompt = $"Otimize o seguinte texto profissional para ser mais impactante considerando o contexto: {contexto}\n\nTexto: {texto}";
            
            var request = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = "Você é um especialista em copywriting profissional. Retorne apenas o texto otimizado, sem explicações adicionais." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.7,
                max_tokens = _maxTokens
            };

            _logger.LogInformation("Otimizando texto com OpenAI...");
            var response = await _httpClient.PostAsJsonAsync(
                "https://api.openai.com/v1/chat/completions", 
                request,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null }
            );
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Erro ao otimizar com OpenAI: {statusCode} - {error}. Usando texto original.", response.StatusCode, errorContent);
                return texto;
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
            var textoOtimizado = result?.Choices?.FirstOrDefault()?.Message?.Content;
            
            if (string.IsNullOrWhiteSpace(textoOtimizado))
            {
                _logger.LogWarning("Resposta vazia da OpenAI ao otimizar. Usando texto original.");
                return texto;
            }

            _logger.LogInformation("Texto otimizado com sucesso");
            return textoOtimizado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao otimizar texto");
            return texto; // Retorna o texto original em caso de erro
        }
    }
}

// DTOs para a resposta da OpenAI API
public class OpenAIResponse
{
    public List<Choice>? Choices { get; set; }
}

public class Choice
{
    public Message? Message { get; set; }
}

public class Message
{
    public string? Content { get; set; }
}
