using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : MonoBehaviour
{
    public GameObject painelMenuInicio;
    public GameObject painelMenuPause;
    public GameObject painelMenuIndicadores;

    [Header("Configuração")]
    public string nomeDaCenaDeJogo = "Jogo";

    private void Start()
    {
        if (painelMenuInicio != null) painelMenuInicio.SetActive(true);
        if (painelMenuPause != null) painelMenuPause.SetActive(false);
        if (painelMenuIndicadores != null) painelMenuIndicadores.SetActive(false);
    }

    public void IniciarJogo()
    {
        if (painelMenuInicio != null) painelMenuInicio.SetActive(false);
        if (painelMenuIndicadores != null) painelMenuIndicadores.SetActive(true);

    }

    public void AbrirPause()
    {
        if (painelMenuPause != null) painelMenuPause.SetActive(true);
    }

    public void FecharPause()
    {
        if (painelMenuPause != null) painelMenuPause.SetActive(false);
    }
    public void SairDoJogo()
    {
        Debug.Log("Sair do Jogo acionado.");
        Application.Quit();
    }
}