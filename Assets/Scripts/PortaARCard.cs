
using UnityEngine;
using TMPro;

public class PortaARCard : MonoBehaviour
{
    public GameObject cuboPorta;
    public TextMeshProUGUI textoInteracao;
    public CameraScanner cameraScanner;

    private bool jogadorPerto = false;
    private bool portaAberta = false;

    private void Start()
    {
        if (textoInteracao != null)
            textoInteracao.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (!jogadorPerto || portaAberta)
            return;

        bool documentosOk =
            GerenciadorDocumento.Instance != null &&
            GerenciadorDocumento.Instance.TodosDocumentosColetados();

        if (textoInteracao != null)
        {
            textoInteracao.gameObject.SetActive(true);

            textoInteracao.text = documentosOk
                ? "Pressione E para escanear o AR Card"
                : "Encontre todos os documentos";
        }

        if (documentosOk && Input.GetKeyDown(KeyCode.E))
        {
            if (cameraScanner != null)
                cameraScanner.AbrirCamera(this);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            jogadorPerto = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            jogadorPerto = false;

        if (textoInteracao != null)
            textoInteracao.gameObject.SetActive(false);
    }

    public void AbrirPorta()
    {
        portaAberta = true;

        if (cuboPorta != null)
            cuboPorta.SetActive(false);

        if (textoInteracao != null)
            textoInteracao.gameObject.SetActive(false);

        Debug.Log("AR Card confirmado! Porta aberta.");
    }
}
