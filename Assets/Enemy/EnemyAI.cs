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
    public Transform[] patrolPoints;      // opcional; sem pontos, ele vaga aleatoriamente

    [Header("Velocidades")]
    public float walkSpeed = 1.6f;
    public float runSpeed = 5f;

    [Header("Visão")]
    public float viewDistance = 14f;
    [Range(10f, 180f)] public float viewAngle = 110f;
    public float eyeHeight = 1.7f;

    [Header("Audição")]
    public float hearDistance = 6f;
    public float hearSpeedThreshold = 4.5f;   // jogador correndo faz barulho
    public float hearRate = 0.35f;            // quanto sobe a percepção por segundo

    [Header("Detecção")]
    public float detectTime = 3f;             // segundos base para notar o jogador
    public float alertThreshold = 0.3f;       // a partir daqui ele fica em alerta
    public float decayPerSecond = 0.25f;      // velocidade com que "esquece"
    public float searchDuration = 6f;

    [Header("Spawn perto do jogador")]
    public bool enableSpawn = true;
    public float maxDistanceFromPlayer = 40f;
    public float spawnMinDistance = 20f;
    public float spawnMaxDistance = 30f;
    public float spawnCooldown = 15f;

    [Header("Captura")]
    public float catchDistance = 1.4f;

    [Header("Debug")]
    public bool showDebugText = true;

    NavMeshAgent agent;
    CharacterController playerController;
    Camera cam;

    State state;
    float awareness;
    float waitTimer;
    float searchTimer;
    float nextSpawnTime;
    float nextSpawnCheck;
    float nextChaseUpdate;
    Vector3 lastKnownPos;
    Vector3 patrolTarget;
    string currentAnim;
    bool caught;
    bool ready;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Start()
    {
        cam = Camera.main;

        if (player == null)
        {
            Debug.LogError("EnemyAI: o campo Player está vazio. Arraste o objeto Player para ele.");
            return;
        }

        playerController = player.GetComponentInParent<CharacterController>();
        if (playerController == null)
            playerController = player.GetComponentInChildren<CharacterController>();
        if (playerController == null)
            Debug.LogError("EnemyAI: o campo Player precisa apontar para o objeto com o Character Controller.");

        // tenta encaixar o inimigo no NavMesh se ele estiver um pouco fora
        if (!agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
                agent.Warp(hit.position);
        }

        if (!agent.isOnNavMesh)
        {
            Debug.LogError("EnemyAI: o inimigo não está sobre o NavMesh. Faça o Bake e posicione-o na área azul.");
            return;
        }

        ready = true;
        PickPatrolTarget();
        StartPatrol();
    }

    void Update()
    {
        if (!ready || caught) return;

        bool seen = CanSeePlayer(out float dist);
        UpdateAwareness(seen, dist);

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
                break;

            case State.Alert:
                FaceTowards(lastKnownPos);
                if (awareness <= 0.05f) StartPatrol();
                break;

            case State.Chase:
                if (seen)
                {
                    awareness = 1f;
                    lastKnownPos = player.position;
                }
                if (Time.time >= nextChaseUpdate)
                {
                    nextChaseUpdate = Time.time + 0.15f;
                    agent.SetDestination(lastKnownPos);
                }
                if (!seen && Arrived()) EnterSearch();

                if (Vector3.Distance(transform.position, player.position) < catchDistance)
                    Caught();
                break;

            case State.Search:
                transform.Rotate(0f, 40f * Time.deltaTime, 0f);
                searchTimer -= Time.deltaTime;
                if (searchTimer <= 0f) { PickPatrolTarget(); StartPatrol(); }
                break;
        }

        CheckSpawn();
    }

    // ---------- Percepção ----------

    bool CanSeePlayer(out float dist)
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Vector3 target = player.position + Vector3.up * 1.4f;
        Vector3 dir = target - eye;
        dist = dir.magnitude;

        if (dist > viewDistance) return false;
        if (Vector3.Angle(transform.forward, dir) > viewAngle * 0.5f) return false;

        if (Physics.Raycast(eye, dir.normalized, out RaycastHit hit, dist + 0.5f,
                            ~0, QueryTriggerInteraction.Ignore))
            return hit.transform == player || hit.transform.IsChildOf(player);

        return false;
    }

    void UpdateAwareness(bool seen, float dist)
    {
        float rise = 0f;

        if (seen)
        {
            // quanto mais perto, mais rápido ele percebe
            float factor = Mathf.Lerp(2f, 0.4f, dist / viewDistance);
            rise += factor / detectTime;
        }

        Vector3 a = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 b = new Vector3(player.position.x, 0f, player.position.z);
        float playerSpeed = playerController != null ? playerController.velocity.magnitude : 0f;

        if (Vector3.Distance(a, b) < hearDistance && playerSpeed > hearSpeedThreshold)
            rise += hearRate;

        if (rise > 0f)
        {
            awareness += rise * Time.deltaTime;
            lastKnownPos = player.position;
        }
        else
        {
            awareness -= decayPerSecond * Time.deltaTime;
        }

        awareness = Mathf.Clamp01(awareness);
    }

    // ---------- Estados ----------

    void EnterIdle()
    {
        state = State.Idle;
        agent.isStopped = true;
        waitTimer = Random.Range(2f, 4f);
        Play("Idle");
    }

    void StartPatrol()
    {
        state = State.Patrol;
        agent.isStopped = false;
        agent.speed = walkSpeed;
        agent.SetDestination(patrolTarget);
        Play("Walk");
    }

    void EnterAlert()
    {
        state = State.Alert;
        agent.isStopped = true;
        Play("Alert");
    }

    void EnterChase()
    {
        state = State.Chase;
        agent.isStopped = false;
        agent.speed = runSpeed;
        agent.SetDestination(lastKnownPos);
        Play("Run");
    }

    void EnterSearch()
    {
        state = State.Search;
        agent.isStopped = true;
        searchTimer = searchDuration;
        awareness = 0.5f;   // fica desconfiado: percebe você mais rápido
        Play("Alert");
    }

    void Caught()
    {
        caught = true;
        agent.isStopped = true;
        Play("Idle");
        Debug.Log("O jogador foi pego!");
        Invoke(nameof(Restart), 1.5f);
    }

    void Restart()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // ---------- Patrulha ----------

    void PickPatrolTarget()
    {
        patrolTarget = transform.position;   // padrão seguro, caso nada seja encontrado

        if (patrolPoints != null && patrolPoints.Length > 0)
        {
            patrolTarget = patrolPoints[Random.Range(0, patrolPoints.Length)].position;
        }
        else if (RandomNavPoint(transform.position, 25f, out Vector3 p))
        {
            patrolTarget = p;
        }
    }

    bool RandomNavPoint(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 20; i++)
        {
            Vector2 c = Random.insideUnitCircle * radius;
            Vector3 p = center + new Vector3(c.x, 0f, c.y);
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    bool Arrived()
    {
        return !agent.pathPending &&
               agent.remainingDistance <= agent.stoppingDistance + 0.4f;
    }

    // ---------- Spawn perto do jogador ----------

    void CheckSpawn()
    {
        if (!enableSpawn) return;
        if (Time.time < nextSpawnCheck) return;
        nextSpawnCheck = Time.time + 1f;

        if (state == State.Chase || state == State.Alert) return;
        if (Time.time < nextSpawnTime) return;
        if (Vector3.Distance(transform.position, player.position) < maxDistanceFromPlayer) return;
        if (IsVisibleToPlayer(transform.position + Vector3.up * 1.5f)) return;

        for (int i = 0; i < 20; i++)
        {
            float angle = Random.Range(0f, 360f);
            float radius = Random.Range(spawnMinDistance, spawnMaxDistance);
            Vector3 p = player.position + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * radius;

            if (!NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas)) continue;
            if (IsVisibleToPlayer(hit.position + Vector3.up * 1.5f)) continue;

            agent.Warp(hit.position);
            nextSpawnTime = Time.time + spawnCooldown;
            awareness = 0f;
            PickPatrolTarget();
            StartPatrol();
            return;
        }
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
        animator.CrossFadeInFixedTime(anim, fade);
    }

    // mostra o estado atual na tela Game para facilitar os testes
    void OnGUI()
    {
        if (!showDebugText) return;
        GUI.Label(new Rect(10, 10, 600, 25),
            "Estado: " + state + " | Percepção: " + awareness.ToString("F2"));
    }

    void OnDrawGizmosSelected()
    {
        Vector3 eye = transform.position + Vector3.up * eyeHeight;
        Gizmos.color = Color.yellow;
        Vector3 l = Quaternion.Euler(0, -viewAngle * 0.5f, 0) * transform.forward;
        Vector3 r = Quaternion.Euler(0, viewAngle * 0.5f, 0) * transform.forward;
        Gizmos.DrawLine(eye, eye + l * viewDistance);
        Gizmos.DrawLine(eye, eye + r * viewDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, hearDistance);
    }
}