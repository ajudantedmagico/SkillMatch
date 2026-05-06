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
        _model = configuration["OpenAI:Model"] ?? "gpt-3.5-turbo";
        _maxTokens = int.Parse(configuration["OpenAI:MaxTokens"] ?? "2000");

        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
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

            var response = await _httpClient.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Erro da API OpenAI: {response.StatusCode} - {errorContent}");
                throw new InvalidOperationException($"Erro ao chamar OpenAI API: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
            if (result?.Choices?.FirstOrDefault()?.Message?.Content == null)
                throw new InvalidOperationException("Resposta vazia da OpenAI API");

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
        try
        {
            var prompt = $"Otimize o seguinte texto profissional para ser mais impactante considerando o contexto: {contexto}\n\nTexto: {texto}";
            
            var request = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = "Você é um especialista em copywriting profissional." },
                    new { role = "user", content = prompt }
                },
                temperature = 0.7,
                max_tokens = _maxTokens
            };

            var response = await _httpClient.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", request);
            
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"Erro ao chamar OpenAI API: {response.StatusCode}");

            var result = await response.Content.ReadFromJsonAsync<OpenAIResponse>();
            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? texto;
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
