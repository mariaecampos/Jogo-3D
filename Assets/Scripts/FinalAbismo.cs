using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Final do jogo. Quando o crachá abre a última porta, o abismo aparece logo atrás dela, em 3D:
// a sala do outro lado some e no lugar dela há um poço sem fundo, com uma borda de pedra.
// Atravessar o vão dispara a cutscene: o jornalista anda até a beira, olha para baixo,
// a borda desaba e ele cai no abismo (tudo gerado por código).
// Ele veio atrás da história dos interrogatórios ilegais; a narração revela para onde
// os interrogados eram levados. Na queda ele passa pela criatura, que guarda a passagem.
// Depois: citação, título e créditos. ENTER pula.
public class FinalAbismo : MonoBehaviour
{
    public static bool Ativo { get; private set; }

    static readonly string[] narracao =
    {
        "Você veio atrás de uma história.",
        "Interrogatórios ilegais.\nConfissões arrancadas no escuro.",
        "Quem era levado para a última sala\nnunca mais aparecia para depor.",
        "Atrás da última porta\nnão havia sala de interrogatório.",
        "Havia o abismo.",
        "A criatura nunca quis te matar.\nEla guardava a passagem.",
        "Agora você tem a sua história, jornalista.",
        "Mas não sobrou ninguém lá em cima\npara contá-la.",
    };

    // medidas do abismo (escala da cena: ~100x o tamanho real)
    const float RaioPoco = 1100f;
    const float ProfundidadePoco = 60000f;   // fundo que nunca chega: a queda inteira cabe aqui

    Bounds vao;
    bool rodando;
    bool pular;
    int linhaAtual = -1;
    bool narracaoAcabou;

    PlayerMovement jogador;
    Transform cam;
    Vector3 frente;
    Vector3 centroPoco;          // topo do poço (centro)
    Vector3 portaNoChao;         // centro do vão da porta, na altura do chão
    Transform borda;             // a laje de pedra que avança sobre o abismo
    Camera cameraJogo;
    GameObject abismo;
    Light luzDoFundo;
    Transform olhoDoAbismo;      // brilho enorme lá embaixo, que acompanha a queda
    CanvasGroup grupoAbismo;     // a palavra ABISMO, gigante, no momento da revelação
    float tempoAbismo = -1f;
    readonly List<Transform> papeis = new List<Transform>();

    CanvasGroup grupoTexto;
    TextMeshProUGUI textoNarracao;
    CanvasGroup grupoTitulo;
    Image preto;
    AudioSource musica;
    AudioSource ronco;

    // chamado pela porta quando o crachá a destranca: o abismo se abre atrás dela na hora
    public static void Preparar(Bounds vaoDaPorta)
    {
        FinalAbismo f = FindAnyObjectByType<FinalAbismo>();
        if (f == null) f = new GameObject("FinalAbismo").AddComponent<FinalAbismo>();
        f.vao = vaoDaPorta;
        f.AbrirAbismo();
    }

    void AbrirAbismo()
    {
        if (abismo != null) return;
        jogador = FindAnyObjectByType<PlayerMovement>();
        if (jogador == null) return;

        Camera c = jogador.GetComponentInChildren<Camera>();
        cameraJogo = c != null ? c : Camera.main;

        frente = vao.center - jogador.transform.position;
        frente.y = 0f;
        if (frente.sqrMagnitude < 1f) frente = jogador.transform.forward;
        frente.y = 0f;
        frente.Normalize();

        float solo = jogador.transform.position.y;
        portaNoChao = new Vector3(vao.center.x, solo, vao.center.z);
        // o poço começa logo depois da porta; o topo fica um pouco abaixo do chão
        centroPoco = portaNoChao + frente * (RaioPoco + 150f) + Vector3.down * 150f;

        EsconderSalaAlemDaPorta();
        ConstruirAbismo();
        ConstruirBorda();
        CriarBrasasDoPoco();
        StartCoroutine(AvermelharAmbiente());
    }

    // tudo o que fica do outro lado da porta (a sala, os andares de baixo) some: ali agora é o abismo
    void EsconderSalaAlemDaPorta()
    {
        EnemyAI criatura = FindAnyObjectByType<EnemyAI>();
        foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r is ParticleSystemRenderer) continue;
            if (r.transform.IsChildOf(jogador.transform)) continue;
            if (criatura != null && r.transform.IsChildOf(criatura.transform)) continue;

            Bounds b = r.bounds;
            if (b.size.x > 6000f || b.size.z > 6000f) continue;   // peças gigantes (o prédio inteiro) ficam

            Vector3 c = b.center;
            if (Vector3.Dot(c - portaNoChao, frente) < 80f) continue;   // do lado de cá da porta
            Vector2 d = new Vector2(c.x - centroPoco.x, c.z - centroPoco.z);
            if (d.magnitude > RaioPoco * 2.3f) continue;

            r.enabled = false;
        }
    }

    // laje de pedra saindo da porta sobre o vazio, e pedaços do chão quebrado em volta da boca do poço
    void ConstruirBorda()
    {
        Material rocha = Mat(new Color(0.18f, 0.15f, 0.13f), false);

        GameObject laje = Primitiva(PrimitiveType.Cube, "Borda", rocha);
        laje.transform.SetParent(null);
        laje.transform.position = portaNoChao + frente * 320f + Vector3.down * 22f;
        laje.transform.rotation = Quaternion.LookRotation(frente);
        laje.transform.localScale = new Vector3(Mathf.Max(220f, vao.size.x > vao.size.z ? vao.size.x : vao.size.z), 40f, 680f);
        laje.AddComponent<BoxCollider>();
        borda = laje.transform;

        for (int i = 0; i < 46; i++)
        {
            float ang = i * Mathf.PI * 2f / 46f;
            Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
            Vector3 p = new Vector3(centroPoco.x, portaNoChao.y, centroPoco.z) + dir * RaioPoco * Random.Range(0.92f, 1.02f);
            if (Vector3.Dot(p - portaNoChao, frente) < 60f) continue;   // não invade o corredor

            GameObject caco = Primitiva(PrimitiveType.Cube, "ChaoQuebrado", rocha);
            caco.transform.SetParent(null);
            caco.transform.position = p + Vector3.down * Random.Range(10f, 60f);
            caco.transform.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(Random.Range(-35f, 10f), Random.Range(-30f, 30f), Random.Range(-15f, 15f));
            caco.transform.localScale = new Vector3(Random.Range(160f, 320f), Random.Range(25f, 50f), Random.Range(160f, 300f));
        }
    }

    // brasas subindo de dentro do poço (vistas da porta antes da queda)
    void CriarBrasasDoPoco()
    {
        GameObject go = new GameObject("BrasasDoPoco");
        go.transform.position = centroPoco + Vector3.down * 2500f;
        ParticleSystem ps = ConfigurarBrasas(go, 160f, RaioPoco * 0.85f, 4000f);
        var vel = ps.velocityOverLifetime;
        vel.y = new ParticleSystem.MinMaxCurve(350f, 750f);
        var main = ps.main;
        main.startLifetime = 7f;
        ps.Play();

        // ronco grave vindo do fundo, em 3D
        AudioSource s = go.AddComponent<AudioSource>();
        s.clip = CriarRonco();
        s.loop = true;
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = 800f;
        s.maxDistance = 8000f;
        s.volume = 0.8f * ConfigAudio.Efeitos;
        s.Play();
    }

    // a porta se abre e o ar fica vermelho: névoa e fundo da câmera viram a cor do abismo
    IEnumerator AvermelharAmbiente()
    {
        Color corInicial = RenderSettings.fogColor;
        float densidadeInicial = RenderSettings.fogDensity;
        Color corFinal = new Color(0.16f, 0.02f, 0.015f);

        // o que sumiu do outro lado da porta mostra o fundo da câmera: ele vira a cor do abismo
        if (cameraJogo != null)
        {
            cameraJogo.clearFlags = CameraClearFlags.SolidColor;
            cameraJogo.backgroundColor = corInicial;
            cameraJogo.farClipPlane = Mathf.Max(cameraJogo.farClipPlane, 6000f);
        }
        RenderSettings.fog = true;

        float t = 0f;
        while (t < 4f)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 4f);
            RenderSettings.fogColor = Color.Lerp(corInicial, corFinal, k);
            RenderSettings.fogDensity = Mathf.Lerp(densidadeInicial, 0.00045f, k);
            if (cameraJogo != null) cameraJogo.backgroundColor = RenderSettings.fogColor;
            yield return null;
        }
    }

    void OnDestroy()
    {
        if (rodando) Ativo = false;
    }

    void Update()
    {
        if (rodando)
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Escape))
                pular = true;
            return;
        }

        if (jogador == null) jogador = FindAnyObjectByType<PlayerMovement>();
        if (jogador == null || Time.timeScale == 0f) return;

        // entrou no vão da porta (com folga)
        Bounds area = vao;
        area.Expand(new Vector3(120f, 400f, 120f));
        if (area.Contains(jogador.transform.position))
            StartCoroutine(Cutscene());
    }

    // ================================================================ cutscene

    IEnumerator Cutscene()
    {
        rodando = true;
        Ativo = true;
        SaveJogo.Apagar();   // o caso acabou

        MenuPausa.Fechar();
        PrepararCena();
        MontarInterface();
        SomAbismal.Silenciar();

        if (abismo == null) AbrirAbismo();

        jogador.transform.rotation = Quaternion.LookRotation(frente);
        Quaternion olharInicial = cam.localRotation;
        Vector3 posCamera = cam.localPosition;
        Flashlight lanterna = FindAnyObjectByType<Flashlight>();

        // ---- 1) anda devagar pela laje até a beira do abismo
        Vector3 inicio = jogador.transform.position;
        Vector3 beira = new Vector3(portaNoChao.x, inicio.y, portaNoChao.z) + frente * 560f;
        float t = 0f;
        while (t < 4f && !pular)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / 4f);
            jogador.transform.position = Vector3.Lerp(inicio, beira, k);
            cam.localRotation = Quaternion.Slerp(olharInicial, Quaternion.Euler(18f, 0f, 0f), k);
            // passos hesitantes: a câmera balança de leve
            cam.localPosition = posCamera + new Vector3(0f, Mathf.Sin(t * 7f) * 0.012f, 0f);
            ronco.volume = 0.35f * k * ConfigAudio.Efeitos;
            yield return null;
        }

        // ---- 2) na beira: olha para baixo, para dentro do abismo
        t = 0f;
        bool gritou = false;
        while (t < 5f && !pular)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 3f));
            cam.localRotation = Quaternion.Euler(Mathf.Lerp(18f, 72f, k), 8f * Mathf.Sin(t * 0.6f), 0f);
            cam.localPosition = posCamera + Random.insideUnitSphere * 0.004f;
            musica.volume = Mathf.Lerp(0f, 0.5f, t / 5f) * ConfigAudio.Musica;
            if (!musica.isPlaying) musica.Play();
            // um grito sobe lá do fundo
            if (!gritou && t > 2.8f) { gritou = true; SomAbismal.TocarInterface("screams", 0.35f); }
            yield return null;
        }

        // ---- 3) a borda racha e desaba
        if (!pular)
        {
            SomAbismal.TocarInterface("golpe", 1f);
            t = 0f;
            while (t < 1.1f && !pular)
            {
                t += Time.deltaTime;
                float k = t / 1.1f;
                cam.localPosition = posCamera + Random.insideUnitSphere * 0.07f * (1f - k * 0.3f);
                cam.localRotation = Quaternion.Euler(Mathf.Lerp(72f, 40f, k), Random.Range(-4f, 4f), Random.Range(-8f, 8f));
                ronco.volume = 0.9f * ConfigAudio.Efeitos;
                yield return null;
            }
            SomAbismal.TocarInterface("golpe", 0.8f);
        }

        // a laje cai junto (vai girando lá embaixo)
        if (borda != null)
        {
            Collider col = borda.GetComponent<Collider>();
            if (col != null) Destroy(col);
            StartCoroutine(LajeCaindo(borda));
        }
        cam.localPosition = posCamera;
        EntrarNoAbismo();
        CriarBrasas();
        CriarPapeis();
        if (!musica.isPlaying) musica.Play();
        ronco.volume = 0.45f * ConfigAudio.Efeitos;

        // ---- 4) a queda, com a narração por cima
        StartCoroutine(Narracao());
        float velocidade = 150f;
        float giro = 0f;
        t = 0f;
        while (!narracaoAcabou && !pular)
        {
            t += Time.deltaTime;

            // desliza para o meio do poço nos primeiros segundos (longe das paredes)
            Vector3 pos = jogador.transform.position;
            Vector3 meio = new Vector3(centroPoco.x, pos.y, centroPoco.z);
            jogador.transform.position = Vector3.MoveTowards(pos, meio, 220f * Time.deltaTime);

            // começa devagar, olhando direto para o fundo (a revelação do abismo) e vai acelerando
            velocidade = Mathf.MoveTowards(velocidade, t < 7f ? 300f : 650f, 90f * Time.deltaTime);
            jogador.transform.position += Vector3.down * velocidade * Time.deltaTime;

            giro += Mathf.Lerp(4f, 14f, Mathf.Clamp01(t / 12f)) * Time.deltaTime;
            float inclinacao = t < 7f
                ? Mathf.Lerp(25f, 86f, Mathf.SmoothStep(0f, 1f, t / 4f))
                : 66f + 18f * Mathf.Sin((t - 7f) * 0.4f);

            // na palavra ABISMO: tremor e olhar reto para baixo
            if (tempoAbismo >= 0f && Time.time - tempoAbismo < 4f)
            {
                float f = 1f - (Time.time - tempoAbismo) / 4f;
                inclinacao = Mathf.Lerp(inclinacao, 89f, f);
                cam.localPosition = posCamera + Random.insideUnitSphere * 0.03f * f;
            }
            cam.localRotation = Quaternion.Euler(inclinacao, 0f, 6f * Mathf.Sin(t * 0.7f));
            jogador.transform.rotation = Quaternion.LookRotation(frente) * Quaternion.Euler(0f, giro, 0f);
            if (tempoAbismo < 0f || Time.time - tempoAbismo >= 4f)
                cam.localPosition = posCamera + Random.insideUnitSphere * 0.006f;
            AtualizarOlho(3500f, 1f);

            // a lanterna treme de vez em quando
            if (lanterna != null) lanterna.Definir(Random.value > 0.015f);

            AtualizarPapeis(velocidade);
            musica.volume = Mathf.Min(0.8f, t * 0.2f) * ConfigAudio.Musica;
            yield return null;
        }

        // ---- 5) o fundo se aproxima: luz vermelha cresce, a lanterna morre, tudo escurece
        t = 0f;
        while (t < 4f && !pular)
        {
            t += Time.deltaTime;
            float k = t / 4f;
            velocidade += 1500f * Time.deltaTime;
            jogador.transform.position += Vector3.down * velocidade * Time.deltaTime;
            cam.localRotation = Quaternion.Slerp(cam.localRotation, Quaternion.Euler(88f, 0f, 0f), Time.deltaTime * 2f);
            if (luzDoFundo != null)
            {
                luzDoFundo.transform.position = jogador.transform.position + Vector3.down * Mathf.Lerp(3000f, 600f, k);
                luzDoFundo.intensity = Mathf.Lerp(1.5e6f, 6e6f, k);
            }
            AtualizarOlho(Mathf.Lerp(3500f, 400f, k), 1f + 2f * k);
            if (lanterna != null) lanterna.Definir(k < 0.5f && Random.value > 0.3f);
            AtualizarPapeis(velocidade);
            if (k > 0.55f) SetAlpha(preto, (k - 0.55f) / 0.45f);
            yield return null;
        }

        SetAlpha(preto, 1f);
        if (lanterna != null) lanterna.Definir(false);
        ronco.Stop();
        grupoTexto.alpha = 0f;
        musica.volume = 0.8f * ConfigAudio.Musica;

        // ---- 6) citação e título, no escuro
        pular = false;
        yield return MostrarTexto("<i>\"Quando você olha longamente para um abismo,\no abismo também olha para dentro de você.\"</i>\n\n<size=26><color=#8C877F>— Friedrich Nietzsche</color></size>", 5f, true);
        if (!pular) yield return MostrarTitulo();

        // ---- 7) créditos
        pular = false;
        bool acabou = false;
        CreditosAbismal.Mostrar(true, () => acabou = true);
        while (!acabou)
        {
            musica.volume = 0.8f * ConfigAudio.Musica;
            yield return null;
        }

        // ---- 8) volta ao menu
        Ativo = false;
        Time.timeScale = 1f;
        AudioListener.pause = false;
        MenuPrincipal.MostrarNaProximaCena();
        SceneManager.LoadScene(MenuPrincipal.CenaInicial ?? SceneManager.GetActiveScene().name);
    }

    // a narração roda junto com a queda; cada frase dispara um "momento" no abismo
    IEnumerator Narracao()
    {
        yield return Esperar(5f);   // primeiro só o abismo, em silêncio

        for (int i = 0; i < narracao.Length && !pular; i++)
        {
            linhaAtual = i;
            if (i == 1) SomAbismal.TocarInterface("radio_static", 0.35f);
            if (i == 4)
            {
                // "Havia o abismo.": a palavra ABISMO toma a tela
                tempoAbismo = Time.time;
                StartCoroutine(PalavraAbismo());
                SomAbismal.TocarInterface("golpe", 1f);
                SomAbismal.TocarInterface("screams", 0.5f);
                yield return Esperar(4.5f);
                continue;
            }
            if (i == 5) MostrarGuardia();
            yield return MostrarTexto(narracao[i], 3.6f, false);
        }

        narracaoAcabou = true;
    }

    // ABISMO, gigante e vermelho, cresce devagar e some
    IEnumerator PalavraAbismo()
    {
        RectTransform r = (RectTransform)grupoAbismo.transform;
        float t = 0f;
        while (t < 4.5f && !pular)
        {
            t += Time.deltaTime;
            grupoAbismo.alpha = Mathf.Clamp01(Mathf.Min(t / 0.4f, (4.5f - t) / 1.2f));
            r.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.15f, t / 4.5f);
            yield return null;
        }
        grupoAbismo.alpha = 0f;
    }

    // o brilho do fundo fica sempre 'distancia' abaixo de quem cai, pulsando devagar
    void AtualizarOlho(float distancia, float escala)
    {
        if (olhoDoAbismo == null) return;
        olhoDoAbismo.position = jogador.transform.position + Vector3.down * distancia;
        float pulso = 1f + 0.08f * Mathf.Sin(Time.time * 1.7f);
        olhoDoAbismo.localScale = new Vector3(900f, 120f, 900f) * escala * pulso;
    }

    IEnumerator Esperar(float s)
    {
        float t = 0f;
        while (t < s && !pular) { t += Time.deltaTime; yield return null; }
    }

    IEnumerator MostrarTexto(string texto, float tempoNaTela, bool centro)
    {
        textoNarracao.text = texto;
        RectTransform r = textoNarracao.rectTransform;
        r.anchorMin = new Vector2(0f, centro ? 0f : 0.12f);
        r.anchorMax = new Vector2(1f, centro ? 1f : 0.42f);
        r.offsetMin = r.offsetMax = Vector2.zero;

        float total = tempoNaTela + 2f;
        float t = 0f;
        while (t < total && !pular)
        {
            t += Time.deltaTime;
            grupoTexto.alpha = Mathf.Clamp01(Mathf.Min(t, total - t));   // entra em 1 s, fica, sai em 1 s
            yield return null;
        }
        grupoTexto.alpha = 0f;
    }

    IEnumerator MostrarTitulo()
    {
        float t = 0f;
        while (t < 6f && !pular)
        {
            t += Time.deltaTime;
            grupoTitulo.alpha = Mathf.Clamp01(Mathf.Min(t / 1.5f, (6f - t) / 1.2f));
            yield return null;
        }
        grupoTitulo.alpha = 0f;
    }

    // ================================================================ o abismo

    void ConstruirAbismo()
    {
        abismo = new GameObject("Abismo");
        abismo.transform.position = centroPoco;

        Material rocha = Mat(new Color(0.16f, 0.13f, 0.12f), false);
        Material rochaEscura = Mat(new Color(0.08f, 0.07f, 0.07f), false);

        // paredes: tubo irregular com a face para dentro, em pedaços
        // (o URP limita as luzes por objeto; pedaços menores recebem as brasas de perto)
        const int pedacos = 16;
        for (int i = 0; i < pedacos; i++)
        {
            GameObject paredes = new GameObject("Paredes_" + i);
            paredes.transform.SetParent(abismo.transform, false);
            paredes.AddComponent<MeshFilter>().sharedMesh = CriarTubo(i * 5, (i + 1) * 5);
            MeshRenderer mr = paredes.AddComponent<MeshRenderer>();
            mr.sharedMaterial = rocha;
            mr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // pedras salientes nas paredes (dão a sensação de velocidade na queda)
        for (int i = 0; i < 140; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f);
            float prof = Random.Range(200f, ProfundidadePoco * 0.8f);
            Vector3 p = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * RaioPoco * Random.Range(0.86f, 0.98f) + Vector3.down * prof;
            GameObject pedra = Primitiva(PrimitiveType.Cube, "Pedra", Random.value > 0.5f ? rocha : rochaEscura);
            pedra.transform.localPosition = p;
            pedra.transform.localRotation = Random.rotation;
            pedra.transform.localScale = new Vector3(Random.Range(80f, 260f), Random.Range(40f, 140f), Random.Range(80f, 260f));
        }

        // brilho vermelho vindo das profundezas, e brasas ao longo da descida
        for (int i = 1; i <= 12; i++)
        {
            Light l = CriarLuz("Brasa", new Color(1f, 0.18f, 0.08f), 2500f, 9e5f);
            float ang = i * 2.3f;
            l.transform.localPosition = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * RaioPoco * 0.7f + Vector3.down * i * 4500f;
        }
        luzDoFundo = CriarLuz("Fundo", new Color(1f, 0.12f, 0.05f), 6000f, 1.5e6f);
        luzDoFundo.transform.SetParent(null);

        // anéis de rocha a cada trecho: passam pela câmera e dão ritmo à queda
        for (int anel = 1; anel <= 22; anel++)
        {
            float prof = anel * 2600f;
            for (int i = 0; i < 16; i++)
            {
                float ang = (i + anel * 0.37f) * Mathf.PI * 2f / 16f;
                Vector3 dir = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang));
                GameObject pedra = Primitiva(PrimitiveType.Cube, "Anel", i % 2 == 0 ? rocha : rochaEscura);
                pedra.transform.localPosition = dir * RaioPoco * Random.Range(0.72f, 0.86f) + Vector3.down * (prof + Random.Range(-120f, 120f));
                pedra.transform.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(-20f, 20f), Random.Range(0f, 360f));
                pedra.transform.localScale = new Vector3(Random.Range(140f, 260f), Random.Range(60f, 140f), Random.Range(260f, 520f));
            }
        }

        // o "olho" do abismo: brilho incandescente lá no fundo, que nunca chega
        GameObject olho = Primitiva(PrimitiveType.Sphere, "OlhoDoAbismo", Mat(new Color(1f, 0.35f, 0.1f), true));
        olho.transform.SetParent(null);
        olhoDoAbismo = olho.transform;
        olhoDoAbismo.position = centroPoco + Vector3.down * 2500f;   // já visível da porta, lá no fundo
        olhoDoAbismo.localScale = new Vector3(900f, 120f, 900f);

    }

    IEnumerator LajeCaindo(Transform laje)
    {
        float v = 0f;
        Vector3 giro = new Vector3(Random.Range(10f, 30f), Random.Range(-20f, 20f), Random.Range(20f, 45f));
        while (laje != null && v < 6000f)
        {
            v += 900f * Time.deltaTime;
            laje.position += Vector3.down * v * Time.deltaTime;
            laje.Rotate(giro * Time.deltaTime);
            yield return null;
        }
    }

    // trecho do tubo de paredes rochosas, do anel 'de' até o anel 'ate' (normais para dentro)
    static Mesh CriarTubo(int de, int ate)
    {
        const int lados = 48, totalAneis = 80;
        int aneis = ate - de;
        Vector3[] v = new Vector3[(lados + 1) * (aneis + 1)];
        Vector2[] uv = new Vector2[v.Length];
        for (int a = 0; a <= aneis; a++)
        {
            int anel = de + a;
            float y = -ProfundidadePoco * anel / totalAneis;
            for (int l = 0; l <= lados; l++)
            {
                float ang = Mathf.PI * 2f * l / lados;
                // l % lados: a costura do tubo fecha sem degrau
                float ruido = Mathf.PerlinNoise((l % lados) * 0.35f, anel * 0.27f) * 0.45f + Mathf.PerlinNoise((l % lados) * 1.3f, anel * 1.1f) * 0.15f;
                float raio = RaioPoco * (0.85f + ruido);
                v[a * (lados + 1) + l] = new Vector3(Mathf.Cos(ang) * raio, y, Mathf.Sin(ang) * raio);
                uv[a * (lados + 1) + l] = new Vector2(l / (float)lados * 6f, anel / (float)totalAneis * 60f);
            }
        }

        int[] tri = new int[lados * aneis * 6];
        int k = 0;
        for (int a = 0; a < aneis; a++)
        {
            for (int l = 0; l < lados; l++)
            {
                int i0 = a * (lados + 1) + l, i1 = i0 + 1, i2 = i0 + lados + 1, i3 = i2 + 1;
                // sentido horário visto de dentro: a face visível fica do lado de dentro do tubo
                tri[k++] = i0; tri[k++] = i2; tri[k++] = i1;
                tri[k++] = i1; tri[k++] = i2; tri[k++] = i3;
            }
        }

        Mesh m = new Mesh();
        m.vertices = v;
        m.uv = uv;
        m.triangles = tri;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // partículas subindo (brasas), sempre em volta da câmera durante a queda
    void CriarBrasas()
    {
        GameObject go = new GameObject("Brasas");
        go.transform.SetParent(cam, false);
        go.transform.localPosition = Vector3.zero;
        ConfigurarBrasas(go, 140f, 1400f, 4f).Play();
    }

    static ParticleSystem ConfigurarBrasas(GameObject go, float porSegundo, float raio, float vida)
    {
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = vida > 100f ? 7f : vida;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(4f, 12f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.45f, 0.15f), new Color(1f, 0.12f, 0.05f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 800;

        var emissao = ps.emission;
        emissao.rateOverTime = porSegundo;

        var forma = ps.shape;
        forma.shapeType = ParticleSystemShapeType.Sphere;
        forma.radius = raio;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-40f, 40f);
        vel.y = new ParticleSystem.MinMaxCurve(80f, 220f);
        vel.z = new ParticleSystem.MinMaxCurve(-40f, 40f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (s == null) s = Shader.Find("Sprites/Default");
        Material m = new Material(s);
        m.color = new Color(1f, 0.4f, 0.15f);
        renderer.sharedMaterial = m;
        return ps;
    }

    // as folhas das provas caem junto com o jornalista
    void CriarPapeis()
    {
        Material papel = Mat(new Color(0.85f, 0.82f, 0.74f), true);
        for (int i = 0; i < 14; i++)
        {
            GameObject q = Primitiva(PrimitiveType.Quad, "Prova", papel);
            q.transform.SetParent(null);
            q.transform.position = jogador.transform.position + Random.insideUnitSphere * 500f + Vector3.down * Random.Range(100f, 700f);
            q.transform.rotation = Random.rotation;
            q.transform.localScale = new Vector3(30f, 40f, 1f);
            papeis.Add(q.transform);
        }
    }

    void AtualizarPapeis(float velocidadeCamera)
    {
        for (int i = 0; i < papeis.Count; i++)
        {
            Transform p = papeis[i];
            if (p == null) continue;
            // caem um pouco mais devagar que o jogador: vão subindo na tela, girando
            p.position += Vector3.down * velocidadeCamera * 0.82f * Time.deltaTime
                          + new Vector3(Mathf.Sin(Time.time + i), 0f, Mathf.Cos(Time.time * 0.8f + i)) * 60f * Time.deltaTime;
            p.Rotate(new Vector3(90f + i * 7f, 50f, 30f) * Time.deltaTime);

            // ficaram muito para cima: voltam lá para baixo
            if (p.position.y > jogador.transform.position.y + 900f)
                p.position = jogador.transform.position + new Vector3(Random.Range(-500f, 500f), -Random.Range(600f, 1000f), Random.Range(-500f, 500f));
        }
    }

    // a criatura aparece numa saliência mais abaixo, encarando o jornalista que cai
    void MostrarGuardia()
    {
        SomAbismal.TocarInterface("screams", 0.7f);

        EnemyAI criatura = FindAnyObjectByType<EnemyAI>(FindObjectsInactive.Include);
        if (criatura == null) return;

        NavMeshAgent agente = criatura.GetComponent<NavMeshAgent>();
        if (agente != null) agente.enabled = false;

        Vector3 dir = new Vector3(Mathf.Cos(1.1f), 0f, Mathf.Sin(1.1f));
        Vector3 pos = new Vector3(centroPoco.x, jogador.transform.position.y - 1500f, centroPoco.z) + dir * RaioPoco * 0.62f;

        GameObject borda = Primitiva(PrimitiveType.Cube, "BordaDaGuardia", Mat(new Color(0.1f, 0.08f, 0.08f), false));
        borda.transform.SetParent(null);
        borda.transform.position = pos + Vector3.down * 30f;
        borda.transform.localScale = new Vector3(420f, 60f, 420f);

        criatura.transform.position = pos;
        criatura.transform.rotation = Quaternion.LookRotation(-dir);

        Animator anim = criatura.GetComponentInChildren<Animator>();
        if (anim != null)
        {
            anim.speed = 1f;
            if (anim.HasState(0, Animator.StringToHash("Alert"))) anim.CrossFadeInFixedTime("Alert", 0.2f);
        }

        Light l = CriarLuz("LuzDaGuardia", new Color(1f, 0.1f, 0.05f), 900f, 3e5f);
        l.transform.SetParent(null);
        l.transform.position = pos - dir * 250f + Vector3.up * 200f;
    }

    // tira o prédio da iluminação: tudo fica escuro e avermelhado
    void EntrarNoAbismo()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        // névoa vermelha: as paredes de perto ficam escuras e a profundidade "acende" em vermelho
        RenderSettings.fogColor = new Color(0.2f, 0.025f, 0.015f);
        RenderSettings.fogDensity = 0.00024f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.02f, 0.02f);
        if (cameraJogo != null) cameraJogo.backgroundColor = RenderSettings.fogColor;

        // o fundo do poço segue o jogador lá embaixo
        if (luzDoFundo != null) luzDoFundo.transform.position = jogador.transform.position + Vector3.down * 3000f;
    }

    // ================================================================ utilidades

    Light CriarLuz(string nome, Color cor, float alcance, float intensidade)
    {
        GameObject go = new GameObject(nome);
        go.transform.SetParent(abismo != null ? abismo.transform : null, false);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = cor;
        l.range = alcance;
        l.intensity = intensidade;
        l.shadows = LightShadows.None;
        return l;
    }

    GameObject Primitiva(PrimitiveType tipo, string nome, Material m)
    {
        GameObject go = GameObject.CreatePrimitive(tipo);
        go.name = nome;
        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = ShadowCastingMode.Off;
        go.transform.SetParent(abismo.transform, false);
        return go;
    }

    static Material Mat(Color cor, bool semLuz)
    {
        Shader s = Shader.Find(semLuz ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
        if (s == null) s = Shader.Find("Standard");
        Material m = new Material(s);
        m.color = cor;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", cor);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.08f);
        return m;
    }

    // tira o controle do jogador, para a criatura e esconde o resto
    void PrepararCena()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Camera c = jogador.GetComponentInChildren<Camera>();
        cam = c != null ? c.transform : Camera.main.transform;
        if (c != null) c.farClipPlane = Mathf.Max(c.farClipPlane, 6000f);

        foreach (MouseLook m in jogador.GetComponentsInChildren<MouseLook>()) m.enabled = false;
        jogador.enabled = false;
        CharacterController cc = jogador.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        EnemyAI criatura = FindAnyObjectByType<EnemyAI>();
        if (criatura != null)
        {
            criatura.enabled = false;
            NavMeshAgent agente = criatura.GetComponent<NavMeshAgent>();
            if (agente != null && agente.enabled && agente.isOnNavMesh) agente.isStopped = true;
            Animator anim = criatura.GetComponentInChildren<Animator>();
            if (anim != null) anim.speed = 0f;
        }
    }

    void MontarInterface()
    {
        TelaAbismal.CarregarFontes();

        GameObject go = new GameObject("FinalAbismo_UI");
        go.transform.SetParent(transform, false);
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 600;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform raiz = (RectTransform)go.transform;

        // vinheta forte nas bordas e faixas de cinema
        Image vinheta = TelaAbismal.CriarImagem("Vinheta", raiz, new Color(0f, 0f, 0f, 0.85f));
        vinheta.sprite = TelaAbismal.CriarSpriteVinheta();
        TelaAbismal.Esticar(vinheta.rectTransform);

        Image cima = TelaAbismal.CriarImagem("FaixaCima", raiz, Color.black);
        TelaAbismal.Ancorar(cima.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -60f), new Vector2(0f, 120f));
        Image baixo = TelaAbismal.CriarImagem("FaixaBaixo", raiz, Color.black);
        TelaAbismal.Ancorar(baixo.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 60f), new Vector2(0f, 120f));

        preto = TelaAbismal.CriarImagem("Preto", raiz, new Color(0f, 0f, 0f, 0f));
        TelaAbismal.Esticar(preto.rectTransform);

        // narração: faixa escura atrás do texto para ler por cima da imagem
        RectTransform blocoTexto = TelaAbismal.CriarRect("Narracao", raiz);
        TelaAbismal.Esticar(blocoTexto);
        grupoTexto = blocoTexto.gameObject.AddComponent<CanvasGroup>();
        grupoTexto.alpha = 0f;

        Image faixa = TelaAbismal.CriarImagem("Faixa", blocoTexto, new Color(0f, 0f, 0f, 0.55f));
        faixa.sprite = TelaAbismal.CriarSpriteGradienteVertical();
        TelaAbismal.Ancorar(faixa.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);

        textoNarracao = TelaAbismal.CriarTexto("Texto", blocoTexto, "", TelaAbismal.fonteTexto, 46f, TelaAbismal.corOsso);
        textoNarracao.characterSpacing = 6f;
        textoNarracao.lineSpacing = 10f;
        textoNarracao.alignment = TextAlignmentOptions.Center;

        // ABISMO, gigante
        RectTransform blocoAbismo = TelaAbismal.CriarRect("Abismo", raiz);
        TelaAbismal.Esticar(blocoAbismo);
        grupoAbismo = blocoAbismo.gameObject.AddComponent<CanvasGroup>();
        grupoAbismo.alpha = 0f;
        TextMeshProUGUI palavra = TelaAbismal.CriarTexto("Palavra", blocoAbismo, "ABISMO", TelaAbismal.fonteTitulo, 320f, TelaAbismal.corSangue);
        palavra.characterSpacing = 30f;
        palavra.alignment = TextAlignmentOptions.Center;
        TelaAbismal.Esticar(palavra.rectTransform);
        TextMeshProUGUI legenda = TelaAbismal.CriarTexto("Legenda", blocoAbismo, "Havia o abismo.", TelaAbismal.fonteTexto, 40f, TelaAbismal.corOsso);
        legenda.characterSpacing = 10f;
        legenda.fontStyle = FontStyles.Italic;
        legenda.alignment = TextAlignmentOptions.Center;
        TelaAbismal.Ancorar(legenda.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -230f), new Vector2(0f, 60f));

        // cartela de título
        RectTransform blocoTitulo = TelaAbismal.CriarRect("Titulo", raiz);
        TelaAbismal.Esticar(blocoTitulo);
        grupoTitulo = blocoTitulo.gameObject.AddComponent<CanvasGroup>();
        grupoTitulo.alpha = 0f;

        TextMeshProUGUI pequeno = TelaAbismal.CriarTexto("Detetive", blocoTitulo, "DETETIVE", TelaAbismal.fonteTexto, 60f, TelaAbismal.corOsso);
        pequeno.characterSpacing = 56f;
        pequeno.alignment = TextAlignmentOptions.Center;
        TelaAbismal.Ancorar(pequeno.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 150f), new Vector2(0f, 90f));

        TextMeshProUGUI grande = TelaAbismal.CriarTexto("Abismal", blocoTitulo, "ABISMAL", TelaAbismal.fonteTitulo, 200f, TelaAbismal.corSangue);
        grande.characterSpacing = 6f;
        grande.alignment = TextAlignmentOptions.Center;
        TelaAbismal.Ancorar(grande.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, 10f), new Vector2(0f, 240f));

        TextMeshProUGUI carimbo = TelaAbismal.CriarTexto("Carimbo", blocoTitulo, "CASO 0713  —  ENCERRADO", TelaAbismal.fonteTexto, 30f, TelaAbismal.corCinza);
        carimbo.characterSpacing = 20f;
        carimbo.alignment = TextAlignmentOptions.Center;
        TelaAbismal.Ancorar(carimbo.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -150f), new Vector2(0f, 50f));

        TextMeshProUGUI dica = TelaAbismal.CriarTexto("Dica", raiz, "ENTER  para pular", TelaAbismal.fonteTexto, 18f,
            new Color(TelaAbismal.corCinza.r, TelaAbismal.corCinza.g, TelaAbismal.corCinza.b, 0.5f));
        dica.characterSpacing = 8f;
        dica.alignment = TextAlignmentOptions.BottomRight;
        TelaAbismal.Ancorar(dica.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-200f, 40f), new Vector2(360f, 30f));

        // som: música do abismo e um ronco grave da terra cedendo
        musica = gameObject.AddComponent<AudioSource>();
        musica.clip = Resources.Load<AudioClip>("sound-track/abyss_sound");
        musica.loop = true;
        musica.volume = 0f;
        musica.spatialBlend = 0f;

        ronco = gameObject.AddComponent<AudioSource>();
        ronco.clip = CriarRonco();
        ronco.loop = true;
        ronco.volume = 0f;
        ronco.spatialBlend = 0f;
        ronco.Play();
    }

    // ruído grave e contínuo (terra tremendo)
    static AudioClip CriarRonco()
    {
        const int taxa = 44100;
        int n = taxa * 3;
        float[] d = new float[n];
        System.Random r = new System.Random(13);
        float marrom = 0f;
        for (int i = 0; i < n; i++)
        {
            marrom = Mathf.Clamp(marrom + ((float)r.NextDouble() * 2f - 1f) * 0.02f, -1f, 1f);
            float t = i / (float)taxa;
            d[i] = Mathf.Clamp(marrom * 2.5f + Mathf.Sin(2f * Mathf.PI * 38f * t) * 0.25f, -1f, 1f);
        }
        AudioClip c = AudioClip.Create("ronco", n, 1, taxa, false);
        c.SetData(d, 0);
        return c;
    }

    static void SetAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a = Mathf.Clamp01(a);
        img.color = c;
    }
}
