using UnityEngine;

[RequireComponent(typeof(Light))]
public class Flashlight : MonoBehaviour
{
    [Header("Controle")]
    public KeyCode toggleKey = KeyCode.F;
    public bool startOn = true;

    [Header("Bateria")]
    public bool useBattery = true;
    public float maxBattery = 120f;      // segundos de luz
    public float lowBatteryPercent = 0.2f;

    [Header("Som (opcional)")]
    public AudioSource clickSound;

    Light lightComp;
    float battery;
    float baseIntensity;
    bool isOn;

    void Awake()
    {
        lightComp = GetComponent<Light>();
        baseIntensity = lightComp.intensity;
        battery = maxBattery;
        isOn = startOn;
        lightComp.enabled = isOn;
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey) && (battery > 0f || !useBattery))
        {
            isOn = !isOn;
            lightComp.enabled = isOn;
            if (clickSound != null) clickSound.Play();
        }

        if (!isOn) return;

        if (useBattery)
        {
            battery -= Time.deltaTime;

            if (battery <= 0f)
            {
                battery = 0f;
                isOn = false;
                lightComp.enabled = false;
                return;
            }

            // luz tremendo quando a bateria está fraca
            if (battery / maxBattery <= lowBatteryPercent)
                lightComp.intensity = baseIntensity * Random.Range(0.3f, 1f);
            else
                lightComp.intensity = baseIntensity;
        }
    }

    // para recarregar com pilhas depois
    public void AddBattery(float seconds)
    {
        battery = Mathf.Min(maxBattery, battery + seconds);
    }
}