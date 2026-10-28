using UnityEngine;

// Soil moisture from 0 (bone dry) to 100 (soaked). It dries on its own; a valve adds water back.
public class SoilSensor : MonoBehaviour
{
    [Range(0f, 100f)] public float moisture = 60f;
    public float dryRate = 1.5f;        // moisture lost per second
    public float evaporation = 1f;      // 1 = normal; the weather can change it later
    public float variation = 0.5f;      // each plot dries between 1 - variation and 1 + variation times as fast

    public float dryness;               // this plot's own factor, picked at random when the scene starts

    void Awake()
    {
        dryness = Random.Range(1f - variation, 1f + variation);
    }

    void Update()
    {
        moisture -= dryRate * dryness * evaporation * Time.deltaTime;
        moisture = Mathf.Clamp(moisture, 0f, 100f);
    }

    public void AddWater(float amount)
    {
        moisture = Mathf.Clamp(moisture + amount, 0f, 100f);
    }
}
