using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Sons do jogo, todos carregados de Assets/Resources/sound-track. Se cria sozinho nas cenas com jogador:
// - música ambiente (as duas faixas, alternando) e zumbido de lâmpada baixinho
// - passos do jogador e passos pesados da criatura (3D, dá para ouvir ela chegando)
// - ruídos aleatórios pelo mapa (rádio, estática, máquinas)
// - SomAbismal.Tocar("nome", posição) para efeitos avulsos (grito, papel...)
// - golpe e batimento cardíaco (gerados por código, não precisam de arquivo)
// Volumes seguem ConfigAudio (OPÇÕES > Música / Efeitos).
public class SomAbismal : MonoBehaviour
{
    const string Pasta = "sound-track/";

    // a cena é ~100x maior que o tamanho real, então as distâncias do som também
    const float DistanciaMinima = 150f;
    const float DistanciaMaxima = 4000f;

    static readonly string[] musicas = { "2-07. Ashes and Ghost", "2-16. Fermata in Mistic Air" };
    static readonly string[] ruidos = { "radio_static", "military_radio", "microwave", "factory_buzz" };

    [Range(0f, 1f)] public float volumeMusica = 0.22f;
    [Range(0f, 1f)] public float volumeZumbido = 0.06f;
    [Range(0f, 1f)] public float volumePassos = 0.5f;
    [Range(0f, 1f)] public float volumeCriatura = 1f;

    PlayerMovement jogador;
    EnemyAI criatura;
    NavMeshAgent agenteCriatura;

    AudioSource musica;
    AudioSource zumbidoFonte;
    AudioSource coracao;
    AudioSource passosJogador;
    AudioSource passosCriatura;
    Vector3 ultimaPosCriatura;
    float velocidadeCriatura;
    AudioClip somPasso;
    int faixa;
    float proximoPasso;
    float proximoRuido;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AoAbrirOJogo()
    {
        Criar();
        SceneManager.sceneLoaded += (cena, modo) => Criar();
    }

    static void Criar()
    {
        if (FindAnyObjectByType<PlayerMovement>() == null) return;
        if (FindAnyObjectByType<SomAbismal>() != null) return;
        new GameObject("SomAbismal").AddComponent<SomAbismal>();
    }

    static AudioClip clipGolpe;
    static AudioClip clipBatida;

    static AudioClip Carregar(string nome)
    {
        if (nome == "golpe") return Golpe();
        AudioClip c = Resources.Load<AudioClip>(Pasta + nome);
        if (c == null) Debug.LogWarning("SomAbismal: não achei Resources/" + Pasta + nome);
        return c;
    }

    static void Configurar3D(AudioSource s)
    {
        s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Logarithmic;
        s.minDistance = DistanciaMinima;
        s.maxDistance = DistanciaMaxima;
        s.dopplerLevel = 0f;
        s.playOnAwake = false;
    }

    // efeito avulso em 3D numa posição do mundo
    public static void Tocar(string nome, Vector3 posicao, float volume = 1f)
    {
        AudioClip clip = Carregar(nome);
        if (clip == null) return;

        GameObject go = new GameObject("Som_" + nome);
        go.transform.position = posicao;
        AudioSource s = go.AddComponent<AudioSource>();
        Configurar3D(s);
        s.clip = clip;
        s.volume = volume * ConfigAudio.Efeitos;
        s.Play();
        Destroy(go, clip.length + 0.2f);
    }

    // som 2D que toca mesmo com o jogo pausado (prévia do volume nas opções, golpe na tela)
    public static void TocarInterface(string nome, float volume = 1f)
    {
        AudioClip clip = Carregar(nome);
        if (clip == null) return;

        GameObject go = new GameObject("Som_" + nome);
        AudioSource s = go.AddComponent<AudioSource>();
        s.spatialBlend = 0f;
        s.ignoreListenerPause = true;
        s.volume = volume * ConfigAudio.Efeitos;
        s.PlayOneShot(clip);
        Destroy(go, clip.length + 0.2f);
    }

    // ---- sons gerados por código ----

    // pancada grave e seca (golpe da criatura)
    static AudioClip Golpe()
    {
        if (clipGolpe != null) return clipGolpe;
        const int taxa = 44100;
        int n = (int)(taxa * 0.45f);
        float[] d = new float[n];
        System.Random r = new System.Random(7);
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)taxa;
            float grave = Mathf.Sin(2f * Mathf.PI * (55f + 90f * Mathf.Exp(-t * 18f)) * t) * Mathf.Exp(-t * 7f);
            float ruido = ((float)r.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 30f);
            d[i] = Mathf.Clamp(grave * 0.9f + ruido * 0.6f, -1f, 1f);
        }
        clipGolpe = AudioClip.Create("golpe", n, 1, taxa, false);
        clipGolpe.SetData(d, 0);
        return clipGolpe;
    }

    // "tum-tum" de coração, em loop (toca quando o jogador está com pouca vida)
    static AudioClip Batida()
    {
        if (clipBatida != null) return clipBatida;
        const int taxa = 44100;
        int n = (int)(taxa * 0.85f);
        float[] d = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)taxa;
            float a = Pulso(t, 0f) + 0.7f * Pulso(t, 0.22f);
            d[i] = a;
        }
        clipBatida = AudioClip.Create("batida", n, 1, taxa, false);
        clipBatida.SetData(d, 0);
        return clipBatida;
    }

    static float Pulso(float t, float inicio)
    {
        float x = t - inicio;
        if (x < 0f) return 0f;
        return Mathf.Sin(2f * Mathf.PI * 48f * x) * Mathf.Exp(-x * 22f);
    }

    void Start()
    {
        jogador = FindAnyObjectByType<PlayerMovement>();
        criatura = FindAnyObjectByType<EnemyAI>();
        if (criatura != null) agenteCriatura = criatura.GetComponent<NavMeshAgent>();

        // música de fundo (2D)
        musica = gameObject.AddComponent<AudioSource>();
        musica.spatialBlend = 0f;
        musica.volume = volumeMusica * ConfigAudio.Musica;
        musica.playOnAwake = false;
        faixa = Random.Range(0, musicas.Length);
        TocarProximaMusica();

        // zumbido de lâmpada (2D, em loop)
        AudioClip zumbido = Carregar("light_buzz");
        if (zumbido != null)
        {
            zumbidoFonte = gameObject.AddComponent<AudioSource>();
            zumbidoFonte.spatialBlend = 0f;
            zumbidoFonte.clip = zumbido;
            zumbidoFonte.loop = true;
            zumbidoFonte.volume = volumeZumbido * ConfigAudio.Efeitos;
            zumbidoFonte.Play();
        }

        // coração acelerado com pouca vida
        coracao = gameObject.AddComponent<AudioSource>();
        coracao.spatialBlend = 0f;
        coracao.clip = Batida();
        coracao.loop = true;
        coracao.volume = 0f;
        coracao.Play();

        // passos do jogador (tocados um a um, no ritmo da caminhada)
        somPasso = Carregar("player_footseps");
        if (jogador != null)
        {
            passosJogador = jogador.gameObject.AddComponent<AudioSource>();
            passosJogador.spatialBlend = 0f;
            passosJogador.playOnAwake = false;
        }

        // passos pesados da criatura (3D, em loop enquanto ela anda)
        AudioClip pesados = Carregar("heavy_footseps");
        if (criatura != null && pesados != null)
        {
            passosCriatura = criatura.gameObject.AddComponent<AudioSource>();
            Configurar3D(passosCriatura);
            // passos pesados se ouvem de longe: dá tempo de fugir
            passosCriatura.minDistance = 400f;
            passosCriatura.maxDistance = 5000f;
            passosCriatura.priority = 0;
            passosCriatura.clip = pesados;
            ultimaPosCriatura = criatura.transform.position;
            passosCriatura.loop = true;
            passosCriatura.volume = 0f;
            passosCriatura.Play();
        }

        proximoRuido = Time.time + Random.Range(20f, 40f);
    }

    void TocarProximaMusica()
    {
        AudioClip c = Carregar(musicas[faixa % musicas.Length]);
        faixa++;
        if (c == null) return;
        musica.clip = c;
        musica.Play();
    }

    static bool silenciado;

    // final do jogo: todo o som ambiente (música, passos, ruídos) some aos poucos
    public static void Silenciar()
    {
        silenciado = true;
    }

    void OnDestroy()
    {
        silenciado = false;
    }

    void Update()
    {
        if (silenciado)
        {
            foreach (AudioSource s in new[] { musica, zumbidoFonte, coracao, passosJogador, passosCriatura })
                if (s != null) s.volume = Mathf.MoveTowards(s.volume, 0f, Time.unscaledDeltaTime * 0.8f);
            return;
        }

        // acabou a faixa: toca a outra (não troca enquanto o jogo está pausado)
        if (musica.clip != null && !musica.isPlaying && !AudioListener.pause)
            TocarProximaMusica();

        // volumes seguem as opções
        musica.volume = volumeMusica * ConfigAudio.Musica;
        if (zumbidoFonte != null) zumbidoFonte.volume = volumeZumbido * ConfigAudio.Efeitos;

        bool poucaVida = jogador != null && jogador.Vida == 1;
        coracao.volume = Mathf.MoveTowards(coracao.volume, poucaVida ? 0.9f * ConfigAudio.Efeitos : 0f, Time.deltaTime);

        AtualizarPassosJogador();
        AtualizarPassosCriatura();

        if (Time.time >= proximoRuido)
        {
            proximoRuido = Time.time + Random.Range(30f, 60f);
            StartCoroutine(RuidoAleatorio());
        }
    }

    void AtualizarPassosJogador()
    {
        if (jogador == null || passosJogador == null || somPasso == null) return;
        if (!jogador.Andando || !jogador.NoChao || Time.timeScale == 0f) return;
        if (Time.time < proximoPasso) return;

        proximoPasso = Time.time + (jogador.Correndo ? 0.32f : 0.5f);
        passosJogador.pitch = Random.Range(0.9f, 1.1f);
        passosJogador.PlayOneShot(somPasso, volumePassos * (jogador.Correndo ? 1f : 0.7f) * ConfigAudio.Efeitos);
    }

    void AtualizarPassosCriatura()
    {
        if (passosCriatura == null || criatura == null || Time.deltaTime <= 0f) return;

        // velocidade medida pelo quanto ela andou de verdade (ignora teletransportes)
        Vector3 pos = criatura.transform.position;
        Vector3 delta = pos - ultimaPosCriatura; delta.y = 0f;
        ultimaPosCriatura = pos;
        float v = delta.magnitude / Time.deltaTime;
        if (v > 2000f) v = 0f;
        velocidadeCriatura = Mathf.Lerp(velocidadeCriatura, v, Time.deltaTime * 8f);

        float alvo = velocidadeCriatura > 15f ? volumeCriatura * ConfigAudio.Efeitos : 0f;
        passosCriatura.volume = Mathf.MoveTowards(passosCriatura.volume, alvo, Time.deltaTime * 3f);

        // o loop pode ter sido parado (pausa / troca de cena): garante que está tocando
        if (!passosCriatura.isPlaying && !AudioListener.pause) passosCriatura.Play();

        // correndo, os passos ficam mais rápidos
        float referencia = agenteCriatura != null ? Mathf.Max(1f, agenteCriatura.speed) : 150f;
        passosCriatura.pitch = Mathf.Clamp(0.8f + 0.5f * (velocidadeCriatura / referencia), 0.8f, 1.4f);
    }

    // um barulho estranho em algum lugar do prédio, longe o bastante para dar arrepio
    IEnumerator RuidoAleatorio()
    {
        if (jogador == null) yield break;

        AudioClip clip = Carregar(ruidos[Random.Range(0, ruidos.Length)]);
        if (clip == null) yield break;

        Vector3 pos = jogador.transform.position + Random.onUnitSphere * 800f;
        if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 800f, NavMesh.AllAreas)) pos = hit.position;

        GameObject go = new GameObject("Ruido_" + clip.name);
        go.transform.position = pos;
        AudioSource s = go.AddComponent<AudioSource>();
        Configurar3D(s);
        s.clip = clip;
        s.volume = 0f;

        // faixas longas: toca só um trecho de 6 a 12 s, com fade
        float duracao = Mathf.Min(clip.length, Random.Range(6f, 12f));
        if (clip.length > duracao) s.time = Random.Range(0f, clip.length - duracao);
        s.Play();

        float t = 0f;
        while (t < duracao && s != null)
        {
            t += Time.deltaTime;
            float fade = Mathf.Min(1f, Mathf.Min(t, duracao - t) / 1.5f);
            s.volume = 0.6f * fade * ConfigAudio.Efeitos;
            yield return null;
        }

        if (go != null) Destroy(go);
    }
}
