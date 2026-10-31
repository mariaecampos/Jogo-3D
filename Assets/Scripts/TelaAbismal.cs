using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Base visual das telas do jogo (menu principal, pausa e fim de jogo).
// Todas usam o mesmo layout: título à esquerda, botões embaixo dele,
// ficha do caso à direita, vinheta, névoa e poeira.
public abstract class TelaAbismal : MonoBehaviour
{
    // cores do tema
    public static readonly Color corOsso = new Color(0.86f, 0.82f, 0.74f);
    public static readonly Color corCinza = new Color(0.55f, 0.53f, 0.50f);
    public static readonly Color corSangue = new Color(0.62f, 0.07f, 0.07f);
    public static readonly Color corPapel = new Color(0.13f, 0.12f, 0.11f, 0.92f);

    internal static TMP_FontAsset fonteTitulo;
    internal static TMP_FontAsset fonteTexto;

    // o que cada tela precisa informar para montar o layout
    protected class Config
    {
        public string tituloPequeno;
        public string tituloGrande;
        public float tamanhoGrande = 210f;
        public Color corGrande = corSangue;
        public Color corBrilho = new Color(1f, 0.05f, 0.05f, 0.55f);
        public bool piscar = true;
        public string lema;
        public float escurecerFundo = 0.78f;
        public string[] fichaCampos;          // pares: rótulo, valor (aceita rich text)
        public string carimbo = "CONFIDENCIAL";
        public Color corCarimbo = new Color(0.7f, 0.08f, 0.08f, 0.75f);
    }

    protected CanvasGroup grupoRaiz;
    protected Image cortinaPreta;
    protected bool saindo;

    CanvasGroup grupoBotoes;
    CanvasGroup grupoControles;
    CanvasGroup grupoOpcoes;
    TextMeshProUGUI tituloGrande;
    TextMeshProUGUI textoDica;
    Config config;
    int botaoControles = -1;
    int botaoOpcoes = -1;

    // painel de opções (volume): 0 = música, 1 = efeitos
    bool mostrandoOpcoes;
    int opcaoSelecionada;
    readonly TextMeshProUGUI[] rotulosOpcao = new TextMeshProUGUI[2];
    readonly Image[] barrasOpcao = new Image[2];
    readonly Image[] marcasOpcao = new Image[2];
    readonly TextMeshProUGUI[] porcentagensOpcao = new TextMeshProUGUI[2];

    // música desta tela (o volume segue a opção de música)
    AudioSource musicaTela;
    float volumeBaseMusica;
    int frameAberto;

    readonly List<BotaoMenu> botoes = new List<BotaoMenu>();
    readonly List<RectTransform> poeira = new List<RectTransform>();
    readonly List<float> velocidadePoeira = new List<float>();
    int selecionado;
    bool mostrandoControles;

    // nome dos botões + ação; use "CONTROLES" ou "OPÇÕES" com acao null para abrir esses painéis
    protected void Montar(Config cfg, params (string texto, System.Action acao)[] listaBotoes)
    {
        config = cfg;
        frameAberto = Time.frameCount;
        CarregarFontes();
        GarantirEventSystem();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();
        grupoRaiz = gameObject.AddComponent<CanvasGroup>();

        RectTransform raiz = (RectTransform)transform;

        // fundo: escurece a cena e adiciona vinheta
        Image fundo = CriarImagem("Fundo", raiz, new Color(0.02f, 0.02f, 0.03f, cfg.escurecerFundo));
        Esticar(fundo.rectTransform);

        Image vinheta = CriarImagem("Vinheta", raiz, Color.black);
        vinheta.sprite = CriarSpriteVinheta();
        vinheta.raycastTarget = false;
        Esticar(vinheta.rectTransform);

        // névoa avermelhada vinda de baixo
        Image nevoa = CriarImagem("Nevoa", raiz, new Color(0.35f, 0.02f, 0.02f, 0.35f));
        nevoa.sprite = CriarSpriteGradienteVertical();
        nevoa.raycastTarget = false;
        Ancorar(nevoa.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0.45f), Vector2.zero, Vector2.zero);

        CriarPoeira(raiz, 45);

        // ---------- bloco do título (lado esquerdo)
        RectTransform blocoTitulo = CriarRect("Titulo", raiz);
        Ancorar(blocoTitulo, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, 60f), new Vector2(1100f, 460f));
        blocoTitulo.pivot = new Vector2(0f, 0.5f);

        TextMeshProUGUI pequeno = CriarTexto("TituloPequeno", blocoTitulo, cfg.tituloPequeno, fonteTexto, 62f, corOsso);
        pequeno.characterSpacing = 58f;
        Ancorar(pequeno.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, -70f), new Vector2(0f, 90f));
        pequeno.alignment = TextAlignmentOptions.BottomLeft;

        tituloGrande = CriarTexto("TituloGrande", blocoTitulo, cfg.tituloGrande, fonteTitulo, cfg.tamanhoGrande, cfg.corGrande);
        tituloGrande.characterSpacing = 6f;
        tituloGrande.alignment = TextAlignmentOptions.TopLeft;
        Ancorar(tituloGrande.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -205f), new Vector2(0f, 250f));
        AplicarBrilho(tituloGrande, cfg.corBrilho);

        Image linha = CriarImagem("Linha", blocoTitulo, corSangue);
        Ancorar(linha.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(310f, -350f), new Vector2(620f, 3f));

        TextMeshProUGUI lema = CriarTexto("Lema", blocoTitulo, cfg.lema, fonteTexto, 30f, corCinza);
        lema.fontStyle = FontStyles.Italic;
        lema.characterSpacing = 4f;
        lema.alignment = TextAlignmentOptions.TopLeft;
        Ancorar(lema.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(4f, -395f), new Vector2(0f, 50f));

        // ---------- botões (o topo do bloco fica logo abaixo do lema)
        RectTransform blocoBotoes = CriarRect("Botoes", raiz);
        blocoBotoes.pivot = new Vector2(0f, 1f);
        Ancorar(blocoBotoes, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, -150f), new Vector2(700f, 300f));
        grupoBotoes = blocoBotoes.gameObject.AddComponent<CanvasGroup>();

        for (int i = 0; i < listaBotoes.Length; i++)
        {
            System.Action acao = listaBotoes[i].acao;
            if (acao == null && listaBotoes[i].texto == "CONTROLES")
            {
                acao = AbrirControles;
                botaoControles = i;
            }
            if (acao == null && listaBotoes[i].texto == "OPÇÕES")
            {
                acao = AbrirOpcoes;
                botaoOpcoes = i;
            }
            CriarBotao(blocoBotoes, listaBotoes[i].texto, i, acao, true, listaBotoes.Length);
        }
        Selecionar(0);

        CriarFichaDoCaso(raiz, cfg);
        CriarPainelControles(raiz);
        CriarPainelOpcoes(raiz);

        // ---------- rodapé
        textoDica = CriarTexto("Dica", raiz, "W / S  ou  SETAS para escolher   —   ENTER para confirmar",
            fonteTexto, 22f, new Color(corCinza.r, corCinza.g, corCinza.b, 0.7f));
        textoDica.characterSpacing = 6f;
        textoDica.alignment = TextAlignmentOptions.Center;
        Ancorar(textoDica.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 35f), new Vector2(0f, 40f));

        TextMeshProUGUI versao = CriarTexto("Versao", raiz, "CASO 0713  —  v0.1", fonteTexto, 18f,
            new Color(corCinza.r, corCinza.g, corCinza.b, 0.45f));
        versao.characterSpacing = 10f;
        versao.alignment = TextAlignmentOptions.BottomRight;
        Ancorar(versao.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-220f, 30f), new Vector2(400f, 30f));

        // cortina preta por cima de tudo (fades)
        cortinaPreta = CriarImagem("Cortina", raiz, new Color(0f, 0f, 0f, 0f));
        cortinaPreta.raycastTarget = false;
        Esticar(cortinaPreta.rectTransform);
    }

    void CriarFichaDoCaso(RectTransform raiz, Config cfg)
    {
        Image ficha = CriarImagem("FichaDoCaso", raiz, corPapel);
        Ancorar(ficha.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-380f, 40f), new Vector2(460f, 560f));
        ficha.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -4f);
        ficha.raycastTarget = false;

        Outline borda = ficha.gameObject.AddComponent<Outline>();
        borda.effectColor = new Color(corOsso.r, corOsso.g, corOsso.b, 0.18f);
        borda.effectDistance = new Vector2(2f, -2f);

        RectTransform r = ficha.rectTransform;

        TextMeshProUGUI cab = CriarTexto("Cabecalho", r, "DEPARTAMENTO DE POLÍCIA\nARQUIVO DE OCORRÊNCIAS", fonteTexto, 22f, corCinza);
        cab.characterSpacing = 8f;
        cab.alignment = TextAlignmentOptions.TopLeft;
        Ancorar(cab.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -70f), new Vector2(-80f, 80f));

        Image sep = CriarImagem("Sep", r, new Color(corOsso.r, corOsso.g, corOsso.b, 0.25f));
        Ancorar(sep.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -125f), new Vector2(-80f, 2f));

        string corpo = "";
        for (int i = 0; i + 1 < cfg.fichaCampos.Length; i += 2)
        {
            if (i > 0) corpo += "\n\n";
            corpo += "<color=#8C877F>" + cfg.fichaCampos[i] + "</color>\n<size=28>" + cfg.fichaCampos[i + 1] + "</size>";
        }
        TextMeshProUGUI txt = CriarTexto("Corpo", r, corpo, fonteTexto, 20f, corOsso);
        txt.characterSpacing = 4f;
        txt.lineSpacing = -10f;
        txt.alignment = TextAlignmentOptions.TopLeft;
        Ancorar(txt.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -300f), new Vector2(-80f, 320f));

        TextMeshProUGUI carimbo = CriarTexto("Carimbo", r, cfg.carimbo, fonteTitulo, 46f, cfg.corCarimbo);
        carimbo.characterSpacing = 10f;
        carimbo.alignment = TextAlignmentOptions.Center;
        Ancorar(carimbo.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(30f, 48f), new Vector2(460f, 70f));
        carimbo.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
    }

    void CriarPainelControles(RectTransform raiz)
    {
        RectTransform painel = CriarRect("Controles", raiz);
        painel.pivot = new Vector2(0f, 1f);
        Ancorar(painel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, -150f), new Vector2(760f, 330f));
        grupoControles = painel.gameObject.AddComponent<CanvasGroup>();

        string[,] linhas =
        {
            { "W A S D", "Mover" },
            { "MOUSE", "Olhar ao redor" },
            { "SHIFT", "Correr" },
            { "ESPAÇO", "Pular" },
            { "F", "Lanterna" },
            { "E", "Interagir" },
            { "ESC", "Pausar" },
        };

        for (int i = 0; i < linhas.GetLength(0); i++)
        {
            float y = -20f - i * 38f;
            TextMeshProUGUI tecla = CriarTexto("Tecla", painel, linhas[i, 0], fonteTexto, 26f, corSangue);
            tecla.characterSpacing = 8f;
            tecla.alignment = TextAlignmentOptions.Left;
            Ancorar(tecla.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f, y), new Vector2(220f, 38f));

            TextMeshProUGUI acao = CriarTexto("Acao", painel, linhas[i, 1], fonteTexto, 26f, corOsso);
            acao.characterSpacing = 4f;
            acao.alignment = TextAlignmentOptions.Left;
            Ancorar(acao.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(480f, y), new Vector2(500f, 38f));
        }

        BotaoMenu voltar = CriarBotao(painel, "VOLTAR", 0, FecharControles, false);
        voltar.rect.pivot = new Vector2(0.5f, 0f);
        Ancorar(voltar.rect, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 60f));
        voltar.Destacar(true);

        MostrarGrupo(grupoControles, false);
    }

    BotaoMenu CriarBotao(RectTransform pai, string texto, int indice, System.Action acao, bool navegavel = true, int quantidade = 1)
    {
        // com mais de 4 botões eles ficam mais juntos para caber acima do rodapé
        // quanto mais botões, mais juntos, para caber acima do rodapé
        float altura = quantidade > 5 ? 54f : (quantidade > 4 ? 60f : 72f);
        float passo = quantidade > 5 ? 56f : (quantidade > 4 ? 62f : 74f);

        RectTransform r = CriarRect("Botao_" + texto, pai);
        Ancorar(r, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -altura * 0.5f - indice * passo), new Vector2(0f, altura));

        Image area = r.gameObject.AddComponent<Image>();
        area.color = new Color(0f, 0f, 0f, 0f);   // invisível, só recebe o mouse

        Image barra = CriarImagem("Barra", r, corSangue);
        barra.raycastTarget = false;
        Ancorar(barra.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(6f, 0f));

        TextMeshProUGUI t = CriarTexto("Texto", r, texto, fonteTexto, 40f, corCinza);
        t.characterSpacing = 14f;
        t.alignment = TextAlignmentOptions.Left;
        Esticar(t.rectTransform);

        BotaoMenu b = r.gameObject.AddComponent<BotaoMenu>();
        b.Configurar(this, navegavel ? indice : -1, t, barra, acao);
        if (navegavel) botoes.Add(b);
        return b;
    }

    // ---------------------------------------------------------------- lógica

    protected virtual void Update()
    {
        if (config == null || saindo) return;

        if (musicaTela != null) musicaTela.volume = volumeBaseMusica * ConfigAudio.Musica;

        AnimarTitulo();
        AnimarPoeira(Time.unscaledDeltaTime);

        Color c = textoDica.color;
        c.a = 0.35f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f));
        textoDica.color = c;

        // outros scripts (MouseLook, MenuManager) travam o cursor; mantém livre com a tela aberta
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // créditos abertos por cima: eles cuidam do teclado
        if (CreditosAbismal.Ativo) return;

        // ignora a tecla que acabou de abrir a tela
        if (Time.frameCount == frameAberto) return;

        bool confirmar = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
        bool voltar = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace);

        if (mostrandoControles)
        {
            if (confirmar || voltar) FecharControles();
            return;
        }

        if (mostrandoOpcoes)
        {
            if (confirmar || voltar) { FecharOpcoes(); return; }
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow) ||
                Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                opcaoSelecionada = 1 - opcaoSelecionada;
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) MudarVolume(opcaoSelecionada, -0.1f);
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) MudarVolume(opcaoSelecionada, 0.1f);
            AtualizarOpcoes();
            return;
        }

        if (voltar) Voltar();
        if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            Selecionar((selecionado + botoes.Count - 1) % botoes.Count);
        if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
            Selecionar((selecionado + 1) % botoes.Count);
        if (confirmar)
            botoes[selecionado].Clicar();
    }

    // ESC fora do painel de controles
    protected virtual void Voltar() { }

    protected void MudarTextoBotao(int indice, string texto)
    {
        if (indice >= 0 && indice < botoes.Count) botoes[indice].MudarTexto(texto);
    }

    void AnimarTitulo()
    {
        float t = Time.unscaledTime;

        // brilho pulsando devagar
        float pulso = 0.5f + 0.5f * Mathf.Sin(t * 1.3f);
        Color claro = Color.Lerp(config.corGrande, Color.white, 0.25f);
        Color cor = Color.Lerp(config.corGrande, claro, pulso * 0.5f);

        // de vez em quando pisca como uma lâmpada falhando
        if (config.piscar && Mathf.PerlinNoise(t * 7f, 0.37f) > 0.78f)
            cor.a = Random.Range(0.15f, 0.6f);

        tituloGrande.color = cor;
    }

    void CriarPoeira(RectTransform raiz, int quantidade)
    {
        for (int i = 0; i < quantidade; i++)
        {
            float tam = Random.Range(2f, 6f);
            Image p = CriarImagem("Poeira", raiz, new Color(corOsso.r, corOsso.g, corOsso.b, Random.Range(0.05f, 0.3f)));
            p.raycastTarget = false;
            RectTransform r = p.rectTransform;
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.sizeDelta = new Vector2(tam, tam);
            r.anchoredPosition = new Vector2(Random.Range(0f, 1920f), Random.Range(0f, 1080f));
            poeira.Add(r);
            velocidadePoeira.Add(Random.Range(8f, 30f));
        }
    }

    void AnimarPoeira(float dt)
    {
        RectTransform raiz = (RectTransform)transform;
        float largura = raiz.rect.width;
        float altura = raiz.rect.height;
        float t = Time.unscaledTime;

        for (int i = 0; i < poeira.Count; i++)
        {
            Vector2 p = poeira[i].anchoredPosition;
            p.y += velocidadePoeira[i] * dt;
            p.x += Mathf.Sin(t * 0.5f + i) * 10f * dt;
            if (p.y > altura + 10f)
            {
                p.y = -10f;
                p.x = Random.Range(0f, largura);
            }
            poeira[i].anchoredPosition = p;
        }
    }

    public void Selecionar(int indice)
    {
        if (indice < 0 || indice >= botoes.Count) return;
        selecionado = indice;
        for (int i = 0; i < botoes.Count; i++)
            botoes[i].Destacar(i == selecionado);
    }

    // ---------------------------------------------------------------- opções (volume)

    void CriarPainelOpcoes(RectTransform raiz)
    {
        RectTransform painel = CriarRect("Opcoes", raiz);
        painel.pivot = new Vector2(0f, 1f);
        Ancorar(painel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(160f, -150f), new Vector2(820f, 330f));
        grupoOpcoes = painel.gameObject.AddComponent<CanvasGroup>();

        string[] nomes = { "MÚSICA", "EFEITOS" };
        for (int i = 0; i < 2; i++)
        {
            int indice = i;
            float y = -50f - i * 95f;

            Image marca = CriarImagem("Marca", painel, corSangue);
            Ancorar(marca.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(3f, y), new Vector2(6f, 46f));
            marcasOpcao[i] = marca;

            TextMeshProUGUI rotulo = CriarTexto("Rotulo", painel, nomes[i], fonteTexto, 38f, corCinza);
            rotulo.characterSpacing = 14f;
            rotulo.alignment = TextAlignmentOptions.Left;
            Ancorar(rotulo.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(150f, y), new Vector2(240f, 60f));
            rotulosOpcao[i] = rotulo;

            BotaoMenu menos = CriarBotao(painel, "-", 0, () => { opcaoSelecionada = indice; MudarVolume(indice, -0.1f); }, false);
            Ancorar(menos.rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(320f, y), new Vector2(50f, 60f));

            // barra clicável / arrastável
            Image fundo = CriarImagem("Barra", painel, new Color(corCinza.r, corCinza.g, corCinza.b, 0.25f));
            Ancorar(fundo.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(510f, y), new Vector2(300f, 14f));
            fundo.raycastTarget = true;
            BarraVolume arrastar = fundo.gameObject.AddComponent<BarraVolume>();
            arrastar.aoMudar = v => { opcaoSelecionada = indice; DefinirVolume(indice, v); };

            Image cheio = CriarImagem("Cheio", fundo.rectTransform, corSangue);
            cheio.rectTransform.anchorMin = Vector2.zero;
            cheio.rectTransform.anchorMax = Vector2.one;
            cheio.rectTransform.offsetMin = cheio.rectTransform.offsetMax = Vector2.zero;
            barrasOpcao[i] = cheio;

            BotaoMenu mais = CriarBotao(painel, "+", 0, () => { opcaoSelecionada = indice; MudarVolume(indice, 0.1f); }, false);
            Ancorar(mais.rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(700f, y), new Vector2(50f, 60f));

            TextMeshProUGUI pct = CriarTexto("Porcentagem", painel, "", fonteTexto, 30f, corOsso);
            pct.alignment = TextAlignmentOptions.Right;
            Ancorar(pct.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(800f, y), new Vector2(100f, 60f));
            porcentagensOpcao[i] = pct;
        }

        TextMeshProUGUI dica = CriarTexto("Dica", painel, "A / D  ou  SETAS para ajustar", fonteTexto, 20f,
            new Color(corCinza.r, corCinza.g, corCinza.b, 0.7f));
        dica.characterSpacing = 6f;
        dica.alignment = TextAlignmentOptions.Left;
        Ancorar(dica.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(200f, -235f), new Vector2(400f, 30f));

        BotaoMenu voltar = CriarBotao(painel, "VOLTAR", 0, FecharOpcoes, false);
        voltar.rect.pivot = new Vector2(0.5f, 0f);
        Ancorar(voltar.rect, new Vector2(0f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 60f));
        voltar.Destacar(true);

        MostrarGrupo(grupoOpcoes, false);
        AtualizarOpcoes();
    }

    static float LerVolume(int i) { return i == 0 ? ConfigAudio.Musica : ConfigAudio.Efeitos; }

    static void DefinirVolume(int i, float v)
    {
        v = Mathf.Round(Mathf.Clamp01(v) * 20f) / 20f;   // passos de 5%
        if (i == 0) ConfigAudio.Musica = v;
        else ConfigAudio.Efeitos = v;
    }

    void MudarVolume(int i, float delta)
    {
        DefinirVolume(i, LerVolume(i) + delta);

        // prévia do volume de efeitos
        if (i == 1) SomAbismal.TocarInterface("paper");
    }

    void AtualizarOpcoes()
    {
        for (int i = 0; i < 2; i++)
        {
            if (rotulosOpcao[i] == null) continue;
            bool sel = i == opcaoSelecionada;
            float v = LerVolume(i);
            rotulosOpcao[i].color = sel ? corOsso : corCinza;
            marcasOpcao[i].enabled = sel;
            barrasOpcao[i].rectTransform.anchorMax = new Vector2(v, 1f);
            barrasOpcao[i].rectTransform.offsetMax = Vector2.zero;
            porcentagensOpcao[i].text = Mathf.RoundToInt(v * 100f) + "%";
        }
    }

    void AbrirOpcoes()
    {
        mostrandoOpcoes = true;
        opcaoSelecionada = 0;
        MostrarGrupo(grupoBotoes, false);
        MostrarGrupo(grupoOpcoes, true);
        AtualizarOpcoes();
    }

    void FecharOpcoes()
    {
        mostrandoOpcoes = false;
        MostrarGrupo(grupoOpcoes, false);
        MostrarGrupo(grupoBotoes, true);
        if (botaoOpcoes >= 0) Selecionar(botaoOpcoes);
    }

    void AbrirControles()
    {
        mostrandoControles = true;
        MostrarGrupo(grupoBotoes, false);
        MostrarGrupo(grupoControles, true);
    }

    void FecharControles()
    {
        mostrandoControles = false;
        MostrarGrupo(grupoControles, false);
        MostrarGrupo(grupoBotoes, true);
        if (botaoControles >= 0) Selecionar(botaoControles);
    }

    // ---------------------------------------------------------------- ações comuns

    protected static void SairDoJogo()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // escurece a tela e carrega a cena (com ou sem o menu principal)
    protected void TrocarDeCena(string cena, bool abrirMenuPrincipal)
    {
        if (saindo) return;
        saindo = true;
        StopAllCoroutines();
        StartCoroutine(TrocarDeCenaRotina(cena, abrirMenuPrincipal));
    }

    IEnumerator TrocarDeCenaRotina(string cena, bool abrirMenuPrincipal)
    {
        grupoRaiz.interactable = false;
        yield return Fade(cortinaPreta, cortinaPreta.color.a, 1f, 0.6f);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (abrirMenuPrincipal) MenuPrincipal.MostrarNaProximaCena();
        SceneManager.LoadScene(cena);
    }

    // toca uma faixa de Resources/sound-track em loop; o volume segue a opção de música
    protected AudioSource TocarMusica(string nome, float volume)
    {
        AudioClip clip = Resources.Load<AudioClip>("sound-track/" + nome);
        if (clip == null) return null;

        AudioSource musica = gameObject.AddComponent<AudioSource>();
        musica.clip = clip;
        musica.loop = true;
        musica.spatialBlend = 0f;
        musica.volume = volume * ConfigAudio.Musica;
        musica.ignoreListenerPause = true;   // continua tocando com o jogo pausado
        musica.Play();

        musicaTela = musica;
        volumeBaseMusica = volume;
        return musica;
    }

    protected static string ProvasColetadas()
    {
        GerenciadorDocumento g = GerenciadorDocumento.Instance;
        if (g == null || g.totalDocumentos <= 0) return "—";
        return g.DocumentosColetados + " / " + g.totalDocumentos;
    }

    // ---------------------------------------------------------------- utilitários

    internal static void CarregarFontes()
    {
        if (fonteTitulo == null) fonteTitulo = Resources.Load<TMP_FontAsset>("Fonts & Materials/Anton SDF");
        if (fonteTexto == null) fonteTexto = Resources.Load<TMP_FontAsset>("Fonts & Materials/Oswald Bold SDF");
        if (fonteTitulo == null) fonteTitulo = TMP_Settings.defaultFontAsset;
        if (fonteTexto == null) fonteTexto = TMP_Settings.defaultFontAsset;
    }

    static void MostrarGrupo(CanvasGroup g, bool mostrar)
    {
        g.alpha = mostrar ? 1f : 0f;
        g.interactable = mostrar;
        g.blocksRaycasts = mostrar;
    }

    protected static IEnumerator Fade(Image img, float de, float para, float duracao)
    {
        float t = 0f;
        while (t < duracao)
        {
            t += Time.unscaledDeltaTime;
            SetAlpha(img, Mathf.Lerp(de, para, t / duracao));
            yield return null;
        }
        SetAlpha(img, para);
    }

    protected static IEnumerator Fade(CanvasGroup g, float de, float para, float duracao)
    {
        float t = 0f;
        while (t < duracao)
        {
            t += Time.unscaledDeltaTime;
            g.alpha = Mathf.Lerp(de, para, t / duracao);
            yield return null;
        }
        g.alpha = para;
    }

    protected static void SetAlpha(Image img, float a)
    {
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    internal static RectTransform CriarRect(string nome, Transform pai)
    {
        GameObject go = new GameObject(nome, typeof(RectTransform));
        go.transform.SetParent(pai, false);
        return (RectTransform)go.transform;
    }

    internal static Image CriarImagem(string nome, Transform pai, Color cor)
    {
        Image img = CriarRect(nome, pai).gameObject.AddComponent<Image>();
        img.color = cor;
        return img;
    }

    internal static TextMeshProUGUI CriarTexto(string nome, Transform pai, string texto, TMP_FontAsset fonte, float tamanho, Color cor)
    {
        TextMeshProUGUI t = CriarRect(nome, pai).gameObject.AddComponent<TextMeshProUGUI>();
        t.font = fonte;
        t.text = texto;
        t.fontSize = tamanho;
        t.color = cor;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        return t;
    }

    internal static void Esticar(RectTransform r)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = r.offsetMax = Vector2.zero;
    }

    internal static void Ancorar(RectTransform r, Vector2 min, Vector2 max, Vector2 pos, Vector2 tamanho)
    {
        r.anchorMin = min;
        r.anchorMax = max;
        r.anchoredPosition = pos;
        r.sizeDelta = tamanho;
    }

    static void AplicarBrilho(TextMeshProUGUI t, Color cor)
    {
        Material m = t.fontMaterial;   // cria uma cópia só para este texto
        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, cor);
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.9f);
        m.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.6f);
        t.UpdateMeshPadding();
    }

    internal static Sprite CriarSpriteVinheta()
    {
        const int tam = 256;
        Texture2D tex = new Texture2D(tam, tam, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        Vector2 centro = new Vector2(tam * 0.5f, tam * 0.5f);
        for (int y = 0; y < tam; y++)
        {
            for (int x = 0; x < tam; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), centro) / (tam * 0.5f);
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1.25f, d)) * 0.95f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tam, tam), new Vector2(0.5f, 0.5f));
    }

    internal static Sprite CriarSpriteGradienteVertical()
    {
        const int alt = 128;
        Texture2D tex = new Texture2D(1, alt, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < alt; y++)
            tex.SetPixel(0, y, new Color(1f, 1f, 1f, Mathf.Pow(1f - y / (float)(alt - 1), 2f)));
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 1, alt), new Vector2(0.5f, 0.5f));
    }

    static void GarantirEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null) return;
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}

// Botão das telas: realça com o mouse e chama a ação ao clicar.
public class BotaoMenu : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    public RectTransform rect;

    TelaAbismal tela;
    int indice;
    TextMeshProUGUI texto;
    Image barra;
    System.Action acao;
    bool destacado;
    float animacao;

    public void Configurar(TelaAbismal tela, int indice, TextMeshProUGUI texto, Image barra, System.Action acao)
    {
        this.tela = tela;
        this.indice = indice;
        this.texto = texto;
        this.barra = barra;
        this.acao = acao;
        rect = (RectTransform)transform;
    }

    public void Destacar(bool valor) { destacado = valor; }

    public void MudarTexto(string valor) { texto.text = valor; }

    public void Clicar() { if (acao != null) acao(); }

    public void OnPointerEnter(PointerEventData e)
    {
        if (indice >= 0) tela.Selecionar(indice);
    }

    public void OnPointerClick(PointerEventData e) { Clicar(); }

    void Update()
    {
        animacao = Mathf.MoveTowards(animacao, destacado ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        float k = Mathf.SmoothStep(0f, 1f, animacao);

        texto.color = Color.Lerp(TelaAbismal.corCinza, new Color(0.95f, 0.92f, 0.86f), k);
        texto.margin = new Vector4(Mathf.Lerp(0f, 34f, k), 0f, 0f, 0f);
        barra.rectTransform.sizeDelta = new Vector2(6f, Mathf.Lerp(0f, 46f, k));
    }
}

// Barra de volume: clicar ou arrastar define o valor (0 a 1) pela posição do mouse.
public class BarraVolume : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    public System.Action<float> aoMudar;

    public void OnPointerDown(PointerEventData e) { Aplicar(e); }
    public void OnDrag(PointerEventData e) { Aplicar(e); }

    void Aplicar(PointerEventData e)
    {
        RectTransform r = (RectTransform)transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(r, e.position, e.pressEventCamera, out Vector2 local)) return;
        float v = Mathf.InverseLerp(r.rect.xMin, r.rect.xMax, local.x);
        if (aoMudar != null) aoMudar(v);
    }
}
