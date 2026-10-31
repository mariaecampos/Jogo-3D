using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Créditos subindo na tela, no estilo dos menus. Abre no fim do jogo (depois da queda no abismo)
// e pelo botão CRÉDITOS do menu principal. ENTER / ESC / clique pulam.
public class CreditosAbismal : MonoBehaviour
{
    static CreditosAbismal instance;
    public static bool Ativo { get { return instance != null; } }

    // nome do grupo (deixe vazio para não mostrar a linha)
    public static string nomeDoGrupo = "Equipe Detetive Abismal";

    static readonly string[] equipe =
    {
        "Caio Eduardo Carvalho de Souza",
        "Lorena Marques Apolinário",
        "Juliana Gonçalves Ribeiro",
        "Maria Eduarda dos Santos Campos",
    };
    const string professor = "Prof. Emerson de Oliveira Batista";

    const float velocidade = 70f;   // pixels por segundo (na resolução de referência)

    System.Action aoTerminar;
    RectTransform conteudo;
    CanvasGroup grupo;
    float alturaConteudo;
    float tempoFinal = -1f;
    float criadoEm;
    bool fechando;
    readonly List<RectTransform> poeira = new List<RectTransform>();

    // doFinal: true quando vem da cutscene (fundo já está preto e a música é do final)
    public static void Mostrar(bool doFinal, System.Action aoTerminar)
    {
        if (instance != null) return;
        instance = new GameObject("Creditos").AddComponent<CreditosAbismal>();
        instance.aoTerminar = aoTerminar;
        instance.Montar(doFinal);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Montar(bool doFinal)
    {
        TelaAbismal.CarregarFontes();
        criadoEm = Time.unscaledTime;

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 700;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = doFinal ? 1f : 0f;

        RectTransform raiz = (RectTransform)transform;

        Image fundo = TelaAbismal.CriarImagem("Fundo", raiz, Color.black);
        TelaAbismal.Esticar(fundo.rectTransform);

        Image nevoa = TelaAbismal.CriarImagem("Nevoa", raiz, new Color(0.35f, 0.02f, 0.02f, 0.3f));
        nevoa.sprite = TelaAbismal.CriarSpriteGradienteVertical();
        nevoa.raycastTarget = false;
        TelaAbismal.Ancorar(nevoa.rectTransform, Vector2.zero, new Vector2(1f, 0.4f), Vector2.zero, Vector2.zero);

        for (int i = 0; i < 40; i++)
        {
            float tam = Random.Range(2f, 5f);
            Image p = TelaAbismal.CriarImagem("Poeira", raiz,
                new Color(TelaAbismal.corOsso.r, TelaAbismal.corOsso.g, TelaAbismal.corOsso.b, Random.Range(0.05f, 0.25f)));
            p.raycastTarget = false;
            p.rectTransform.anchorMin = p.rectTransform.anchorMax = Vector2.zero;
            p.rectTransform.sizeDelta = new Vector2(tam, tam);
            p.rectTransform.anchoredPosition = new Vector2(Random.Range(0f, 1920f), Random.Range(0f, 1080f));
            poeira.Add(p.rectTransform);
        }

        // tudo o que sobe fica dentro de 'conteudo'
        conteudo = TelaAbismal.CriarRect("Conteudo", raiz);
        conteudo.anchorMin = new Vector2(0.5f, 0f);
        conteudo.anchorMax = new Vector2(0.5f, 0f);
        conteudo.pivot = new Vector2(0.5f, 1f);
        conteudo.sizeDelta = new Vector2(1400f, 10f);

        float y = 0f;
        y = Titulo(y);
        y = Linha(y, "Um jogo de terror e investigação", 30f, TelaAbismal.corCinza, true, 160f);
        if (!string.IsNullOrEmpty(nomeDoGrupo)) y = Linha(y, nomeDoGrupo.ToUpper(), 40f, TelaAbismal.corOsso, false, 120f);

        y = Rotulo(y, "DESENVOLVIDO POR");
        foreach (string nome in equipe) y = Linha(y, nome, 44f, TelaAbismal.corOsso, false, 66f);
        y -= 100f;

        y = Rotulo(y, "ORIENTAÇÃO");
        y = Linha(y, professor, 44f, TelaAbismal.corOsso, false, 160f);

        y = Rotulo(y, "INSTITUIÇÃO");
        y = Linha(y, "UNIFAE", 64f, TelaAbismal.corSangue, false, 80f, TelaAbismal.fonteTitulo);
        y = Linha(y, "3º ano  —  Engenharia de Software", 34f, TelaAbismal.corOsso, false, 56f);
        y = Linha(y, "2026", 28f, TelaAbismal.corCinza, false, 160f);

        y = Rotulo(y, "FEITO COM");
        y = Linha(y, "Unity 6  •  Universal Render Pipeline", 32f, TelaAbismal.corOsso, false, 52f);
        y = Linha(y, "Vuforia Engine  (realidade aumentada)", 32f, TelaAbismal.corOsso, false, 260f);

        y = Linha(y, "OBRIGADO POR JOGAR", 72f, TelaAbismal.corOsso, false, 110f, TelaAbismal.fonteTitulo);
        y = Linha(y, "Algumas verdades estão enterradas fundo demais.", 30f, TelaAbismal.corCinza, true, 0f);

        alturaConteudo = -y;
        conteudo.anchoredPosition = new Vector2(0f, 0f);   // começa logo abaixo da tela

        TextMeshProUGUI dica = TelaAbismal.CriarTexto("Dica", raiz, "ENTER  para pular", TelaAbismal.fonteTexto, 18f,
            new Color(TelaAbismal.corCinza.r, TelaAbismal.corCinza.g, TelaAbismal.corCinza.b, 0.5f));
        dica.characterSpacing = 8f;
        dica.alignment = TextAlignmentOptions.BottomRight;
        TelaAbismal.Ancorar(dica.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-200f, 40f), new Vector2(360f, 30f));
    }

    float Titulo(float y)
    {
        TextMeshProUGUI pequeno = Criar("DETETIVE", TelaAbismal.fonteTexto, 56f, TelaAbismal.corOsso, y, 80f);
        pequeno.characterSpacing = 50f;
        y -= 80f;
        TextMeshProUGUI grande = Criar("ABISMAL", TelaAbismal.fonteTitulo, 170f, TelaAbismal.corSangue, y, 200f);
        grande.characterSpacing = 6f;
        return y - 220f;
    }

    float Rotulo(float y, string texto)
    {
        TextMeshProUGUI t = Criar(texto, TelaAbismal.fonteTexto, 24f, TelaAbismal.corCinza, y, 40f);
        t.characterSpacing = 30f;
        Image linha = TelaAbismal.CriarImagem("Linha", conteudo, TelaAbismal.corSangue);
        TelaAbismal.Ancorar(linha.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 48f), new Vector2(120f, 2f));
        return y - 80f;
    }

    float Linha(float y, string texto, float tamanho, Color cor, bool italico, float espacoDepois, TMP_FontAsset fonte = null)
    {
        TextMeshProUGUI t = Criar(texto, fonte != null ? fonte : TelaAbismal.fonteTexto, tamanho, cor, y, tamanho * 1.4f);
        t.characterSpacing = 4f;
        if (italico) t.fontStyle = FontStyles.Italic;
        return y - tamanho * 1.4f - espacoDepois;
    }

    TextMeshProUGUI Criar(string texto, TMP_FontAsset fonte, float tamanho, Color cor, float y, float altura)
    {
        TextMeshProUGUI t = TelaAbismal.CriarTexto("Texto", conteudo, texto, fonte, tamanho, cor);
        t.alignment = TextAlignmentOptions.Center;
        TelaAbismal.Ancorar(t.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, y - altura * 0.5f), new Vector2(0f, altura));
        return t;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (!fechando) grupo.alpha = Mathf.MoveTowards(grupo.alpha, 1f, dt * 2f);

        foreach (RectTransform p in poeira)
        {
            Vector2 pos = p.anchoredPosition;
            pos.y += 20f * dt;
            if (pos.y > 1090f) { pos.y = -10f; pos.x = Random.Range(0f, 1920f); }
            p.anchoredPosition = pos;
        }

        if (fechando)
        {
            grupo.alpha = Mathf.MoveTowards(grupo.alpha, 0f, dt * 1.5f);
            if (grupo.alpha <= 0f) Terminar();
            return;
        }

        // meio segundo de folga: o ENTER que pulou a narração não pula os créditos junto
        if (Time.unscaledTime - criadoEm > 0.5f &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) ||
             Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(0)))
        {
            fechando = true;
            return;
        }

        // sobe até a última linha ficar no meio da tela, espera um pouco e fecha
        RectTransform raiz = (RectTransform)transform;
        float alvo = alturaConteudo + raiz.rect.height * 0.5f;
        Vector2 a = conteudo.anchoredPosition;
        if (a.y < alvo)
        {
            a.y = Mathf.Min(alvo, a.y + velocidade * dt);
            conteudo.anchoredPosition = a;
        }
        else
        {
            if (tempoFinal < 0f) tempoFinal = Time.unscaledTime + 5f;
            if (Time.unscaledTime >= tempoFinal) fechando = true;
        }
    }

    void Terminar()
    {
        System.Action acao = aoTerminar;
        Destroy(gameObject);
        if (acao != null) acao();
    }
}
