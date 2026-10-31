
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GerenciadorDocumento : MonoBehaviour
{
    public static GerenciadorDocumento Instance;

    public TextMeshProUGUI textoContador;
    public int totalDocumentos = 0;
    public bool debugAtivo = true;   // tecla R: pega o próximo documento (para testes)

    [Header("Documentos roubados pela criatura")]
    [Tooltip("Distância mínima do jogador onde um documento roubado pode reaparecer")]
    public float distanciaMinimaReaparecer = 1500f;

    // documentos que o jogador está carregando (podem ser roubados)
    private readonly List<ColetorDocumento> coletados = new List<ColetorDocumento>();

    public int DocumentosColetados { get { return coletados.Count; } }

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        // conta no Awake, antes do save desligar os documentos já pegos
        if (totalDocumentos <= 0)
            totalDocumentos = FindObjectsByType<ColetorDocumento>(FindObjectsSortMode.None).Length;
    }

    private void Start()
    {
        AtualizarUI();
    }

    private void Update()
    {
        if (debugAtivo && Time.timeScale > 0f && Input.GetKeyDown(KeyCode.R))
            ColetarDocumentoDebug();
    }

    public void AdicionarDocumento(ColetorDocumento doc)
    {
        if (doc == null || coletados.Contains(doc))
            return;

        coletados.Add(doc);
        AtualizarUI();
    }

    public void ColetarDocumentoDebug()
    {
        foreach (ColetorDocumento doc in FindObjectsByType<ColetorDocumento>(FindObjectsSortMode.None))
        {
            if (doc.Coletado) continue;
            doc.Coletar();
            Debug.Log("Documento de debug: " + DocumentosColetados + " / " + totalDocumentos);
            return;
        }
        Debug.Log("Todos os documentos já foram contabilizados.");
    }

    // a criatura arranca 'quantidade' provas do jogador; elas reaparecem em lugares aleatórios do mapa.
    // Retorna quantas foram perdidas.
    public int PerderDocumentos(int quantidade, Vector3 posicaoJogador)
    {
        int perdidos = 0;

        while (perdidos < quantidade && coletados.Count > 0)
        {
            int i = Random.Range(0, coletados.Count);
            ColetorDocumento doc = coletados[i];
            coletados.RemoveAt(i);
            if (doc == null) continue;

            Vector3 lugar;
            if (!NavMeshAleatorio.SortearLongeDe(posicaoJogador, distanciaMinimaReaparecer, out lugar))
                lugar = posicaoJogador;   // sem NavMesh: cai ali mesmo

            doc.Reaparecer(lugar);
            perdidos++;
        }

        AtualizarUI();
        return perdidos;
    }

    public bool TodosDocumentosColetados()
    {
        return totalDocumentos > 0 &&
               DocumentosColetados >= totalDocumentos;
    }

    private void AtualizarUI()
    {
        if (textoContador != null)
            textoContador.text = "Documentos: " + DocumentosColetados + " / " + totalDocumentos;
    }
}
