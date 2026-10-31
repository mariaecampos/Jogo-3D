using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Sorteia pontos uniformes em todo o NavMesh da cena (usado para espalhar documentos perdidos).
public static class NavMeshAleatorio
{
    static NavMeshTriangulation tri;
    static float[] areaAcumulada;
    static Scene cenaDoCache;

    static bool Preparar()
    {
        Scene atual = SceneManager.GetActiveScene();
        if (areaAcumulada != null && cenaDoCache == atual) return areaAcumulada.Length > 0;
        cenaDoCache = atual;

        tri = NavMesh.CalculateTriangulation();
        int n = tri.indices.Length / 3;
        areaAcumulada = new float[n];

        float total = 0f;
        for (int t = 0; t < n; t++)
        {
            Vector3 a = tri.vertices[tri.indices[t * 3]];
            Vector3 b = tri.vertices[tri.indices[t * 3 + 1]];
            Vector3 c = tri.vertices[tri.indices[t * 3 + 2]];
            total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            areaAcumulada[t] = total;
        }
        return n > 0;
    }

    public static bool Sortear(out Vector3 ponto)
    {
        ponto = Vector3.zero;
        if (!Preparar()) return false;

        float r = Random.value * areaAcumulada[areaAcumulada.Length - 1];
        int lo = 0, hi = areaAcumulada.Length - 1;
        while (lo < hi)
        {
            int meio = (lo + hi) / 2;
            if (areaAcumulada[meio] < r) lo = meio + 1;
            else hi = meio;
        }

        Vector3 a = tri.vertices[tri.indices[lo * 3]];
        Vector3 b = tri.vertices[tri.indices[lo * 3 + 1]];
        Vector3 c = tri.vertices[tri.indices[lo * 3 + 2]];

        float u = Random.value, v = Random.value;
        if (u + v > 1f) { u = 1f - u; v = 1f - v; }
        ponto = a + u * (b - a) + v * (c - a);
        return true;
    }

    // ponto do NavMesh longe de 'evitar' (tenta várias vezes; se não achar, aceita o mais longe)
    public static bool SortearLongeDe(Vector3 evitar, float distanciaMinima, out Vector3 ponto)
    {
        ponto = Vector3.zero;
        float melhor = -1f;

        for (int i = 0; i < 40; i++)
        {
            if (!Sortear(out Vector3 p)) return false;

            float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(evitar.x, 0f, evitar.z));
            if (d > melhor) { melhor = d; ponto = p; }
            if (d >= distanciaMinima) return true;
        }
        return melhor >= 0f;
    }

    // altura do NavMesh logo abaixo de um ponto (para manter a altura original do documento sobre o chão)
    public static bool AlturaDoChao(Vector3 perto, float raio, out float y)
    {
        y = perto.y;
        if (!NavMesh.SamplePosition(perto, out NavMeshHit hit, raio, NavMesh.AllAreas)) return false;
        y = hit.position.y;
        return true;
    }
}
