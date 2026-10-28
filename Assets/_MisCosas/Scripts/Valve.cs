using UnityEngine;

// Opens when its sensor is dry and closes when it is wet. Open means water goes to the soil and leaves the tank.
public class Valve : MonoBehaviour
{
    public SoilSensor sensor;
    public WaterTank tank;

    public float openBelow = 30f;
    public float closeAbove = 70f;
    public float waterPerSecond = 6f;          // moisture added to the soil
    public float tankCostPerSecond = 1.5f;     // percent taken from the tank

    public Renderer indicator;                 // the model that changes colour
    public Color openColor = Color.green;
    public Color closedColor = Color.red;

    public bool isOpen;

    void Update()
    {
        if (sensor.moisture < openBelow) isOpen = true;
        else if (sensor.moisture > closeAbove) isOpen = false;

        if (indicator != null) indicator.material.color = isOpen ? openColor : closedColor;

        // An empty tank cannot deliver anything, so nothing flows.
        if (!isOpen || tank.level <= 0f) return;

        sensor.AddWater(waterPerSecond * Time.deltaTime);
        tank.Drain(tankCostPerSecond * Time.deltaTime);
    }
}
