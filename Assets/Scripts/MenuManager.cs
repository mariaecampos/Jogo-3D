
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MenuManager : MonoBehaviour
{
    public GameObject painelMenuInicio;
    public GameObject painelMenuPause;
    public GameObject painelMenuIndicadores;

    public Button botaoIniciar;
    public Button botaoPausar;
    public Button botaoContinuar;
    public Button botaoSair;

    public string nomeDaCenaDeJogo = "Jogo";
    public bool carregarCenaAoIniciar = false;

    private void Awake()
    {
        GarantirEventSystem();

        if (botaoIniciar != null)
            botaoIniciar.onClick.AddListener(IniciarJogo);

        if (botaoPausar != null)
            botaoPausar.onClick.AddListener(AbrirPause);

        if (botaoContinuar != null)
            botaoContinuar.onClick.AddListener(FecharPause);

        if (botaoSair != null)
            botaoSair.onClick.AddListener(SairDoJogo);
    }

    private void Start()
    {
        if (painelMenuInicio != null)
            painelMenuInicio.SetActive(false);

        if (painelMenuPause != null)
            painelMenuPause.SetActive(false);

        if (painelMenuIndicadores != null)
            painelMenuIndicadores.SetActive(true);

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (FimDeJogo.Ativo)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (PauseAberto())
                FecharPause();
            else
                AbrirPause();
        }

        AtualizarEstado();
    }

    public void IniciarJogo()
    {
        Time.timeScale = 1f;

        if (carregarCenaAoIniciar)
        {
            SceneManager.LoadScene(nomeDaCenaDeJogo);
            return;
        }

        if (painelMenuInicio != null)
            painelMenuInicio.SetActive(false);

        if (painelMenuIndicadores != null)
            painelMenuIndicadores.SetActive(true);

        AtualizarEstado();
    }

    public void AbrirPause()
    {
        if (painelMenuPause != null)
            painelMenuPause.SetActive(true);

        AtualizarEstado();
    }

    public void FecharPause()
    {
        if (painelMenuPause != null)
            painelMenuPause.SetActive(false);

        AtualizarEstado();
    }

    public void SairDoJogo()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private bool PauseAberto()
    {
        return painelMenuPause != null &&
               painelMenuPause.activeSelf;
    }

    private bool MenuAberto()
    {
        return PauseAberto();
    }

    private void AtualizarEstado()
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

    private void GarantirEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}
