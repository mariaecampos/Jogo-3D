
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// HUD da cena. A pausa agora é a tela MenuPausa (abre com ESC sozinha);
// os botões daqui só chamam ela. O painelMenuPause antigo fica sempre desligado.
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

        // a HUD agora é a HudAbismal; o painel antigo fica desligado
        if (painelMenuIndicadores != null)
            painelMenuIndicadores.SetActive(false);

        // o menu principal controla o tempo e o cursor enquanto estiver aberto
        if (MenuPrincipal.Ativo)
            return;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
    }

    public void AbrirPause()
    {
        MenuPausa.Abrir();
    }

    public void FecharPause()
    {
        MenuPausa.Fechar();
    }

    public void SairDoJogo()
    {
        SaveJogo.Salvar();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void GarantirEventSystem()
    {
        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }
}
