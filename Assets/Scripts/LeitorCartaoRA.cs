
using UnityEngine;
using Vuforia;

public class LeitorCartaoRA : MonoBehaviour
{
    public GameObject porta;
    public GameObject painelScanner;

    private ObserverBehaviour alvo;
    private bool lendo = false;
    private bool desbloqueado = false;

    void Start()
    {
        alvo = GetComponent<ObserverBehaviour>();

        if (alvo != null)
            alvo.OnTargetStatusChanged += AoReconhecerCartao;
    }

    public void IniciarLeitura()
    {
        if (desbloqueado)
            return;

        if (GerenciadorDocumento.Instance == null ||
            !GerenciadorDocumento.Instance.TodosDocumentosColetados())
        {
            Debug.Log("Ainda faltam documentos!");
            return;
        }

        lendo = true;

        if (painelScanner != null)
            painelScanner.SetActive(true);
    }

    private void AoReconhecerCartao(
        ObserverBehaviour behaviour,
        TargetStatus status)
    {
        if (!lendo || desbloqueado)
            return;

        if (status.Status == Status.TRACKED)
        {
            desbloqueado = true;
            lendo = false;

            if (porta != null)
                porta.SetActive(false);

            if (painelScanner != null)
                painelScanner.SetActive(false);

            Debug.Log("Cartão reconhecido! Porta desbloqueada!");
        }
    }

    void OnDestroy()
    {
        if (alvo != null)
            alvo.OnTargetStatusChanged -= AoReconhecerCartao;
    }
}
