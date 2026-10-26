
using UnityEngine;
using TMPro;

public class GerenciadorDocumento : MonoBehaviour
{
    public static GerenciadorDocumento Instance;

    public TextMeshProUGUI textoContador;
    public int totalDocumentos = 0;

    private int documentosColetados = 0;

    public bool TodosDocumentosColetados()
    {
        return totalDocumentos > 0 &&
               documentosColetados >= totalDocumentos;
    }

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
        if (totalDocumentos <= 0)
            totalDocumentos = FindObjectsOfType<ColetorDocumento>().Length;

        AtualizarUI();
    }

    public void AdicionarDocumento()
    {
        if (TodosDocumentosColetados())
            return;

        documentosColetados++;

        Debug.Log("Documentos: " + documentosColetados +
                  " / " + totalDocumentos);

        AtualizarUI();
    }

    private void AtualizarUI()
    {
        if (textoContador != null)
        {
            textoContador.text = "Documentos: " +
                documentosColetados + " / " + totalDocumentos;
        }
    }
}
