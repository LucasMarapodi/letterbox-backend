using System.Diagnostics.CodeAnalysis;

namespace Tcc.Domain.Identity;

public static class PasswordPolicy
{
    public const int MinimumLength = 6;
    public const string Description = "A senha deve ter pelo menos 6 caracteres, uma letra maiúscula, um número e um caractere especial. " +
        "Espaços e controles não contam como especial. Minúscula não é obrigatória. A senha é preservada sem Trim ou normalização.";

    public static void Validate([NotNull] string? password)
    {
        if (string.IsNullOrWhiteSpace(password)) throw new DomainValidationException(Description);

        var hasMinimumLength = password.Length >= MinimumLength;
        var hasUppercase = password.Any(char.IsUpper);
        var hasDigit = password.Any(char.IsDigit);
        var hasSpecial = password.Any(character => !char.IsLetterOrDigit(character) &&
            !char.IsWhiteSpace(character) && !char.IsControl(character));

        if (!hasMinimumLength || !hasUppercase || !hasDigit || !hasSpecial)
            throw new DomainValidationException(Description);
    }
}
