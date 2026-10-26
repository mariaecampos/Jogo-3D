
using UnityEngine;
using TMPro;

public class GerenciadorDocumento : MonoBehaviour
{
    public static GerenciadorDocumento Instance;

    public TextMeshProUGUI textoContador;
    public int totalDocumentos = 0;
    public bool debugAtivo = true;

    private int documentosColetados = 0;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    private void Start()
    {
        if (totalDocumentos <= 0)
            totalDocumentos = FindObjectsOfType<ColetorDocumento>().Length;

        AtualizarUI();
    }

    private void Update()
    {
        if (debugAtivo && Input.GetKeyDown(KeyCode.R))
            ColetarDocumentoDebug();
    }

    public void AdicionarDocumento()
    {
        if (documentosColetados >= totalDocumentos)
            return;

        documentosColetados++;
        AtualizarUI();
    }

    public void ColetarDocumentoDebug()
    {
        if (!debugAtivo)
            return;

        if (documentosColetados < totalDocumentos)
        {
            documentosColetados++;
            Debug.Log("Documento de debug: " + documentosColetados + " / " + totalDocumentos);
            AtualizarUI();
        }
        else
        {
            Debug.Log("Todos os documentos já foram contabilizados.");
        }
    }

    public bool TodosDocumentosColetados()
    {
        return totalDocumentos > 0 &&
               documentosColetados >= totalDocumentos;
    }

    private void AtualizarUI()
    {
        if (textoContador != null)
            textoContador.text = "Documentos: " + documentosColetados + " / " + totalDocumentos;
    }
}
