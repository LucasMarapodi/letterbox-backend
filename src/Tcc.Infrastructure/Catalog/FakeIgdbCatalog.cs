using Tcc.Application.Catalog;
using Tcc.Domain.Catalog;

namespace Tcc.Infrastructure.Catalog;

public sealed class FakeIgdbCatalog : IGameCatalog
{
    private readonly IReadOnlyList<Game> games = Array.AsReadOnly(new[]
    {
        new Game("game-1", "Ecos do Horizonte", "Explore os caminhos de um mundo fictício.", null,
            Array.AsReadOnly(new[] { "Aventura" }), Array.AsReadOnly(new[] { "PC" }), new DateOnly(2024, 4, 12)),
        new Game("game-2", "Circuito Aurora", "Corridas noturnas em circuitos de uma cidade fictícia.", null,
            Array.AsReadOnly(new[] { "Corrida" }), Array.AsReadOnly(new[] { "PC", "Console" }), new DateOnly(2025, 2, 20)),
        new Game("game-3", "Atlas de Cinzas", "Descubra histórias esquecidas em uma aventura de fantasia.", null,
            Array.AsReadOnly(new[] { "RPG", "Aventura" }), Array.AsReadOnly(new[] { "PC" }), null)
    });

    public IReadOnlyList<Game> All() => games;
    public Game? Find(string id) => games.FirstOrDefault(game => game.Id == id);
}
