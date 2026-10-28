using UnityEngine;

// The tank is fed from the lake by gravity. Open valves drain it; a slow refill keeps the demo from stopping.
public class WaterTank : MonoBehaviour
{
    [Range(0f, 100f)] public float level = 100f;     // percent full
    public float refillRate = 0.5f;                  // percent per second, 0 to turn it off
    public float lowLevel = 20f;

    public bool Low => level < lowLevel;

    void Update()
    {
        level = Mathf.Clamp(level + refillRate * Time.deltaTime, 0f, 100f);
    }

    public void Drain(float amount)
    {
        level = Mathf.Clamp(level - amount, 0f, 100f);
    }
}
