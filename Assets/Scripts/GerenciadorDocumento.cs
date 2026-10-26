using UnityEngine;
using TMPro;

public class GerenciadorDocumento : MonoBehaviour
{
    public static GerenciadorDocumento Instance;

    public TextMeshProUGUI textoContador;

    [Tooltip("0 = conta sozinho quantos documentos existem na cena ao iniciar")]
    public int totalDocumentos = 0;

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
        // se o total não foi informado, conta os documentos que existem na cena
        if (totalDocumentos <= 0)
            totalDocumentos = FindObjectsOfType<ColetorDocumento>().Length;

        AtualizarUI();
    }

    public void AdicionarDocumento()
    {
        documentosColetados++;
        Debug.Log("Documento contabilizado! Total atual: " + documentosColetados + " / " + totalDocumentos);
        AtualizarUI();

        // coletou todos: o jogador vence
        if (totalDocumentos > 0 && documentosColetados >= totalDocumentos)
            FimDeJogo.Vitoria();
    }

    void AtualizarUI()
    {
        if (textoContador != null)
        {
            textoContador.text = "Documentos: " + documentosColetados + " / " + totalDocumentos;
        }
        else
        {
            Debug.LogError("ERRO CRÍTICO: O campo 'Texto Contador' está VAZIO no Inspector do objeto GameManager!");
        }
    }
}