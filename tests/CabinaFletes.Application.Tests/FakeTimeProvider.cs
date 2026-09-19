namespace CabinaFletes.Application.Tests;

/// <summary>
/// TimeProvider fijo y determinístico. Vive en el namespace raíz de las pruebas a
/// propósito: los namespaces de cada contexto lo ven sin necesitar un using, y no
/// queda escondido dentro del archivo de pruebas de un caso de uso concreto.
/// </summary>
public sealed class FakeTimeProvider : TimeProvider
{
    public DateTimeOffset Fijo { get; set; } = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Fijo;
}
