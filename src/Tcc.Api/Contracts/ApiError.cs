namespace Tcc.Api.Contracts;

public sealed record ApiError(string Code, string Message)
{
    public static ApiError FromStatus(int status) => status switch
    {
        400 => new("validation_failed", "Requisição inválida."),
        401 => new("unauthorized", "Sessão necessária ou inválida."),
        403 => new("forbidden", "Operação não permitida."),
        404 => new("not_found", "Recurso não encontrado."),
        409 => new("conflict", "Conflito com o estado atual."),
        500 => new("internal_error", "Erro interno."),
        503 => new("integration_unavailable", "Integração externa indisponível."),
        502 => new("integration_invalid_response", "Resposta inválida da integração externa."),
        _ => new("http_error", "Não foi possível atender à requisição.")
    };
}
