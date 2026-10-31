using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// HUD do jogo no mesmo estilo dos menus: provas no canto, estado da lanterna,
// mira e aviso quando uma prova é encontrada. Se cria sozinha nas cenas com jogador.
public class HudAbismal : MonoBehaviour
{
    static HudAbismal instance;

    static readonly Color corOsso = TelaAbismal.corOsso;
    static readonly Color corCinza = TelaAbismal.corCinza;
    static readonly Color corSangue = TelaAbismal.corSangue;

    CanvasGroup grupo;
    TextMeshProUGUI textoProvas;
    TextMeshProUGUI textoObjetivo;
    RectTransform blocoProvas;
    readonly List<Image> marcas = new List<Image>();

    TextMeshProUGUI textoLanternaEstado;
    Image luzLanterna;

    CanvasGroup grupoAviso;
    TextMeshProUGUI textoAviso;
    float tempoAviso;

    // dica de interação (ex.: porta final), mostrada enquanto alguém pedir a cada frame
    CanvasGroup grupoDica;
    TextMeshProUGUI textoDica;
    int frameDica = -10;

    Image flashVermelho;
    Image telaVermelha;
    Image vinhetaPoucaVida;
    readonly List<Image> marcasVida = new List<Image>();
    PlayerMovement jogador;
    float tempoFlash;
    float tempoTremor;
    Transform cameraTremendo;
    Vector3 posicaoCamera;

    Flashlight lanterna;
    int ultimoColetados = -1;
    int ultimoTotal = -1;
    float pulsoProvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AoAbrirOJogo()
    {
        Criar();
        SceneManager.sceneLoaded += (cena, modo) => Criar();
    }

    static void Criar()
    {
        if (FindAnyObjectByType<PlayerMovement>() == null) return;
        if (FindAnyObjectByType<HudAbismal>() != null) return;
        new GameObject("HudAbismal").AddComponent<HudAbismal>();
    }

    // a criatura acertou o jogador: tela vermelha, tremor e aviso
    public static void MostrarAtaque(int provasPerdidas, int vidaRestante)
    {
        if (instance == null) return;
        instance.tempoFlash = 1f;
        instance.tempoTremor = 0.7f;

        if (vidaRestante <= 0) return;   // golpe fatal: a tela de fim de jogo assume

        string texto = vidaRestante == 1 ? "VOCÊ ESTÁ MUITO FERIDO" : "VOCÊ FOI ATACADO";
        if (provasPerdidas == 1) texto += "   —   1 PROVA ROUBADA";
        else if (provasPerdidas > 1) texto += "   —   " + provasPerdidas + " PROVAS ROUBADAS";
        instance.MostrarAviso(texto);
        instance.textoAviso.color = new Color(1f, 0.35f, 0.3f);
    }

    // aviso no centro da tela (usado pela realidade aumentada)
    public static void Aviso(string texto, Color cor)
    {
        if (instance == null) return;
        instance.MostrarAviso(texto);
        instance.textoAviso.color = cor;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Awake()
    {
        instance = this;
        TelaAbismal.CarregarFontes();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // abaixo dos menus (500)

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        grupo = gameObject.AddComponent<CanvasGroup>();
        grupo.blocksRaycasts = false;
        grupo.interactable = false;

        RectTransform raiz = (RectTransform)transform;

        // vinheta leve nas bordas
        Image vinheta = TelaAbismal.CriarImagem("Vinheta", raiz, new Color(0f, 0f, 0f, 0.55f));
        vinheta.sprite = TelaAbismal.CriarSpriteVinheta();
        vinheta.raycastTarget = false;
        TelaAbismal.Esticar(vinheta.rectTransform);

        // vermelho permanente nas bordas quando resta só 1 de vida
        vinhetaPoucaVida = TelaAbismal.CriarImagem("PoucaVida", raiz, new Color(0.5f, 0f, 0f, 0f));
        vinhetaPoucaVida.sprite = TelaAbismal.CriarSpriteVinheta();
        vinhetaPoucaVida.raycastTarget = false;
        TelaAbismal.Esticar(vinhetaPoucaVida.rectTransform);

        // golpe: a tela inteira fica vermelha e as bordas ficam escuras de sangue
        telaVermelha = TelaAbismal.CriarImagem("Dano", raiz, new Color(0.7f, 0f, 0f, 0f));
        telaVermelha.raycastTarget = false;
        TelaAbismal.Esticar(telaVermelha.rectTransform);

        flashVermelho = TelaAbismal.CriarImagem("Ataque", raiz, new Color(0.45f, 0f, 0f, 0f));
        flashVermelho.sprite = TelaAbismal.CriarSpriteVinheta();
        flashVermelho.raycastTarget = false;
        TelaAbismal.Esticar(flashVermelho.rectTransform);

        CriarProvas(raiz);
        CriarLanterna(raiz);
        CriarVida(raiz);
        CriarMira(raiz);
        CriarAviso(raiz);
        CriarDica(raiz);

        // atalho da pausa no canto inferior direito
        TextMeshProUGUI pausa = TelaAbismal.CriarTexto("Pausa", raiz,
            "<color=#9E1212>ESC</color>   PAUSA", TelaAbismal.fonteTexto, 20f,
            new Color(corCinza.r, corCinza.g, corCinza.b, 0.6f));
        pausa.characterSpacing = 10f;
        pausa.alignment = TextAlignmentOptions.BottomRight;
        TelaAbismal.Ancorar(pausa.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-200f, 50f), new Vector2(300f, 30f));

        // a HUD antiga (texto "Documentos: x / y") fica escondida
        GerenciadorDocumento g = GerenciadorDocumento.Instance;
        if (g != null && g.textoContador != null)
            g.textoContador.gameObject.SetActive(false);
    }

    void CriarProvas(RectTransform raiz)
    {
        blocoProvas = TelaAbismal.CriarRect("Provas", raiz);
        blocoProvas.pivot = new Vector2(1f, 1f);
        TelaAbismal.Ancorar(blocoProvas, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -50f), new Vector2(420f, 170f));

        Image linha = TelaAbismal.CriarImagem("Linha", blocoProvas, corSangue);
        TelaAbismal.Ancorar(linha.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-1.5f, -60f), new Vector2(3f, 120f));

        TextMeshProUGUI rotulo = TelaAbismal.CriarTexto("Rotulo", blocoProvas, "PROVAS", TelaAbismal.fonteTexto, 22f, corCinza);
        rotulo.characterSpacing = 30f;
        rotulo.alignment = TextAlignmentOptions.TopRight;
        TelaAbismal.Ancorar(rotulo.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -15f), new Vector2(-24f, 30f));

        textoProvas = TelaAbismal.CriarTexto("Contador", blocoProvas, "0 / 0", TelaAbismal.fonteTitulo, 64f, corOsso);
        textoProvas.characterSpacing = 4f;
        textoProvas.alignment = TextAlignmentOptions.TopRight;
        TelaAbismal.Ancorar(textoProvas.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -70f), new Vector2(-24f, 80f));

        textoObjetivo = TelaAbismal.CriarTexto("Objetivo", blocoProvas, "", TelaAbismal.fonteTexto, 19f, corCinza);
        textoObjetivo.characterSpacing = 6f;
        textoObjetivo.fontStyle = FontStyles.Italic;
        textoObjetivo.alignment = TextAlignmentOptions.TopRight;
        TelaAbismal.Ancorar(textoObjetivo.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -160f), new Vector2(-24f, 30f));
    }

    void MontarMarcas(int total)
    {
        foreach (Image m in marcas) Destroy(m.gameObject);
        marcas.Clear();

        // uma marquinha (página) para cada prova, da direita para a esquerda
        for (int i = 0; i < total; i++)
        {
            Image m = TelaAbismal.CriarImagem("Marca", blocoProvas, corCinza);
            m.raycastTarget = false;
            TelaAbismal.Ancorar(m.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-34f - (total - 1 - i) * 20f, -128f), new Vector2(12f, 16f));
            m.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            marcas.Add(m);
        }
    }

    void CriarLanterna(RectTransform raiz)
    {
        RectTransform bloco = TelaAbismal.CriarRect("Lanterna", raiz);
        bloco.pivot = new Vector2(0f, 0f);
        TelaAbismal.Ancorar(bloco, Vector2.zero, Vector2.zero, new Vector2(60f, 45f), new Vector2(420f, 70f));

        Image linha = TelaAbismal.CriarImagem("Linha", bloco, corSangue);
        TelaAbismal.Ancorar(linha.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1.5f, 0f), new Vector2(3f, 0f));

        // "lâmpada" que acende junto com a lanterna
        luzLanterna = TelaAbismal.CriarImagem("Luz", bloco, corOsso);
        luzLanterna.raycastTarget = false;
        TelaAbismal.Ancorar(luzLanterna.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(14f, 14f));
        luzLanterna.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

        TextMeshProUGUI rotulo = TelaAbismal.CriarTexto("Rotulo", bloco, "LANTERNA", TelaAbismal.fonteTexto, 20f, corCinza);
        rotulo.characterSpacing = 24f;
        rotulo.alignment = TextAlignmentOptions.BottomLeft;
        TelaAbismal.Ancorar(rotulo.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 1f), new Vector2(30f, 0f), new Vector2(-60f, 0f));
        rotulo.rectTransform.offsetMin = new Vector2(60f, 0f);

        textoLanternaEstado = TelaAbismal.CriarTexto("Estado", bloco, "", TelaAbismal.fonteTexto, 28f, corOsso);
        textoLanternaEstado.characterSpacing = 10f;
        textoLanternaEstado.alignment = TextAlignmentOptions.TopLeft;
        textoLanternaEstado.rectTransform.anchorMin = new Vector2(0f, 0f);
        textoLanternaEstado.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        textoLanternaEstado.rectTransform.offsetMin = new Vector2(60f, 0f);
        textoLanternaEstado.rectTransform.offsetMax = Vector2.zero;
    }

    void CriarVida(RectTransform raiz)
    {
        jogador = FindAnyObjectByType<PlayerMovement>();
        int max = jogador != null ? jogador.vidaMaxima : 3;

        RectTransform bloco = TelaAbismal.CriarRect("Vida", raiz);
        bloco.pivot = new Vector2(0f, 0f);
        TelaAbismal.Ancorar(bloco, Vector2.zero, Vector2.zero, new Vector2(60f, 130f), new Vector2(420f, 60f));

        Image linha = TelaAbismal.CriarImagem("Linha", bloco, corSangue);
        TelaAbismal.Ancorar(linha.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1.5f, 0f), new Vector2(3f, 0f));

        TextMeshProUGUI rotulo = TelaAbismal.CriarTexto("Rotulo", bloco, "VIDA", TelaAbismal.fonteTexto, 20f, corCinza);
        rotulo.characterSpacing = 24f;
        rotulo.alignment = TextAlignmentOptions.Left;
        TelaAbismal.Ancorar(rotulo.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(110f, 40f));

        // um losango de sangue para cada golpe que ainda aguenta
        for (int i = 0; i < max; i++)
        {
            Image m = TelaAbismal.CriarImagem("Ponto", bloco, corSangue);
            m.raycastTarget = false;
            TelaAbismal.Ancorar(m.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(190f + i * 34f, 0f), new Vector2(18f, 18f));
            m.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            marcasVida.Add(m);
        }
    }

    void AtualizarVida()
    {
        if (jogador == null) return;
        int vida = jogador.Vida;

        for (int i = 0; i < marcasVida.Count; i++)
        {
            bool cheio = i < vida;
            Color c = cheio ? new Color(0.75f, 0.08f, 0.06f) : new Color(corCinza.r, corCinza.g, corCinza.b, 0.25f);
            // o último ponto pisca quando está por um fio
            if (cheio && vida == 1) c.a = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f);
            marcasVida[i].color = c;
        }

        float alvo = vida == 1 ? 0.35f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f) : 0f;
        Color v = vinhetaPoucaVida.color;
        v.a = Mathf.MoveTowards(v.a, alvo, Time.unscaledDeltaTime);
        vinhetaPoucaVida.color = v;
    }

    void CriarMira(RectTransform raiz)
    {
        Image mira = TelaAbismal.CriarImagem("Mira", raiz, new Color(corOsso.r, corOsso.g, corOsso.b, 0.55f));
        mira.raycastTarget = false;
        TelaAbismal.Ancorar(mira.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 5f));
        mira.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
    }

    // chame a cada frame enquanto a dica deve aparecer; some sozinha quando ninguém pedir mais
    public static void MostrarDica(string texto)
    {
        if (instance == null) return;
        if (instance.textoDica.text != texto) instance.textoDica.text = texto;
        instance.frameDica = Time.frameCount;
    }

    void CriarDica(RectTransform raiz)
    {
        RectTransform r = TelaAbismal.CriarRect("Dica", raiz);
        TelaAbismal.Ancorar(r, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 140f), new Vector2(760f, 64f));
        grupoDica = r.gameObject.AddComponent<CanvasGroup>();
        grupoDica.alpha = 0f;

        Image fundo = TelaAbismal.CriarImagem("Fundo", r, new Color(0.03f, 0.02f, 0.02f, 0.7f));
        fundo.raycastTarget = false;
        TelaAbismal.Esticar(fundo.rectTransform);

        Image barra = TelaAbismal.CriarImagem("Barra", r, corSangue);
        TelaAbismal.Ancorar(barra.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1.5f, 0f), new Vector2(3f, 0f));

        textoDica = TelaAbismal.CriarTexto("Texto", r, "", TelaAbismal.fonteTexto, 26f, corOsso);
        textoDica.characterSpacing = 12f;
        textoDica.alignment = TextAlignmentOptions.Center;
        TelaAbismal.Esticar(textoDica.rectTransform);
    }

    void CriarAviso(RectTransform raiz)
    {
        RectTransform r = TelaAbismal.CriarRect("Aviso", raiz);
        TelaAbismal.Ancorar(r, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 230f), new Vector2(900f, 90f));
        grupoAviso = r.gameObject.AddComponent<CanvasGroup>();
        grupoAviso.alpha = 0f;

        Image fundo = TelaAbismal.CriarImagem("Fundo", r, new Color(0.03f, 0.02f, 0.02f, 0.75f));
        fundo.raycastTarget = false;
        TelaAbismal.Esticar(fundo.rectTransform);

        Image barra = TelaAbismal.CriarImagem("Barra", r, corSangue);
        TelaAbismal.Ancorar(barra.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1.5f), new Vector2(0f, 3f));

        textoAviso = TelaAbismal.CriarTexto("Texto", r, "", TelaAbismal.fonteTexto, 34f, corOsso);
        textoAviso.characterSpacing = 16f;
        textoAviso.alignment = TextAlignmentOptions.Center;
        TelaAbismal.Esticar(textoAviso.rectTransform);
    }

    void MostrarAviso(string texto)
    {
        textoAviso.text = texto;
        textoAviso.color = corOsso;
        tempoAviso = 3f;
    }

    void Update()
    {
        // some quando algum menu estiver por cima
        bool menuAberto = MenuPrincipal.Ativo || MenuPausa.Aberto || FimDeJogo.Ativo || FinalAbismo.Ativo;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, menuAberto ? 0f : 1f, Time.unscaledDeltaTime * 4f);

        AtualizarProvas();
        AtualizarLanterna();
        AtualizarAtaque();
        AtualizarVida();

        bool dicaAtiva = Time.frameCount - frameDica <= 1;
        grupoDica.alpha = Mathf.MoveTowards(grupoDica.alpha, dicaAtiva ? 1f : 0f, Time.unscaledDeltaTime * 6f);

        if (tempoAviso > 0f)
        {
            tempoAviso -= Time.unscaledDeltaTime;
            float alvo = tempoAviso > 0.5f ? 1f : Mathf.Clamp01(tempoAviso / 0.5f);
            grupoAviso.alpha = Mathf.MoveTowards(grupoAviso.alpha, alvo, Time.unscaledDeltaTime * 5f);
        }
        else
        {
            grupoAviso.alpha = 0f;
        }
    }

    void AtualizarProvas()
    {
        GerenciadorDocumento g = GerenciadorDocumento.Instance;
        int coletados = g != null ? g.DocumentosColetados : 0;
        int total = g != null ? g.totalDocumentos : 0;

        if (total != ultimoTotal)
        {
            MontarMarcas(total);
            ultimoTotal = total;
            ultimoColetados = -1;
        }

        if (coletados != ultimoColetados)
        {
            // pegou uma prova agora (não conta a primeira leitura nem o save)
            if (ultimoColetados >= 0 && coletados > ultimoColetados)
            {
                pulsoProvas = 1f;
                MostrarAviso(coletados >= total
                    ? "TODAS AS PROVAS REUNIDAS"
                    : "PROVA ENCONTRADA   " + coletados + " / " + total);
            }
            ultimoColetados = coletados;

            textoProvas.text = coletados + " <size=40><color=#8C877F>/ " + total + "</color></size>";
            for (int i = 0; i < marcas.Count; i++)
                marcas[i].color = i < coletados ? corSangue : new Color(corCinza.r, corCinza.g, corCinza.b, 0.35f);

            if (total > 0 && coletados >= total)
                textoObjetivo.text = "Leve as provas até a porta trancada";
            else if (coletados == 0)
                textoObjetivo.text = "Sem provas, a criatura não perdoa";
            else
                textoObjetivo.text = "Ela rouba provas se te alcançar";
        }

        // o número "pula" e fica vermelho por um instante quando pega uma prova
        pulsoProvas = Mathf.MoveTowards(pulsoProvas, 0f, Time.unscaledDeltaTime * 1.5f);
        textoProvas.rectTransform.localScale = Vector3.one * (1f + 0.25f * pulsoProvas);
        textoProvas.color = Color.Lerp(corOsso, new Color(0.9f, 0.15f, 0.12f), pulsoProvas);
    }

    void AtualizarAtaque()
    {
        // vermelho forte nas bordas que vai sumindo
        tempoFlash = Mathf.MoveTowards(tempoFlash, 0f, Time.unscaledDeltaTime * 0.8f);
        Color c = flashVermelho.color;
        c.a = tempoFlash;
        flashVermelho.color = c;

        Color t = telaVermelha.color;
        t.a = 0.55f * tempoFlash * tempoFlash;   // some mais rápido que as bordas
        telaVermelha.color = t;

        // tremor da câmera (mexe só a posição local; o MouseLook mexe só a rotação)
        if (tempoTremor > 0f)
        {
            if (cameraTremendo == null && Camera.main != null)
            {
                cameraTremendo = Camera.main.transform;
                posicaoCamera = cameraTremendo.localPosition;
            }
            tempoTremor -= Time.deltaTime;
            if (cameraTremendo != null)
            {
                cameraTremendo.localPosition = tempoTremor > 0f
                    ? posicaoCamera + Random.insideUnitSphere * 0.06f * (tempoTremor / 0.7f)
                    : posicaoCamera;
                if (tempoTremor <= 0f) cameraTremendo = null;
            }
        }
    }

    void AtualizarLanterna()
    {
        if (lanterna == null) lanterna = FindAnyObjectByType<Flashlight>();

        bool ligada = lanterna != null && lanterna.Ligada;
        textoLanternaEstado.text = ligada
            ? "LIGADA   <size=20><color=#8C877F>[F]</color></size>"
            : "<color=#8C877F>DESLIGADA</color>   <size=20><color=#9E1212>[F]</color></size>";

        Color c = ligada ? new Color(1f, 0.93f, 0.75f) : new Color(corCinza.r, corCinza.g, corCinza.b, 0.35f);
        if (ligada) c.a = 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 3f);
        luzLanterna.color = c;
    }
}
