using UnityEngine;

public class ColetorDocumento : MonoBehaviour
{
    private AudioSource audioSource;

    // true depois de pego. O objeto não é destruído: fica desligado para poder
    // reaparecer em outro lugar se a criatura roubar a prova.
    public bool Coletado { get; private set; }

    // quanto o documento flutua acima do chão (medido no início, usado ao reaparecer)
    float alturaSobreOChao = -1f;

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
        MedirAltura();
    }

    void MedirAltura()
    {
        if (alturaSobreOChao >= 0f) return;
        if (NavMeshAleatorio.AlturaDoChao(transform.position, 400f, out float chao))
            alturaSobreOChao = Mathf.Max(0f, transform.position.y - chao);
        else
            alturaSobreOChao = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            Coletar();
    }

    public void Coletar()
    {
        if (Coletado)
            return;

        Coletado = true;

        if (GerenciadorDocumento.Instance != null)
            GerenciadorDocumento.Instance.AdicionarDocumento(this);

        // some na hora; o objeto só é desligado depois do som
        MostrarModelo(false);

        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.volume = ConfigAudio.Efeitos;
            audioSource.Play();
            Invoke(nameof(Desligar), audioSource.clip.length);
        }
        else
        {
            SomAbismal.Tocar("paper", transform.position, 1f);
            Desligar();
        }
    }

    void Desligar()
    {
        if (Coletado) gameObject.SetActive(false);
    }

    // a criatura roubou a prova: ela volta para o mapa em 'posicaoNoChao'
    public void Reaparecer(Vector3 posicaoNoChao)
    {
        MedirAltura();
        CancelInvoke();
        Coletado = false;
        transform.position = posicaoNoChao + Vector3.up * alturaSobreOChao;
        gameObject.SetActive(true);
        MostrarModelo(true);
    }

    // usado pelo save: marca como pego sem som e sem contar de novo
    public void MarcarColetado()
    {
        Coletado = true;
        gameObject.SetActive(false);
    }

    void MostrarModelo(bool mostrar)
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = mostrar;
        foreach (Collider c in GetComponentsInChildren<Collider>(true)) c.enabled = mostrar;
    }
}
