using UnityEngine;
using UnityEngine.Rendering;

// Turns the sun around the farm and colours the light, the ambient light and the fog to match.
// The plots also dry faster in the middle of the day and slower at night.
public class DayNightCycle : MonoBehaviour
{
    public Light sun;
    public Light moon;                               // optional, one is created if it is left empty
    public Material skybox;                         // optional, so it can be set here and not in the Lighting window
    public float dayLength = 240f;                   // real seconds for a whole day
    [Range(0f, 24f)] public float hour = 9f;         // time of day when the game starts

    public Color noonColor = new Color(1f, 0.96f, 0.88f);
    public Color horizonColor = new Color(1f, 0.55f, 0.25f);
    public Color dayAmbient = new Color(0.55f, 0.62f, 0.72f);
    public Color nightAmbient = new Color(0.10f, 0.12f, 0.20f);
    public Color moonColor = new Color(0.55f, 0.65f, 1f);
    public float moonStrength = 0.25f;               // fraction of the sun's intensity
    public Color dayFog = new Color(0.72f, 0.80f, 0.90f);
    public Color nightFog = new Color(0.03f, 0.04f, 0.08f);
    public float fogDensity = 0.0012f;

    public float dayEvaporation = 1.5f;              // the plots dry faster at noon...
    public float nightEvaporation = 0.4f;            // ...and slower at night

    float maxIntensity;
    float yaw;
    SoilSensor[] sensors;

    public string TimeText => $"{(int)hour:00}:{(int)(hour % 1f * 60f):00}";

    void Awake()
    {
        maxIntensity = sun.intensity;
        yaw = sun.transform.eulerAngles.y;

        if (moon == null)
        {
            moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = sun.shadows;
            moon.color = moonColor;
        }

        RenderSettings.sun = sun;
        if (skybox != null) RenderSettings.skybox = skybox;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = fogDensity;
    }

    void Start()
    {
        sensors = FindObjectsByType<SoilSensor>(FindObjectsSortMode.None);
    }

    void Update()
    {
        hour = (hour + 24f / dayLength * Time.deltaTime) % 24f;

        // 6:00 the sun is on the horizon, 12:00 overhead, 18:00 on the horizon again.
        float angle = hour / 24f * 360f - 90f;
        sun.transform.rotation = Quaternion.Euler(angle, yaw, 0f);

        float height = Mathf.Sin(angle * Mathf.Deg2Rad);       // 1 at noon, 0 at sunrise and sunset, -1 at midnight
        float day = Mathf.Clamp01(height * 2f);

        sun.color = Color.Lerp(horizonColor, noonColor, day);
        sun.intensity = maxIntensity * day;

        // The moon is on the opposite side of the sky, so it is up while the sun is down.
        moon.transform.rotation = Quaternion.Euler(angle + 180f, yaw, 0f);
        moon.intensity = maxIntensity * moonStrength * Mathf.Clamp01(-height * 2f);

        float light = Mathf.Clamp01(height * 3f + 0.25f);
        RenderSettings.ambientLight = Color.Lerp(nightAmbient, dayAmbient, light);
        RenderSettings.fogColor = Color.Lerp(nightFog, dayFog, light);

        float evaporation = Mathf.Lerp(nightEvaporation, dayEvaporation, height * 0.5f + 0.5f);
        foreach (var s in sensors) s.evaporation = evaporation;
    }
}
