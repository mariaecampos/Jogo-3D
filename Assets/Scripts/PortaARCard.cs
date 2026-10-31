
using UnityEngine;
using TMPro;

// Porta final da delegacia. Com todas as provas, aperte E e mostre o crachá (Image Target)
// para a webcam: a realidade aumentada reconhece o crachá e a porta se abre.
// Atrás dela começa o final do jogo (FinalAbismo).
public class PortaARCard : MonoBehaviour
{
    public GameObject cuboPorta;
    public TextMeshProUGUI textoInteracao;
    public CameraScanner cameraScanner;   // leitor antigo (webcam + botão), usado só se a RA não existir

    private bool jogadorPerto = false;
    private bool portaAberta = false;
    private bool esperandoCracha = false;

    private void Start()
    {
        // o texto antigo da cena fica escondido: a dica agora aparece na HUD, no estilo do jogo
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

        if (!documentosOk)
        {
            int faltam = GerenciadorDocumento.Instance != null
                ? GerenciadorDocumento.Instance.totalDocumentos - GerenciadorDocumento.Instance.DocumentosColetados
                : 0;
            HudAbismal.MostrarDica("PORTA TRANCADA   <color=#8C877F>—   faltam " + faltam + (faltam == 1 ? " prova" : " provas") + "</color>");
        }
        else if (esperandoCracha)
            HudAbismal.MostrarDica("MOSTRE O CRACHÁ PARA A CÂMERA");
        else
            HudAbismal.MostrarDica("<color=#B01818>[E]</color>   USAR O CRACHÁ");

        if (documentosOk && !esperandoCracha && Input.GetKeyDown(KeyCode.E) && Time.timeScale > 0f)
        {
            if (RealidadeAumentadaQR.PedirCracha(this))
                esperandoCracha = true;
            else if (cameraScanner != null)
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
        {
            jogadorPerto = false;

            // saiu de perto: para de esperar o crachá
            if (esperandoCracha)
            {
                esperandoCracha = false;
                RealidadeAumentadaQR.CancelarCracha(this);
            }
        }

    }

    public void AbrirPorta()
    {
        if (portaAberta)
            return;

        portaAberta = true;
        esperandoCracha = false;

        // o final começa quando o jogador atravessa o vão da porta
        if (cuboPorta != null)
        {
            Renderer r = cuboPorta.GetComponentInChildren<Renderer>();
            Bounds vao = r != null ? r.bounds : new Bounds(cuboPorta.transform.position, Vector3.one * 100f);
            FinalAbismo.Preparar(vao);
            cuboPorta.SetActive(false);
        }

        Debug.Log("Crachá confirmado! Porta aberta.");
    }
}
