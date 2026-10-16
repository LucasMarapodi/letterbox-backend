using System.Net.Mail;

namespace Tcc.Domain.Identity;

public sealed record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Create(string? input)
    {
        var value = input?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(value) || value.Length > 254 ||
            !MailAddress.TryCreate(value, out var address) || address.Address != value ||
            !value.Contains('@') || value.Any(char.IsWhiteSpace))
            throw new DomainValidationException("Email inválido.");
        return new Email(value);
    }
}
