namespace AiService.Infrastructure.Qdrant;

/// <summary>
/// Deterministisches, abhaengigkeitsfreies Bag-of-Words-Hashing in einen festen Vektor (Groesse
/// <see cref="VectorSize"/>). KNOWN SIMPLIFICATION: kein echtes OpenAI-Embedding-Modell - fuer die
/// semantische Vorauswahl von Katalog-Leistungen (Requirement "Vektorsuche (Qdrant)") reicht eine
/// einfache, lokale Textaehnlichkeit (Cosine-Similarity auf Wort-Hashes) aus, ohne einen zweiten
/// externen KI-API-Call pro Indexierung/Suche zu benoetigen - kein Gold-Plating.
/// </summary>
public static class SimpleTextEmbedding
{
    public const int VectorSize = 64;

    public static float[] Embed(string text)
    {
        var vector = new float[VectorSize];

        var words = text
            .ToLowerInvariant()
            .Split([' ', '\t', '\n', ',', '.', '-', '+'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var word in words)
        {
            var bucket = Math.Abs(word.GetHashCode()) % VectorSize;
            vector[bucket] += 1f;
        }

        var magnitude = MathF.Sqrt(vector.Sum(v => v * v));
        if (magnitude > 0f)
        {
            for (var i = 0; i < vector.Length; i++)
            {
                vector[i] /= magnitude;
            }
        }

        return vector;
    }
}
