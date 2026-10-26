
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CameraScanner : MonoBehaviour
{
    public GameObject painelCamera;
    public RawImage imagemCamera;
    public TextMeshProUGUI textoStatus;
    public Button botaoConfirmar;

    private WebCamTexture webcam;
    private PortaARCard portaAtual;

    private void Start()
    {
        painelCamera.SetActive(false);

        if (botaoConfirmar != null)
            botaoConfirmar.onClick.AddListener(ConfirmarLeitura);
    }

    public void AbrirCamera(PortaARCard porta)
    {
        portaAtual = porta;
        painelCamera.SetActive(true);

        if (textoStatus != null)
            textoStatus.text = "Posicione o AR Card diante da camera";

        if (WebCamTexture.devices.Length == 0)
        {
            if (textoStatus != null)
                textoStatus.text = "Nenhuma webcam encontrada.";

            return;
        }

        if (webcam == null)
        {
            webcam = new WebCamTexture();
            imagemCamera.texture = webcam;
        }

        if (!webcam.isPlaying)
            webcam.Play();
    }

    public void ConfirmarLeitura()
    {
        if (portaAtual == null)
            return;

        if (webcam == null || !webcam.isPlaying)
        {
            if (textoStatus != null)
                textoStatus.text = "Camera indisponivel.";
            return;
        }

        if (textoStatus != null)
            textoStatus.text = "AR Card confirmado!";

        portaAtual.AbrirPorta();
        FecharCamera();
    }

    public void FecharCamera()
    {
        if (webcam != null && webcam.isPlaying)
            webcam.Stop();

        painelCamera.SetActive(false);
        portaAtual = null;
    }

    private void OnDestroy()
    {
        if (webcam != null && webcam.isPlaying)
            webcam.Stop();
    }
}
