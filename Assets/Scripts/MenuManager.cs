using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    public GameObject painelMenuInicio;
    public GameObject painelMenuPause;
    public GameObject painelMenuIndicadores;

    [Header("Botões (opcional: arraste aqui para ligar sem usar o OnClick)")]
    public Button botaoIniciar;
    public Button botaoPausar;       // botão que abre o pause (se existir no jogo)
    public Button botaoContinuar;    // botão que fecha o pause
    public Button botaoSair;

    [Header("Configuração")]
    public string nomeDaCenaDeJogo = "Jogo";
    [Tooltip("Marque só se o menu inicial for uma cena separada: o botão Iniciar carrega a cena acima.")]
    public bool carregarCenaAoIniciar = false;

    private void Awake()
    {
        GarantirEventSystem();

        // liga os botões por código (funciona mesmo se o OnClick do Inspector tiver se perdido)
        if (botaoIniciar != null) botaoIniciar.onClick.AddListener(IniciarJogo);
        if (botaoPausar != null) botaoPausar.onClick.AddListener(AbrirPause);
        if (botaoContinuar != null) botaoContinuar.onClick.AddListener(FecharPause);
        if (botaoSair != null) botaoSair.onClick.AddListener(SairDoJogo);
    }

    private void Start()
    {
        if (painelMenuInicio != null) painelMenuInicio.SetActive(true);
        if (painelMenuPause != null) painelMenuPause.SetActive(false);
        if (painelMenuIndicadores != null) painelMenuIndicadores.SetActive(false);

        AtualizarEstado();
    }

    private void Update()
    {
        // a tela de vitória / game over cuida do próprio mouse
        if (FimDeJogo.Ativo) return;

        // ESC abre e fecha o pause (só depois de iniciar o jogo)
        if (Input.GetKeyDown(KeyCode.Escape) && !InicioAberto())
        {
            if (PauseAberto()) FecharPause();
            else AbrirPause();
        }

        // repetido a cada frame para nenhum outro script (como o MouseLook) travar o mouse com um menu aberto
        AtualizarEstado();
    }

    public void IniciarJogo()
    {
        Debug.Log("MenuManager: IniciarJogo chamado.");

        if (carregarCenaAoIniciar)
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(nomeDaCenaDeJogo);
            return;
        }

        if (painelMenuInicio != null) painelMenuInicio.SetActive(false);
        if (painelMenuIndicadores != null) painelMenuIndicadores.SetActive(true);

        AtualizarEstado();
    }

    public void AbrirPause()
    {
        Debug.Log("MenuManager: AbrirPause chamado.");
        if (painelMenuPause != null) painelMenuPause.SetActive(true);

        AtualizarEstado();
    }

    public void FecharPause()
    {
        Debug.Log("MenuManager: FecharPause chamado.");
        if (painelMenuPause != null) painelMenuPause.SetActive(false);

        AtualizarEstado();
    }

    public void SairDoJogo()
    {
        Debug.Log("MenuManager: SairDoJogo chamado.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ---------- Mouse e pausa ----------

    bool InicioAberto() { return painelMenuInicio != null && painelMenuInicio.activeSelf; }
    bool PauseAberto() { return painelMenuPause != null && painelMenuPause.activeSelf; }
    bool MenuAberto() { return InicioAberto() || PauseAberto(); }

    // menu aberto: jogo pausado e mouse livre. Menu fechado: jogo rodando e mouse travado no centro.
    void AtualizarEstado()
    {
        if (MenuAberto())
        {
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // sem EventSystem na cena nenhum botão de UI recebe clique
    void GarantirEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        Debug.LogWarning("MenuManager: não havia EventSystem na cena; criei um automaticamente.");
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}