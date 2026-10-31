using UnityEngine;
using UnityEngine.SceneManagement;

// Menu de pausa no mesmo estilo do menu principal. Abre e fecha com ESC em qualquer cena,
// sem precisar de nada na cena. Uso por código: MenuPausa.Abrir();  MenuPausa.Fechar();
public class MenuPausa : TelaAbismal
{
    static MenuPausa instance;
    static int frameFechado = -1;

    // true enquanto a pausa estiver na tela
    public static bool Aberto { get { return instance != null; } }
    public static bool FechouNesteFrame { get { return frameFechado == Time.frameCount; } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CriarAtalho()
    {
        GameObject go = new GameObject("AtalhoPausa");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<AtalhoPausa>();
    }

    public static void Abrir()
    {
        if (instance != null || MenuPrincipal.Ativo || FimDeJogo.Ativo || FinalAbismo.Ativo) return;

        Time.timeScale = 0f;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        instance = new GameObject("MenuPausa").AddComponent<MenuPausa>();
        instance.Montar();
    }

    public static void Fechar()
    {
        if (instance == null) return;

        Destroy(instance.gameObject);
        instance = null;
        frameFechado = Time.frameCount;

        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Montar()
    {
        string cena = SceneManager.GetActiveScene().name;

        Montar(new Config
        {
            tituloPequeno = "INVESTIGAÇÃO",
            tituloGrande = "PAUSADA",
            lema = "A escuridão espera por você.",
            escurecerFundo = 0.6f,
            fichaCampos = new[]
            {
                "CASO", "0713",
                "LOCAL", cena,
                "PROVAS COLETADAS", ProvasColetadas(),
                "STATUS", "<color=#B01818>EM ANDAMENTO</color>",
            },
        },
        ("CONTINUAR", Fechar),
        ("CONTROLES", null),
        ("OPÇÕES", null),
        ("MENU PRINCIPAL", SalvarEIrAoMenu),
        ("SALVAR E SAIR", SalvarESair));

        grupoRaiz.alpha = 0f;
        StartCoroutine(Fade(grupoRaiz, 0f, 1f, 0.25f));
    }

    void SalvarEIrAoMenu()
    {
        SaveJogo.Salvar();
        TrocarDeCena(MenuPrincipal.CenaInicial ?? SceneManager.GetActiveScene().name, true);
    }

    static void SalvarESair()
    {
        SaveJogo.Salvar();
        SairDoJogo();
    }

    protected override void Update()
    {
        // mantém o jogo congelado mesmo se outro script mexer no tempo
        if (!saindo) Time.timeScale = 0f;
        base.Update();
    }

    // ESC com a pausa aberta volta para o jogo
    protected override void Voltar() { Fechar(); }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}

// Fica vivo entre as cenas e abre a pausa quando o jogador aperta ESC.
public class AtalhoPausa : MonoBehaviour
{
    void Update()
    {
        // o mesmo ESC que fechou a pausa não pode abrir de novo
        if (Input.GetKeyDown(KeyCode.Escape) && !MenuPausa.Aberto && !MenuPausa.FechouNesteFrame && !FinalAbismo.Ativo)
            MenuPausa.Abrir();
    }
}
