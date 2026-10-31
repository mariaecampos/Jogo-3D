using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Vuforia;
using Image = UnityEngine.UI.Image;

// Realidade aumentada (Vuforia + webcam):
// - quando a criatura chega perto, mostre o QR code "detetive abismal" e ela fica atordoada e foge;
// - na porta final, mostre o crachá (Image Target "card02") para destrancar.
// Se cria sozinho nas cenas com a criatura ou com a porta do crachá.
public class RealidadeAumentadaQR : MonoBehaviour
{
    [Header("QR code")]
    public string textoEsperado = "detetive abismal";   // o que o QR code diz quando é lido
    public float distanciaAtivacao = 1400f;             // a criatura precisa estar mais perto que isso (escala da cena)
    public float recarga = 20f;                         // segundos entre dois usos

    [Header("Crachá (porta final)")]
    public string nomeDoCracha = "card02";              // nome do Image Target do crachá na base do Vuforia

    [Header("Imagem da câmera")]
    public bool inverterVertical = true;    // a textura do Vuforia chega de ponta-cabeça
    public bool espelharHorizontal = false; // marque se a imagem aparecer espelhada

    [Header("Testes")]
    [Tooltip("Segure esta tecla para simular o QR code / crachá (para testar sem webcam). Desmarque antes de apresentar.")]
    public bool permitirTeclaDeTeste = true;
    public KeyCode teclaDeTeste = KeyCode.Q;

    static RealidadeAumentadaQR instance;

    EnemyAI criatura;
    PortaARCard portaPedindo;      // porta esperando o crachá
    float crachaLidoAte;
    bool vuforiaPronto;
    bool leitorCriado;
    float proximaBusca;
    float prontoEm;
    float mostrarLidoAte;
    string erro;

    // interface
    CanvasGroup grupo;
    RawImage imagemCamera;
    Image borda;
    TextMeshProUGUI textoStatus;
    TextMeshProUGUI textoCamera;
    Image flash;
    float tempoFlash;

    static readonly Color corOsso = TelaAbismal.corOsso;
    static readonly Color corCinza = TelaAbismal.corCinza;
    static readonly Color corSangue = TelaAbismal.corSangue;
    static readonly Color corAmuleto = new Color(0.55f, 0.85f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AoAbrirOJogo()
    {
        Criar();
        SceneManager.sceneLoaded += (cena, modo) => Criar();
    }

    static void Criar()
    {
        if (FindAnyObjectByType<EnemyAI>() == null && FindAnyObjectByType<PortaARCard>() == null) return;
        if (FindAnyObjectByType<RealidadeAumentadaQR>() != null) return;
        new GameObject("RealidadeAumentadaQR").AddComponent<RealidadeAumentadaQR>();
    }

    // a porta final pede o crachá: a janela de RA abre e fica esperando o Image Target
    public static bool PedirCracha(PortaARCard porta)
    {
        if (instance == null) return false;
        instance.portaPedindo = porta;
        return true;
    }

    public static void CancelarCracha(PortaARCard porta)
    {
        if (instance != null && instance.portaPedindo == porta) instance.portaPedindo = null;
    }

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        criatura = FindAnyObjectByType<EnemyAI>();
        MontarInterface();
        IniciarVuforia();
    }

    // ---------------------------------------------------------------- Vuforia

    void IniciarVuforia()
    {
        try
        {
            // o Vuforia precisa de uma câmera própria (ARCamera). Ela renderiza numa textura minúscula,
            // fora da tela: só serve para o Vuforia ler a webcam.
            if (VuforiaBehaviour.Instance == null)
            {
                GameObject go = new GameObject("CameraRA_QRCode");
                Camera cam = go.AddComponent<Camera>();
                cam.targetTexture = new RenderTexture(64, 64, 16);
                cam.cullingMask = 0;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.depth = -100;
                go.AddComponent<VuforiaBehaviour>();
            }

            VuforiaApplication.Instance.OnVuforiaStarted += AoIniciarVuforia;
            if (VuforiaApplication.Instance.IsRunning) AoIniciarVuforia();
        }
        catch (System.Exception e)
        {
            erro = "CÂMERA DE RA INDISPONÍVEL";
            Debug.LogWarning("RealidadeAumentadaQR: não consegui iniciar o Vuforia: " + e.Message);
        }
    }

    void AoIniciarVuforia()
    {
        vuforiaPronto = true;
        if (leitorCriado || VuforiaBehaviour.Instance == null) return;

        try
        {
            HashSet<BarcodeBehaviour.BarcodeType> tipos = new HashSet<BarcodeBehaviour.BarcodeType> { BarcodeBehaviour.BarcodeType.QRCODE };
            VuforiaBehaviour.Instance.ObserverFactory.CreateBarcodeBehaviour(tipos, false);
            leitorCriado = true;
        }
        catch (System.Exception e)
        {
            erro = "LEITOR DE QR CODE INDISPONÍVEL";
            Debug.LogWarning("RealidadeAumentadaQR: não consegui criar o leitor de QR code: " + e.Message);
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        if (VuforiaApplication.Instance != null)
            VuforiaApplication.Instance.OnVuforiaStarted -= AoIniciarVuforia;
    }

    // algum QR code com o texto certo está na frente da câmera agora?
    bool QRCodeVisivel()
    {
        if (permitirTeclaDeTeste && Input.GetKey(teclaDeTeste)) return true;
        if (!leitorCriado) return false;

        string alvo = Normalizar(textoEsperado);
        foreach (BarcodeBehaviour b in FindObjectsByType<BarcodeBehaviour>(FindObjectsSortMode.None))
        {
            Status st = b.TargetStatus.Status;
            if (st != Status.TRACKED && st != Status.EXTENDED_TRACKED) continue;
            if (b.InstanceData == null || string.IsNullOrEmpty(b.InstanceData.Text)) continue;
            if (Normalizar(b.InstanceData.Text).Contains(alvo)) return true;
        }
        return false;
    }

    // o crachá (Image Target) está sendo visto pela câmera agora?
    // aceita o mesmo QR code "detetive abismal" (o crachá do jogador) ou o Image Target do crachá
    bool CrachaVisivel()
    {
        if (QRCodeVisivel()) return true;
        if (!vuforiaPronto) return false;

        foreach (ObserverBehaviour o in FindObjectsByType<ObserverBehaviour>(FindObjectsSortMode.None))
        {
            if (o is BarcodeBehaviour) continue;
            if (!string.IsNullOrEmpty(nomeDoCracha) && o.TargetName != nomeDoCracha) continue;
            Status st = o.TargetStatus.Status;
            if (st == Status.TRACKED || st == Status.EXTENDED_TRACKED) return true;
        }
        return false;
    }

    // "Detetive Abismal!" -> "detetiveabismal" (ignora maiúsculas, acentos, espaços e pontuação)
    static string Normalizar(string s)
    {
        StringBuilder sb = new StringBuilder();
        foreach (char c in s.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- lógica

    void Update()
    {
        bool menuAberto = MenuPrincipal.Ativo || MenuPausa.Aberto || FimDeJogo.Ativo || FinalAbismo.Ativo;

        // porta final esperando o crachá
        if (portaPedindo != null && !menuAberto && CrachaVisivel())
        {
            portaPedindo.AbrirPorta();
            portaPedindo = null;
            crachaLidoAte = Time.time + 2.5f;
            tempoFlash = 1f;
            SomAbismal.TocarInterface("radio_static", 0.9f);
            HudAbismal.Aviso("CRACHÁ RECONHECIDO   —   PORTA DESTRANCADA", corAmuleto);
        }
        bool perto = criatura != null && criatura.Pronto && !criatura.Afastado &&
                     criatura.DistanciaAoJogador < distanciaAtivacao;
        bool recarregando = Time.time < prontoEm;

        if (!menuAberto && Time.unscaledTime >= proximaBusca)
        {
            proximaBusca = Time.unscaledTime + 0.1f;

            if (perto && !recarregando && QRCodeVisivel())
            {
                criatura.Atordoar();
                prontoEm = Time.time + recarga;
                mostrarLidoAte = Time.time + 2.5f;
                tempoFlash = 1f;
                SomAbismal.TocarInterface("radio_static", 0.9f);
                HudAbismal.Aviso("O AMULETO FUNCIONOU   —   A CRIATURA RECUOU", corAmuleto);
            }
        }

        AtualizarInterface(menuAberto, perto, recarregando);
    }

    // ---------------------------------------------------------------- interface

    void MontarInterface()
    {
        TelaAbismal.CarregarFontes();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;   // acima da HUD, abaixo dos menus

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform raiz = (RectTransform)transform;

        // clarão azulado quando o QR code funciona
        flash = TelaAbismal.CriarImagem("Flash", raiz, new Color(corAmuleto.r, corAmuleto.g, corAmuleto.b, 0f));
        flash.raycastTarget = false;
        TelaAbismal.Esticar(flash.rectTransform);

        // janela da câmera (canto inferior direito)
        RectTransform janela = TelaAbismal.CriarRect("JanelaRA", raiz);
        janela.pivot = new Vector2(1f, 0f);
        TelaAbismal.Ancorar(janela, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 100f), new Vector2(400f, 320f));
        grupo = janela.gameObject.AddComponent<CanvasGroup>();
        grupo.alpha = 0f;
        grupo.blocksRaycasts = false;

        Image fundo = TelaAbismal.CriarImagem("Fundo", janela, new Color(0.03f, 0.02f, 0.02f, 0.85f));
        fundo.raycastTarget = false;
        TelaAbismal.Esticar(fundo.rectTransform);

        borda = TelaAbismal.CriarImagem("Borda", janela, corSangue);
        borda.raycastTarget = false;
        TelaAbismal.Ancorar(borda.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -1.5f), new Vector2(0f, 3f));

        TextMeshProUGUI titulo = TelaAbismal.CriarTexto("Titulo", janela, "REALIDADE AUMENTADA", TelaAbismal.fonteTexto, 18f, corCinza);
        titulo.characterSpacing = 18f;
        titulo.alignment = TextAlignmentOptions.TopLeft;
        TelaAbismal.Ancorar(titulo.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(-36f, 26f));

        imagemCamera = CriarRaw(janela);
        TelaAbismal.Ancorar(imagemCamera.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(360f, 200f));

        textoCamera = TelaAbismal.CriarTexto("SemCamera", imagemCamera.rectTransform, "LIGANDO A CÂMERA...", TelaAbismal.fonteTexto, 18f, corCinza);
        textoCamera.characterSpacing = 8f;
        textoCamera.alignment = TextAlignmentOptions.Center;
        TelaAbismal.Esticar(textoCamera.rectTransform);

        textoStatus = TelaAbismal.CriarTexto("Status", janela, "", TelaAbismal.fonteTexto, 22f, corOsso);
        textoStatus.characterSpacing = 6f;
        textoStatus.alignment = TextAlignmentOptions.Center;
        textoStatus.textWrappingMode = TextWrappingModes.Normal;
        TelaAbismal.Ancorar(textoStatus.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 34f), new Vector2(-24f, 56f));
    }

    static RawImage CriarRaw(Transform pai)
    {
        RectTransform r = TelaAbismal.CriarRect("Camera", pai);
        RawImage img = r.gameObject.AddComponent<RawImage>();
        img.color = new Color(0.08f, 0.08f, 0.09f, 1f);
        img.raycastTarget = false;
        return img;
    }

    void AtualizarInterface(bool menuAberto, bool perto, bool recarregando)
    {
        bool lidoAgora = Time.time < mostrarLidoAte;
        bool crachaLido = Time.time < crachaLidoAte;
        bool modoCracha = portaPedindo != null || crachaLido;
        bool mostrar = !menuAberto && (perto || lidoAgora || modoCracha);
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, mostrar ? 1f : 0f, Time.unscaledDeltaTime * 4f);

        // imagem da webcam vinda do Vuforia
        Texture tex = null;
        if (vuforiaPronto && VuforiaBehaviour.Instance != null && VuforiaBehaviour.Instance.VideoBackground != null)
            tex = VuforiaBehaviour.Instance.VideoBackground.VideoBackgroundTexture;
        imagemCamera.texture = tex;
        imagemCamera.uvRect = new Rect(espelharHorizontal ? 1f : 0f, inverterVertical ? 1f : 0f,
                                       espelharHorizontal ? -1f : 1f, inverterVertical ? -1f : 1f);
        imagemCamera.color = tex != null ? Color.white : new Color(0.08f, 0.08f, 0.09f, 1f);
        textoCamera.text = tex != null ? "" : (erro ?? "LIGANDO A CÂMERA...");

        string dicaTeste = permitirTeclaDeTeste ? "\n<size=16><color=#8C877F>(teste: segure " + teclaDeTeste + ")</color></size>" : "";
        Color corBorda;
        if (crachaLido)
        {
            textoStatus.text = "<color=#8CD9FF>CRACHÁ RECONHECIDO!</color>\nA porta se abriu";
            corBorda = corAmuleto;
        }
        else if (portaPedindo != null)
        {
            textoStatus.text = "MOSTRE O CRACHÁ (QR CODE)\nPARA A CÂMERA" + dicaTeste;
            corBorda = corOsso;
            corBorda.a = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
        }
        else if (lidoAgora)
        {
            textoStatus.text = "<color=#8CD9FF>QR CODE LIDO!</color>\nA criatura recuou";
            corBorda = corAmuleto;
        }
        else if (recarregando)
        {
            textoStatus.text = "AMULETO RECARREGANDO\n<color=#8C877F>" + Mathf.CeilToInt(prontoEm - Time.time) + " s</color>";
            corBorda = corCinza;
        }
        else
        {
            textoStatus.text = "ELA ESTÁ PERTO!\nMOSTRE O QR CODE PARA A CÂMERA" + dicaTeste;
            corBorda = corSangue;
            corBorda.a = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
        }
        borda.color = corBorda;

        tempoFlash = Mathf.MoveTowards(tempoFlash, 0f, Time.unscaledDeltaTime * 1.2f);
        Color f = flash.color;
        f.a = 0.45f * tempoFlash * tempoFlash;
        flash.color = f;
    }
}
