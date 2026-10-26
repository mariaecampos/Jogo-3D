using UnityEngine;
using TMPro;

public class GerenciadorDocumento : MonoBehaviour
{
    public static GerenciadorDocumento Instance;


    public TextMeshProUGUI textoContador;

    private int documentosColetados = 0;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        AtualizarUI();
    }

    public void AdicionarDocumento()
    {
        documentosColetados++;
        Debug.Log("Documento contabilizado! Total atual: " + documentosColetados);
        AtualizarUI();
    }

    void AtualizarUI()
    {
        if (textoContador != null)
        {
            textoContador.text = "Documentos: " + documentosColetados;
            Debug.Log("Texto da UI atualizado com sucesso para: Documentos: " + documentosColetados);
        }
        else
        {
            Debug.LogError("ERRO CRÍTICO: O campo 'Texto Contador' está VAZIO no Inspector do objeto GameManager!");
        }
    }
}