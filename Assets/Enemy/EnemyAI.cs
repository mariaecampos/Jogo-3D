using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    enum State { Idle, Patrol, Alert, Chase, Search }

    [Header("Referências")]
    public Transform player;              // objeto raiz do Player (o que tem o Character Controller)
    public Animator animator;
    public Transform[] patrolPoints;      // opcional; sem pontos, ele vaga por todo o NavMesh

    [Header("Escala do mundo")]
    [Tooltip("0 = automático (altura visível do modelo ÷ 1,8 m). Digite um número para forçar.")]
    public float distanceScale = 0f;
    public bool autoAgentSize = true;     // ajusta Radius/Height do Nav Mesh Agent ao tamanho do modelo
    public bool syncAnimSpeed = true;     // acerta a velocidade da animação com a velocidade real (menos "patinação")

    [Header("Velocidades (m/s, NÃO escalam sozinhas)")]
    public float walkSpeed = 1.6f;
    public float runSpeed = 5f;

    [Header("Visão (valores base, multiplicados pela escala)")]
    public float viewDistance = 14f;
    [Range(10f, 180f)] public float viewAngle = 110f;
    public float eyeHeight = 1.7f;

    [Header("Audição (valores base, multiplicados pela escala)")]
    public float hearDistance = 6f;
    public float hearSpeedThreshold = 4.5f;   // velocidade do jogador a partir da qual ele faz barulho (não escala)
    public float hearRate = 0.35f;            // quanto sobe a percepção por segundo

    [Header("Detecção")]
    public float detectTime = 3f;             // segundos base para notar o jogador
    public float alertThreshold = 0.3f;       // a partir daqui ele fica em alerta
    public float forgetRatePerSecond = 0.12f; // quão rápido a percepção cai quando ele não sente você (menor = esquece mais devagar)
    public float alertDuration = 3f;          // tempo máximo no Alert: depois disso, se ainda sente você, ele parte para a perseguição
    public float alertRiseMultiplier = 2f;    // durante o Alert a percepção sobe mais rápido enquanto ele te sente
    public float chaseMemoryTime = 8f;        // segundos que ele continua sabendo onde você está depois de te perder de vista
    public float searchTime = 12f;            // segundos procurando no último lugar onde te sentiu
    public float closeSenseDistance = 2f;     // base (x escala): muito perto, ele sente você mesmo sem ver
    public float closeSenseRate = 0.4f;       // quanto sobe a percepção por segundo nessa distância

    [Header("Teletransporte quando o jogador está longe (distâncias base x escala)")]
    public bool teleportWhenFar = true;
    public float farDistancePercent = 0.25f;   // "longe" = essa fração do tamanho do mapa (0,25 = 25%)
    public float farDistanceOverride = 0f;     // se > 0, usa esse valor em METROS no lugar da porcentagem
    public float farSeconds = 20f;             // quanto tempo seguido "longe" até ele se teletransportar
    public float teleportCooldown = 30f;       // intervalo mínimo entre dois teletransportes
    public float teleportMinDistance = 8f;     // ele reaparece entre essas duas distâncias do jogador
    public float teleportMaxDistance = 16f;
    public bool investigateAfterTeleport = true;   // depois de reaparecer, ele vai até o lugar onde você está

    [Header("Patrulha")]
    public bool randomAnywhere = true;        // sorteia destinos em QUALQUER lugar do NavMesh
    public float patrolRadius = 60f;          // base x escala; usado só se 'Random Anywhere' estiver desmarcado
    public float minPatrolDistance = 8f;      // base x escala; destinos mais perto que isso são ignorados
    public float wallMargin = 0.3f;           // base x escala; destinos e pontos de nascimento colados em parede são evitados

    [Header("Início da partida")]
    public bool randomStartPosition = true;        // a cada partida ele começa em um ponto seguro aleatório
    public float startMinDistancePercent = 0.25f;  // não nasce mais perto do jogador que essa fração do tamanho do mapa

    [Header("Captura (precisa encostar)")]
    public float touchPadding = 0.3f;         // folga em metros além do corpo dele e do jogador
    [Tooltip("Alcance do braço dele em metros (x escala), somado aos corpos dos dois")]
    public float alcanceDoGolpe = 0.6f;

    [Header("Ataque")]
    [Tooltip("Com provas: ele ataca, rouba provas e some. Sem provas: pega o jogador (fim de jogo).")]
    public bool atacarRoubaDocumentos = true;
    public int documentosRoubados = 2;          // quantas provas ele arranca por ataque
    public float tempoDoAtaque = 1.2f;          // segundos parado depois do golpe
    [Tooltip("Depois do golpe ele some e deixa o jogador em paz por esse tempo; depois volta a caçar")]
    public float tempoAfastadoAposAtaque = 25f;

    [Header("QR code (realidade aumentada)")]
    public float tempoAtordoado = 4f;           // parado, tonto, depois de ver o QR code
    public float tempoAfastadoAposQR = 15f;     // depois do atordoamento ele foge e só volta após esse tempo
    public float forcaDoEmpurrao = 700f;        // velocidade (na escala da cena) com que o jogador é jogado para trás
    public float subidaDoEmpurrao = 60f;
    public float intervaloDoGrito = 20f;        // grito ao começar a perseguir (no máximo 1 a cada X s)

    [Header("Debug")]
    public bool showDebugText = true;
    public bool enableDebugKeys = true;       // tecla T: traz o inimigo para perto do jogador (desmarque antes de apresentar)
    [Tooltip("Se o NavMesh estiver mais longe que isso, o inimigo não é encaixado automaticamente.")]
    public float maxSnapDistance = 100f;

    NavMeshAgent agent;
    CharacterController playerController;
    Camera cam;

    State state;
    float awareness;
    float waitTimer;
    float searchTimer;
    float farTimer;
    float nextTeleportTime;
    float nextTeleportTry;
    float nextChaseUpdate;
    float nextInitTry;
    float nextMeasure;
    float lastSensedTime = -999f;
    float alertTimer;
    Vector3 lastKnownPos;
    Vector3 patrolTarget;
    string currentAnim;
    bool caught;
    bool ready;

    // ataque
    float fimDoAtaque;
    float ignorarJogadorAte;
    bool atacou;
    bool fugirDepois;          // ao fim da pausa: some para longe
    bool voltarDepois;         // ao fim do descanso: reaparece perto do jogador e vai atrás dele
    int tentativasDeVolta;

    // usados pela realidade aumentada (QR code)
    public bool Pronto { get { return ready && !caught; } }
    public bool Afastado { get { return Time.time < ignorarJogadorAte; } }
    public bool Atordoado { get { return Time.time < fimDoAtaque; } }
    public float DistanciaAoJogador
    {
        get
        {
            if (player == null) return float.MaxValue;
            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = player.position; b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
    float proximoGrito;
    string status = "iniciando";
    bool patrolTargetFound;

    // anti-travamento
    float progressTimer;
    Vector3 progressPos;
    int stuckCount;

    // debug
    float dbgDist;
    bool dbgSeen;

    // medidas do modelo
    float modelHeight = 1.8f;
    float autoScale = 1f;

    // ---------- Escala ----------

    float S { get { return Mathf.Max(0.01f, distanceScale > 0f ? distanceScale : autoScale); } }
    float ViewDist { get { return viewDistance * S; } }
    float HearDist { get { return hearDistance * S; } }
    float CloseDist { get { return closeSenseDistance * S; } }
    // distância (em metros) a partir da qual o jogador conta como "longe"
    float FarDist
    {
        get
        {
            if (farDistanceOverride > 0f) return farDistanceOverride;
            EnsureNavCache();
            return mapSize > 0f ? mapSize * farDistancePercent : 20f * S;
        }
    }

    // distância (no chão) entre os centros dos dois para o golpe acertar:
    // corpo dele (raio do agente) + corpo do jogador (já na escala do mundo) + alcance do braço.
    // Antes não contava a escala do jogador nem o raio do agente, e o golpe nunca alcançava.
    float TouchReach
    {
        get
        {
            float raioJogador = 0.4f * S;
            if (playerController != null)
            {
                Vector3 e = playerController.transform.lossyScale;
                raioJogador = playerController.radius * Mathf.Max(Mathf.Abs(e.x), Mathf.Abs(e.z));
            }
            return agent.radius + raioJogador + (alcanceDoGolpe + touchPadding) * S;
        }
    }
    float EyeH { get { return eyeHeight * S; } }

    // mede a altura visível do modelo e deduz a escala do mundo
    void MeasureModel()
    {
        Renderer[] rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return;

        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);

        modelHeight = Mathf.Max(0.5f, b.size.y);
        autoScale = Mathf.Clamp(modelHeight / 1.8f, 1f, 100f);
    }

    void ApplyAgentSize()
    {
        agent.stoppingDistance = (state == State.Chase) ? 0f : 0.5f * S;
        if (!autoAgentSize) return;

        agent.height = modelHeight;
        agent.radius = modelHeight * 0.22f;
    }

    // velocidade + aceleração coerentes, para ele não derrapar nas curvas e raspar nas paredes
    void SetSpeed(float v)
    {
        agent.speed = v;
        agent.acceleration = Mathf.Max(8f, v * 4f);
        agent.angularSpeed = 480f;
    }

    // ---------- Início ----------

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        // quem move o inimigo é o NavMeshAgent, nunca a animação
        if (animator != null) animator.applyRootMotion = false;
    }

    void Start()
    {
        TryInit();
    }

    // prepara o inimigo; se algo faltar, guarda o motivo em "status" (aparece na tela) e tenta de novo depois
    bool TryInit()
    {
        cam = Camera.main;
        MeasureModel();
        ApplyAgentSize();

        // se o campo Player estiver vazio, procura pela tag Player ou pelo Character Controller
        if (player == null)
        {
            GameObject tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null) player = tagged.transform;
        }
        if (player == null)
        {
            CharacterController cc = FindObjectOfType<CharacterController>();
            if (cc != null) player = cc.transform;
        }
        if (player == null)
        {
            status = "campo Player vazio e nenhum Character Controller encontrado na cena";
            return false;
        }

        if (playerController == null)
        {
            playerController = player.GetComponentInParent<CharacterController>();
            if (playerController == null)
                playerController = player.GetComponentInChildren<CharacterController>();
        }

        if (!agent.enabled)
        {
            status = "o Nav Mesh Agent está desativado";
            return false;
        }

        // tenta encaixar o inimigo no NavMesh (usando o mesmo tipo de agente do Nav Mesh Agent)
        if (!agent.isOnNavMesh)
        {
            NavMeshQueryFilter filter = new NavMeshQueryFilter();
            filter.agentTypeID = agent.agentTypeID;
            filter.areaMask = NavMesh.AllAreas;

            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, maxSnapDistance, filter))
            {
                agent.Warp(hit.position);

                if (!agent.isOnNavMesh)
                {
                    agent.enabled = false;
                    transform.position = hit.position;
                    agent.enabled = true;
                }
            }
        }

        if (!agent.isOnNavMesh)
        {
            status = "o inimigo não está sobre o NavMesh | " + NavInfo();
            return false;
        }

        status = "OK";
        ready = true;

        // a cada partida ele começa em um ponto seguro aleatório, longe do jogador
        if (randomStartPosition && !SpawnAtRandomSafePoint())
            Debug.LogWarning("EnemyAI: não achei um ponto de início seguro; ele começa onde foi colocado.");

        PickPatrolTarget();
        StartPatrol();
        return true;
    }

    // dados para descobrir por que o inimigo não encaixa no NavMesh
    string NavInfo()
    {
        NavMeshTriangulation tri = NavMesh.CalculateTriangulation();

        string info = "vertices do NavMesh no jogo: " + tri.vertices.Length +
                      " | posicao: " + transform.position.ToString("F1") +
                      " | escala: " + transform.lossyScale.ToString("F2") +
                      " | agentType: " + agent.agentTypeID;

        NavMeshQueryFilter filter = new NavMeshQueryFilter();
        filter.agentTypeID = agent.agentTypeID;
        filter.areaMask = NavMesh.AllAreas;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit far, 500f, filter))
            info += " | NavMesh mais proximo a " + far.distance.ToString("F1") + " m, em " + far.position.ToString("F1");
        else
            info += " | nenhum NavMesh deste tipo em 500 m";

        if (player != null)
            info += " | jogador em: " + player.position.ToString("F1");

        return info;
    }

    // ---------- Loop principal ----------

    void Update()
    {
        if (!ready)
        {
            if (Time.time >= nextInitTry)
            {
                nextInitTry = Time.time + 1f;
                TryInit();
            }
            return;
        }

        if (caught) return;

        // logo depois do golpe: fica parado encarando o jogador e depois vai embora
        if (Time.time < fimDoAtaque)
        {
            FaceTowards(player.position);
            return;
        }
        if (atacou)
        {
            atacou = false;
            DepoisDoAtaque();
        }

        // acabou o descanso: ele volta a caçar
        if (voltarDepois && Time.time >= ignorarJogadorAte && Time.time >= nextTeleportTry)
        {
            nextTeleportTry = Time.time + 1f;
            tentativasDeVolta++;
            if (TeleportNearPlayer(teleportMinDistance * S, teleportMaxDistance * S, true) || tentativasDeVolta >= 8)
            {
                voltarDepois = false;
                // sem lugar escondido por perto: vai andando até onde o jogador está
                if (tentativasDeVolta >= 8)
                {
                    patrolTarget = player.position;
                    patrolTargetFound = true;
                    StartPatrol();
                }
            }
        }

        if (Time.time >= nextMeasure)
        {
            nextMeasure = Time.time + 2f;
            MeasureModel();
            ApplyAgentSize();
        }

        if (enableDebugKeys && Input.GetKeyDown(KeyCode.T))
        {
            if (!TeleportNearPlayer(ViewDist * 0.4f, ViewDist * 0.6f, false))
                Debug.LogWarning("EnemyAI: não achei um ponto seguro perto do jogador.");
        }

        bool seen = CanSeePlayer(out float dist);
        dbgDist = dist;
        dbgSeen = seen;
        UpdateAwareness(seen, dist);

        if (Time.time >= ignorarJogadorAte && TouchingPlayer()) { Caught(); return; }

        if (awareness >= 1f && state != State.Chase) EnterChase();

        switch (state)
        {
            case State.Idle:
                waitTimer -= Time.deltaTime;
                if (awareness >= alertThreshold) EnterAlert();
                else if (waitTimer <= 0f) { PickPatrolTarget(); StartPatrol(); }
                break;

            case State.Patrol:
                if (awareness >= alertThreshold) EnterAlert();
                else if (Arrived()) EnterIdle();
                else CheckProgress();
                break;

            case State.Alert:
                FaceTowards(lastKnownPos);
                alertTimer += Time.deltaTime;

                // acabou o tempo de alerta: se ainda sente o jogador, persegue; senão, procura por perto
                if (alertTimer >= alertDuration)
                {
                    if (Time.time - lastSensedTime < 1.5f) EnterChase();
                    else EnterSearch();
                }
                else if (awareness <= 0.05f) StartPatrol();
                break;

            case State.Chase:
                if (seen) awareness = 1f;

                // por alguns segundos depois de te perder de vista, ele ainda sabe onde você está
                bool remembers = Time.time - lastSensedTime < chaseMemoryTime;
                if (remembers) lastKnownPos = player.position;

                if (Time.time >= nextChaseUpdate)
                {
                    nextChaseUpdate = Time.time + 0.15f;
                    agent.SetDestination(lastKnownPos);
                }

                // só desiste depois que a memória acaba e ele chega ao último lugar onde te sentiu
                if (!remembers && Arrived()) EnterSearch();
                break;

            case State.Search:
                transform.Rotate(0f, 40f * Time.deltaTime, 0f);
                searchTimer -= Time.deltaTime;
                if (searchTimer <= 0f) { PickPatrolTarget(); StartPatrol(); }
                break;
        }

        SyncAnimationSpeed();
        CheckTeleportWhenFar();
    }

    // se ele não está avançando de verdade (raspando em parede/porta), sorteia outro destino
    void CheckProgress()
    {
        progressTimer += Time.deltaTime;
        if (progressTimer < 1f) return;

        Vector3 a = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 b = new Vector3(progressPos.x, 0f, progressPos.z);
        float moved = Vector3.Distance(a, b);

        progressTimer = 0f;
        progressPos = transform.position;

        if (moved < agent.speed * 0.25f) stuckCount++;
        else stuckCount = 0;

        if (stuckCount >= 2)
        {
            stuckCount = 0;
            PickPatrolTarget();
            StartPatrol();
        }
    }

    // a animação toca mais rápido ou mais devagar conforme a velocidade real (evita "patinar")
    void SyncAnimationSpeed()
    {
        if (!syncAnimSpeed || animator == null) return;

        if (state == State.Patrol || state == State.Chase)
        {
            float reference = (state == State.Chase ? 4.5f : 1.4f) * S;
            animator.speed = Mathf.Clamp(agent.velocity.magnitude / reference, 0.5f, 3f);
        }
        else
        {
            animator.speed = 1f;
        }
    }

    // contato de verdade: o corpo dele encosta no corpo do jogador
    bool TouchingPlayer()
    {
        Vector3 a = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 b = new Vector3(player.position.x, 0f, player.position.z);
        if (Vector3.Distance(a, b) > TouchReach) return false;

        // o jogador precisa estar na faixa de altura do corpo dele
        float dy = player.position.y - transform.position.y;
        return dy > -modelHeight * 0.5f && dy < modelHeight;
    }

    // ---------- Percepção ----------

    bool CanSeePlayer(out float dist)
    {
        Vector3 eye = transform.position + Vector3.up * EyeH;
        Vector3 target = player.position + Vector3.up * 1.4f;
        Vector3 dir = target - eye;
        dist = dir.magnitude;

        if (dist > ViewDist) return false;
        if (Vector3.Angle(transform.forward, dir) > viewAngle * 0.5f) return false;

        if (Physics.Raycast(eye, dir.normalized, out RaycastHit hit, dist + 0.5f,
                            ~0, QueryTriggerInteraction.Ignore))
            return hit.transform == player || hit.transform.IsChildOf(player);

        return false;
    }

    void UpdateAwareness(bool seen, float dist)
    {
        // depois de um ataque ele "esquece" o jogador por um tempo (dá chance de fugir)
        if (Time.time < ignorarJogadorAte)
        {
            awareness = 0f;
            return;
        }

        float rise = 0f;

        if (seen)
        {
            // quanto mais perto, mais rápido ele percebe
            float factor = Mathf.Lerp(2f, 0.4f, dist / ViewDist);
            rise += factor / detectTime;
        }

        Vector3 a = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 b = new Vector3(player.position.x, 0f, player.position.z);
        float playerSpeed = playerController != null ? playerController.velocity.magnitude : 0f;

        float flatDist = Vector3.Distance(a, b);

        if (flatDist < HearDist && playerSpeed > hearSpeedThreshold)
            rise += hearRate;

        // presença muito próxima é sentida mesmo sem ver
        if (flatDist < CloseDist)
            rise += closeSenseRate;

        if (state == State.Alert) rise *= alertRiseMultiplier;

        if (rise > 0f)
        {
            awareness += rise * Time.deltaTime;
            lastKnownPos = player.position;
            lastSensedTime = Time.time;
        }
        else
        {
            awareness -= forgetRatePerSecond * Time.deltaTime;
        }

        awareness = Mathf.Clamp01(awareness);
    }

    // ---------- Estados ----------

    void EnterIdle(float wait = -1f)
    {
        state = State.Idle;
        agent.isStopped = true;
        waitTimer = wait > 0f ? wait : Random.Range(2f, 4f);
        Play("Idle");
    }

    void StartPatrol()
    {
        if (!patrolTargetFound)
        {
            EnterIdle(0.5f);   // sem destino alcançável: tenta de novo em meio segundo
            return;
        }

        progressTimer = 0f;
        progressPos = transform.position;
        stuckCount = 0;

        state = State.Patrol;
        agent.isStopped = false;
        SetSpeed(walkSpeed);
        agent.stoppingDistance = 0.5f * S;
        agent.SetDestination(patrolTarget);
        Play("Walk");
    }

    void EnterAlert()
    {
        alertTimer = 0f;
        state = State.Alert;
        agent.isStopped = true;
        Play("Alert");
    }

    void EnterChase()
    {
        state = State.Chase;
        agent.isStopped = false;
        SetSpeed(runSpeed);
        agent.stoppingDistance = 0f;   // na perseguição ele vai até encostar
        agent.SetDestination(lastKnownPos);
        Play("Run");

        if (Time.time >= proximoGrito)
        {
            proximoGrito = Time.time + intervaloDoGrito;
            SomAbismal.Tocar("screams", transform.position + Vector3.up * modelHeight * 0.8f, 0.8f);
        }
    }

    void EnterSearch()
    {
        state = State.Search;
        agent.isStopped = true;
        searchTimer = searchTime;
        awareness = 0.5f;   // fica desconfiado: percebe você mais rápido
        Play("Alert");
    }

    void Caught()
    {
        // cada golpe tira 1 de vida (e rouba provas); o último golpe é fatal
        PlayerMovement pm = player.GetComponentInParent<PlayerMovement>();
        bool vivo = pm != null && pm.ReceberDano();
        if (vivo)
        {
            Atacar(pm);
            return;
        }

        caught = true;
        SomAbismal.TocarInterface("golpe", 1f);
        HudAbismal.MostrarAtaque(0, 0);
        SomAbismal.Tocar("screams", transform.position + Vector3.up * modelHeight * 0.8f, 1f);
        agent.isStopped = true;
        Play("Idle");
        Debug.Log("O jogador foi pego!");
        FimDeJogo.Derrota();
    }

    void Atacar(PlayerMovement pm)
    {
        GerenciadorDocumento docs = GerenciadorDocumento.Instance;
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        Play("Alert");

        Pausar(tempoDoAtaque, tempoAfastadoAposAtaque);

        int perdidos = 0;
        if (atacarRoubaDocumentos && docs != null)
            perdidos = docs.PerderDocumentos(documentosRoubados, player.position);

        pm.Empurrar(player.position - transform.position, forcaDoEmpurrao, subidaDoEmpurrao);

        SomAbismal.TocarInterface("golpe", 1f);
        SomAbismal.Tocar("screams", transform.position + Vector3.up * modelHeight * 0.8f, 1f);
        HudAbismal.MostrarAtaque(perdidos, pm.Vida);
    }

    // depois do golpe ele some (se o jogador não estiver olhando) ou vai embora andando
    void DepoisDoAtaque()
    {
        agent.isStopped = false;

        // some para um ponto aleatório longe do jogador e fica por lá até o descanso acabar
        if (fugirDepois) SpawnAtRandomSafePoint();
        fugirDepois = false;

        PickPatrolTarget();
        StartPatrol();
    }

    // para no lugar por 'parado' segundos, depois foge e ignora o jogador por 'afastado' segundos
    void Pausar(float parado, float afastado)
    {
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        Play("Alert");

        atacou = true;
        fugirDepois = true;
        voltarDepois = true;
        tentativasDeVolta = 0;
        fimDoAtaque = Time.time + parado;
        ignorarJogadorAte = fimDoAtaque + afastado;
        awareness = 0f;
        state = State.Idle;
    }

    // o jogador mostrou o QR code: ele fica tonto, foge e só volta depois de um tempo
    public void Atordoar()
    {
        if (!Pronto) return;
        Pausar(tempoAtordoado, tempoAfastadoAposQR);
        SomAbismal.Tocar("screams", transform.position + Vector3.up * modelHeight * 0.8f, 1f);
    }

    // ---------- Patrulha ----------

    void PickPatrolTarget()
    {
        patrolTarget = transform.position;   // padrão seguro, caso nada seja encontrado
        patrolTargetFound = false;

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            patrolTarget = patrolPoints[Random.Range(0, patrolPoints.Length)].position;
            patrolTargetFound = true;
            return;
        }

        NavMeshPath path = new NavMeshPath();

        // 3 passadas: regras completas, depois sem folga de parede, depois com distância mínima menor.
        // Assim ele quase sempre acha um destino e não fica parado.
        for (int pass = 0; pass < 3; pass++)
        {
            float minD = minPatrolDistance * S * (pass == 2 ? 0.25f : 1f);
            bool useWallMargin = (pass == 0);

            for (int i = 0; i < 15; i++)
            {
                Vector3 p;
                if (randomAnywhere && EnsureNavCache())
                {
                    p = RandomNavMeshPoint();
                }
                else
                {
                    Vector2 c = Random.insideUnitCircle * (patrolRadius * S);
                    p = transform.position + new Vector3(c.x, 0f, c.y);
                }

                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 8f * S, NavMesh.AllAreas)) continue;
                if (Vector3.Distance(hit.position, transform.position) < minD) continue;

                // evita destinos colados em parede (ele ficaria raspando nela)
                if (useWallMargin &&
                    NavMesh.FindClosestEdge(hit.position, out NavMeshHit edge, NavMesh.AllAreas) &&
                    edge.distance < wallMargin * S) continue;

                // só aceita pontos que o agente realmente consegue alcançar
                if (agent.CalculatePath(hit.position, path) && path.status == NavMeshPathStatus.PathComplete)
                {
                    patrolTarget = hit.position;
                    patrolTargetFound = true;
                    return;
                }
            }
        }
    }

    // ---- sorteio uniforme de um ponto em todo o NavMesh ----

    NavMeshTriangulation navTri;
    float[] triCumArea;
    bool navCacheBuilt;
    float mapSize;   // diagonal horizontal do NavMesh, em metros

    bool EnsureNavCache()
    {
        if (navCacheBuilt) return triCumArea != null && triCumArea.Length > 0;
        navCacheBuilt = true;

        navTri = NavMesh.CalculateTriangulation();

        Vector3[] vs = navTri.vertices;
        if (vs.Length > 0)
        {
            float minX = vs[0].x, maxX = vs[0].x, minZ = vs[0].z, maxZ = vs[0].z;
            for (int k = 1; k < vs.Length; k++)
            {
                if (vs[k].x < minX) minX = vs[k].x;
                if (vs[k].x > maxX) maxX = vs[k].x;
                if (vs[k].z < minZ) minZ = vs[k].z;
                if (vs[k].z > maxZ) maxZ = vs[k].z;
            }
            float dx = maxX - minX, dz = maxZ - minZ;
            mapSize = Mathf.Sqrt(dx * dx + dz * dz);
        }

        int triCount = navTri.indices.Length / 3;
        if (triCount == 0) return false;

        triCumArea = new float[triCount];
        float total = 0f;
        for (int t = 0; t < triCount; t++)
        {
            Vector3 a = navTri.vertices[navTri.indices[t * 3]];
            Vector3 b = navTri.vertices[navTri.indices[t * 3 + 1]];
            Vector3 c = navTri.vertices[navTri.indices[t * 3 + 2]];
            total += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            triCumArea[t] = total;
        }
        return true;
    }

    Vector3 RandomNavMeshPoint()
    {
        float r = Random.value * triCumArea[triCumArea.Length - 1];

        int lo = 0, hi = triCumArea.Length - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (triCumArea[mid] < r) lo = mid + 1;
            else hi = mid;
        }

        Vector3 a = navTri.vertices[navTri.indices[lo * 3]];
        Vector3 b = navTri.vertices[navTri.indices[lo * 3 + 1]];
        Vector3 c = navTri.vertices[navTri.indices[lo * 3 + 2]];

        float u = Random.value, v = Random.value;
        if (u + v > 1f) { u = 1f - u; v = 1f - v; }
        return a + u * (b - a) + v * (c - a);
    }

    bool Arrived()
    {
        return !agent.pathPending &&
               agent.remainingDistance <= agent.stoppingDistance + 0.4f * S;
    }

    // ---------- Teletransporte quando o jogador está longe ----------

    void CheckTeleportWhenFar()
    {
        if (!teleportWhenFar) return;

        // descansando depois de um ataque / QR code: não se aproxima
        if (Afastado || voltarDepois) { farTimer = 0f; return; }

        // em perseguição ou alerta ele nunca some
        if (state == State.Chase || state == State.Alert)
        {
            farTimer = 0f;
            return;
        }

        Vector3 a = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 b = new Vector3(player.position.x, 0f, player.position.z);

        if (Vector3.Distance(a, b) > FarDist) farTimer += Time.deltaTime;
        else farTimer = 0f;

        if (farTimer < farSeconds) return;
        if (Time.time < nextTeleportTime) return;
        if (Time.time < nextTeleportTry) return;
        nextTeleportTry = Time.time + 1f;

        // nunca desaparece na frente do jogador
        if (IsVisibleToPlayer(transform.position + Vector3.up * modelHeight * 0.5f)) return;

        if (TeleportNearPlayer(teleportMinDistance * S, teleportMaxDistance * S, true))
        {
            farTimer = 0f;
            nextTeleportTime = Time.time + teleportCooldown;
        }
    }

    // procura um ponto SEGURO perto do jogador e leva o inimigo para lá
    bool TeleportNearPlayer(float minDist, float maxDist, bool requireHidden)
    {
        // onde o jogador está no NavMesh
        if (!NavMesh.SamplePosition(player.position, out NavMeshHit playerNav, 4f * S, NavMesh.AllAreas))
            return false;

        NavMeshPath path = new NavMeshPath();

        for (int i = 0; i < 40; i++)
        {
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(minDist, maxDist);
            Vector3 p = player.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;

            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 3f * S, NavMesh.AllAreas)) continue;
            Vector3 pos = hit.position;

            // 1) mesmo nível do jogador (descarta cima de móveis, mezaninos, etc.)
            if (Mathf.Abs(pos.y - playerNav.position.y) > 0.5f * S) continue;

            // 2) e 3) folga das paredes e espaço livre para o corpo dele (sem objetos dentro)
            if (!IsSafeSpot(pos, true)) continue;

            // 4) o jogador consegue chegar a esse ponto a pé (descarta ilhas isoladas) e o caminho não é longo demais
            if (!NavMesh.CalculatePath(pos, playerNav.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete) continue;
            if (PathLength(path) > maxDist * 2.5f) continue;

            // 5) fora da visão do jogador
            if (requireHidden)
            {
                if (IsVisibleToPlayer(pos + Vector3.up * modelHeight * 0.5f)) continue;
                if (IsVisibleToPlayer(pos + Vector3.up * modelHeight * 0.95f)) continue;
            }

            agent.Warp(pos);
            awareness = 0f;

            if (investigateAfterTeleport)
            {
                patrolTarget = playerNav.position;   // vai até onde você está
                patrolTargetFound = true;
            }
            else
            {
                PickPatrolTarget();
            }

            StartPatrol();
            return true;
        }

        return false;
    }

    // ponto seguro: com folga das paredes e sem nenhum objeto dentro do volume do corpo dele
    bool IsSafeSpot(Vector3 pos, bool checkWallMargin)
    {
        float bodyR = modelHeight * 0.15f;
        float lift = Mathf.Max(0.2f, modelHeight * 0.05f);   // tira o fundo da cápsula do chão

        if (checkWallMargin &&
            NavMesh.FindClosestEdge(pos, out NavMeshHit edge, NavMesh.AllAreas) &&
            edge.distance < wallMargin * S) return false;

        Vector3 bottom = pos + Vector3.up * (bodyR + lift);
        Vector3 top = pos + Vector3.up * Mathf.Max(bodyR + lift, modelHeight - bodyR);
        return !Physics.CheckCapsule(bottom, top, bodyR, ~0, QueryTriggerInteraction.Ignore);
    }

    // início da partida: leva o inimigo para um ponto seguro aleatório, longe do jogador
    bool SpawnAtRandomSafePoint()
    {
        if (!EnsureNavCache()) return false;

        Vector3 playerNavPos = player.position;
        if (NavMesh.SamplePosition(player.position, out NavMeshHit pn, 4f * S, NavMesh.AllAreas))
            playerNavPos = pn.position;

        NavMeshPath path = new NavMeshPath();
        float minDist = mapSize * startMinDistancePercent;

        // 3 passadas: regras completas, depois menos exigente (distância e folga de parede)
        for (int pass = 0; pass < 3; pass++)
        {
            float needed = minDist * (pass == 0 ? 1f : (pass == 1 ? 0.6f : 0.3f));
            bool useWallMargin = pass < 2;

            for (int i = 0; i < 40; i++)
            {
                Vector3 p = RandomNavMeshPoint();
                if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 3f * S, NavMesh.AllAreas)) continue;
                Vector3 pos = hit.position;

                // longe do jogador
                Vector3 d = pos - player.position;
                d.y = 0f;
                if (d.magnitude < needed) continue;

                // sem parede nem objeto no corpo dele
                if (!IsSafeSpot(pos, useWallMargin)) continue;

                // o jogador consegue chegar até lá a pé (descarta ilhas isoladas)
                if (!NavMesh.CalculatePath(pos, playerNavPos, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete) continue;

                agent.Warp(pos);
                return true;
            }
        }

        return false;
    }

    float PathLength(NavMeshPath path)
    {
        Vector3[] c = path.corners;
        float len = 0f;
        for (int k = 1; k < c.Length; k++) len += Vector3.Distance(c[k - 1], c[k]);
        return len;
    }

    // true se o ponto está na tela do jogador e sem parede no caminho
    bool IsVisibleToPlayer(Vector3 point)
    {
        if (cam == null) return false;

        Vector3 vp = cam.WorldToViewportPoint(point);
        if (vp.z < 0f || vp.x < -0.1f || vp.x > 1.1f || vp.y < -0.1f || vp.y > 1.1f)
            return false;

        bool blocked = Physics.Linecast(cam.transform.position, point,
                                        ~0, QueryTriggerInteraction.Ignore);
        return !blocked;
    }

    // ---------- Utilidades ----------

    void FaceTowards(Vector3 pos)
    {
        Vector3 dir = pos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) return;
        Quaternion rot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, rot, 3f * Time.deltaTime);
    }

    void Play(string anim, float fade = 0.25f)
    {
        if (animator == null || currentAnim == anim) return;
        currentAnim = anim;

        if (!animator.HasState(0, Animator.StringToHash(anim)))
        {
            Debug.LogWarning("EnemyAI: o Animator não tem um estado chamado '" + anim + "'. Confira o nome no Animator Controller.");
            return;
        }

        animator.CrossFadeInFixedTime(anim, fade);
    }

    // mostra o estado atual na tela Game para facilitar os testes
    void OnGUI()
    {
        if (!showDebugText) return;

        string msg;
        if (ready)
        {
            msg = "Estado: " + state + " | Percepção: " + awareness.ToString("F2") +
                  " | Longe (acima de " + FarDist.ToString("F0") + " m) há: " + farTimer.ToString("F0") + "/" + farSeconds.ToString("F0") + " s" +
                  "\nDist. jogador: " + dbgDist.ToString("F1") + " m | Visto: " + (dbgSeen ? "sim" : "nao") +
                  " | Alcance: visão " + ViewDist.ToString("F0") + " m, audição " + HearDist.ToString("F0") +
                  " m, presença " + CloseDist.ToString("F0") + " m, contato " + TouchReach.ToString("F1") + " m" +
                  "\nVelocidade: " + agent.velocity.magnitude.ToString("F2") +
                  " (speed do agente: " + agent.speed.ToString("F1") + ", parado: " + agent.isStopped +
                  ", timeScale: " + Time.timeScale.ToString("F1") + ")" +
                  " | Caminho: " + agent.pathStatus +
                  " | Falta: " + agent.remainingDistance.ToString("F1") + " m" +
                  " | Destino alcançável: " + (patrolTargetFound ? "sim" : "NAO") +
                  "\nEscala do mundo: " + S.ToString("F1") + (distanceScale > 0f ? " (manual)" : " (automática)") +
                  " | Altura do modelo: " + modelHeight.ToString("F1") + " m" +
                  " | Agent Radius sugerido p/ o NavMesh: " + (modelHeight * 0.22f).ToString("F1");
        }
        else
        {
            msg = "INIMIGO PARADO: " + status;
        }

        GUI.Label(new Rect(10, 10, Screen.width - 20, 160), "[" + gameObject.name + "] " + msg);
    }

    void OnDrawGizmosSelected()
    {
        float scale = Application.isPlaying ? S : Mathf.Max(1f, distanceScale);
        Vector3 eye = transform.position + Vector3.up * eyeHeight * scale;

        Gizmos.color = Color.yellow;
        Vector3 l = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 r = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawLine(eye, eye + l * viewDistance * scale);
        Gizmos.DrawLine(eye, eye + r * viewDistance * scale);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hearDistance * scale);
    }
}