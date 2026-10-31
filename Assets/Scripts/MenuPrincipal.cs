using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Menu principal "DETETIVE ABISMAL". Não precisa de nada na cena: ele se cria sozinho
// quando o jogo abre e congela o jogo até o jogador começar.
// Música: Assets/Resources/sound-track/2-16. Fermata in Mistic Air.mp3
public class MenuPrincipal : TelaAbismal
{
    static MenuPrincipal instance;
    static bool jaMostrado;
    static bool mostrarNaProxima;

    // cena em que o jogo começou (para onde o botão "MENU PRINCIPAL" volta)
    public static string CenaInicial { get; private set; }

    // true enquanto o menu principal estiver na tela
    public static bool Ativo { get { return instance != null; } }

    AudioSource musica;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AoAbrirOJogo()
    {
        if (jaMostrado) return;
        jaMostrado = true;
        CenaInicial = SceneManager.GetActiveScene().name;
        SceneManager.sceneLoaded += AoCarregarCena;
        Criar();
    }

    // chamado antes de trocar de cena: o menu aparece assim que a nova cena carregar
    public static void MostrarNaProximaCena() { mostrarNaProxima = true; }

    static void AoCarregarCena(Scene cena, LoadSceneMode modo)
    {
        if (!mostrarNaProxima) return;
        mostrarNaProxima = false;
        Criar();
    }

    static void Criar()
    {
        if (instance == null)
            new GameObject("MenuPrincipal").AddComponent<MenuPrincipal>();
    }

    void Awake()
    {
        instance = this;
        Congelar();

        // com save, aparece "CONTINUAR" e a ficha mostra o último registro
        SaveJogo.Dados save = SaveJogo.Ler();

        string[] ficha = save == null
            ? new[]
            {
                "CASO", "0713",
                "LOCAL", "Delegacia",
                "DENÚNCIA", "Interrogatórios ilegais",
                "STATUS", "<color=#B01818>NÃO RESOLVIDO</color>",
            }
            : new[]
            {
                "CASO", "0713",
                "LOCAL", "Delegacia",
                "ÚLTIMO REGISTRO", save.documentosColetados + " / " + save.totalDocumentos + " provas  —  " + save.data,
                "STATUS", "<color=#B01818>EM ANDAMENTO</color>",
            };

        Config cfg = new Config
        {
            tituloPequeno = "DETETIVE",
            tituloGrande = "ABISMAL",
            lema = "Algumas verdades estão enterradas fundo demais.",
            fichaCampos = ficha,
        };

        if (save == null)
            Montar(cfg,
                ("INICIAR INVESTIGAÇÃO", NovoJogo),
                ("CONTROLES", null),
                ("OPÇÕES", null),
                ("CRÉDITOS", AbrirCreditos),
                ("SAIR", SairDoJogo));
        else
            Montar(cfg,
                ("CONTINUAR", Continuar),
                ("NOVA INVESTIGAÇÃO", NovoJogo),
                ("CONTROLES", null),
                ("OPÇÕES", null),
                ("CRÉDITOS", AbrirCreditos),
                ("SAIR", SairDoJogo));

        SetAlpha(cortinaPreta, 1f);
        musica = TocarMusica("2-16. Fermata in Mistic Air", 0.7f);
    }

    void Start()
    {
        StartCoroutine(Fade(cortinaPreta, 1f, 0f, 1.8f));
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    protected override void Update()
    {
        // outros scripts (MenuManager) soltam o tempo no Start deles; mantém tudo congelado
        if (!saindo) Congelar();
        base.Update();
    }

    static void Congelar()
    {
        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    static void AbrirCreditos()
    {
        CreditosAbismal.Mostrar(false, null);
    }

    void NovoJogo()
    {
        SaveJogo.Apagar();
        Comecar(false);
    }

    void Continuar()
    {
        Comecar(true);
    }

    void Comecar(bool continuar)
    {
        if (saindo) return;
        saindo = true;
        StopAllCoroutines();
        StartCoroutine(Sumir(continuar));
    }

    IEnumerator Sumir(bool continuar)
    {
        grupoRaiz.interactable = false;
        yield return Fade(cortinaPreta, cortinaPreta.color.a, 1f, 0.8f);

        if (continuar)
        {
            SaveJogo.Dados save = SaveJogo.Ler();
            if (save != null && save.cena != SceneManager.GetActiveScene().name)
            {
                // save em outra cena: carrega ela já com o jogo rodando
                Time.timeScale = 1f;
                AudioListener.pause = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                SaveJogo.Continuar();
                yield break;
            }
            SaveJogo.Continuar();   // mesma cena: só reposiciona o jogador e os documentos
        }

        // tira a interface e revela a cena com a cortina sumindo
        foreach (Transform filho in transform)
            if (filho != cortinaPreta.transform)
                filho.gameObject.SetActive(false);

        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        instance = null;   // libera a pausa (ESC) a partir daqui

        float volumeInicial = musica != null ? musica.volume : 0f;
        float tempo = 0f;
        while (tempo < 1.5f)
        {
            tempo += Time.unscaledDeltaTime;
            float k = tempo / 1.5f;
            SetAlpha(cortinaPreta, 1f - k);
            if (musica != null) musica.volume = volumeInicial * (1f - k);
            yield return null;
        }

        Destroy(gameObject);
    }
}
