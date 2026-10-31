using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Lanterna do jogador. F liga / desliga a qualquer momento.
// A luz de verdade é criada presa à câmera do jogador (aponta sempre para onde ele olha),
// então não depende da posição/rotação do modelo 3D da lanterna.
public class Flashlight : MonoBehaviour
{
    [Header("Controle")]
    public KeyCode toggleKey = KeyCode.F;
    public bool startOn = true;

    [Header("Luz (o mapa é ~100x maior que o tamanho real)")]
    public float intensidade = 300000f;
    public float alcance = 3000f;
    [Range(10f, 120f)] public float angulo = 55f;
    public Color cor = new Color(1f, 0.95f, 0.82f);
    public bool sombras = true;

    [Header("Bateria (desligada = lanterna infinita)")]
    public bool useBattery = false;
    public float maxBattery = 120f;      // segundos de luz
    public float lowBatteryPercent = 0.2f;

    [Header("Som (opcional)")]
    public AudioSource clickSound;

    Light luz;
    float battery;
    bool isOn;

    // usados pela HUD e pelo save
    public bool Ligada { get { return isOn; } }
    public bool TemBateria { get { return battery > 0f || !useBattery; } }
    public float Bateria
    {
        get { return battery; }
        set { battery = Mathf.Clamp(value, 0f, maxBattery); }
    }

    // garante uma lanterna ativa em toda cena que tiver jogador
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AoAbrirOJogo()
    {
        Garantir();
        SceneManager.sceneLoaded += (cena, modo) => Garantir();
    }

    static void Garantir()
    {
        PlayerMovement jogador = FindAnyObjectByType<PlayerMovement>();
        if (jogador == null) return;

        // a da cena pode estar com o objeto desligado
        Flashlight existente = null;
        foreach (Flashlight f in FindObjectsByType<Flashlight>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!f.gameObject.scene.IsValid()) continue;
            if (!f.gameObject.activeSelf) f.gameObject.SetActive(true);
            existente = f;
        }

        if (existente == null)
        {
            Camera cam = jogador.GetComponentInChildren<Camera>();
            if (cam == null) cam = Camera.main;
            if (cam != null) cam.gameObject.AddComponent<Flashlight>();
        }
    }

    void Awake()
    {
        battery = maxBattery;

        Camera cam = GetComponentInParent<Camera>();
        if (cam == null)
        {
            PlayerMovement jogador = FindAnyObjectByType<PlayerMovement>();
            if (jogador != null) cam = jogador.GetComponentInChildren<Camera>();
        }
        if (cam == null) cam = Camera.main;

        // a luz antiga deste objeto (presa ao modelo) fica desligada
        Light antiga = GetComponent<Light>();
        if (antiga != null) antiga.enabled = false;

        if (cam != null)
        {
            // o corpo / modelo do jogador não pode fazer sombra, senão tampa a lanterna
            foreach (Renderer r in cam.transform.root.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = ShadowCastingMode.Off;

            GameObject go = new GameObject("LuzDaLanterna");
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            luz = go.AddComponent<Light>();
        }
        else
        {
            luz = antiga != null ? antiga : gameObject.AddComponent<Light>();
        }

        luz.type = LightType.Spot;
        luz.color = cor;
        luz.intensity = intensidade;
        luz.range = alcance;
        luz.spotAngle = angulo;
        luz.innerSpotAngle = angulo * 0.55f;
        luz.shadows = sombras ? LightShadows.Soft : LightShadows.None;
        luz.shadowNearPlane = 1f;
        luz.renderMode = LightRenderMode.ForcePixel;   // nunca é descartada pelo limite de luzes

        Definir(startOn);
    }

    void Update()
    {
        // jogo pausado: F não faz nada
        if (Time.timeScale > 0f && Input.GetKeyDown(toggleKey))
            Alternar();

        if (!isOn || !useBattery) return;

        battery -= Time.deltaTime;

        if (battery <= 0f)
        {
            battery = 0f;
            Definir(false);
            return;
        }

        // luz tremendo quando a bateria está fraca
        if (battery / maxBattery <= lowBatteryPercent)
            luz.intensity = intensidade * Random.Range(0.3f, 1f);
        else
            luz.intensity = intensidade;
    }

    public void Alternar()
    {
        if (!isOn && !TemBateria) return;
        Definir(!isOn);
        if (clickSound != null) clickSound.Play();
    }

    public void Definir(bool ligada)
    {
        isOn = ligada && TemBateria;
        if (luz != null) luz.enabled = isOn;
    }

    // para recarregar com pilhas depois
    public void AddBattery(float seconds)
    {
        battery = Mathf.Min(maxBattery, battery + seconds);
    }
}
