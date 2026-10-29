using UnityEngine;

// Opens when its sensor is dry and closes when it is wet. Open means water goes to the soil and leaves the tank.
// While water flows it can break. A broken valve lets nothing through until a worker repairs it.
public class Valve : MonoBehaviour
{
    public SoilSensor sensor;
    public WaterTank tank;

    public float openBelow = 30f;
    public float closeAbove = 70f;
    public float waterPerSecond = 6f;          // moisture added to the soil, at normal flow
    public float tankCostPerSecond = 0.5f;     // percent taken from the tank, at normal flow
    public float breakChance = 0.01f;          // chance per second of breaking while water flows

    // Gravity feed: the higher the tank water sits above the valve, the more pressure and the more flow.
    public float normalHead = 15f;             // metres of head that give normal flow
    public float tankHeight = 7f;              // the water surface drops as the tank empties
    public float pipeFactor = 1f;              // narrower or longer pipe: below 1

    public Renderer indicator;                 // the model that changes colour
    public Color openColor = Color.green;
    public Color closedColor = Color.red;
    public Color brokenColor = new Color(1f, 0.5f, 0f);

    public bool isOpen;
    public bool isBroken;

    void Update()
    {
        if (!isBroken)
        {
            if (sensor.moisture < openBelow) isOpen = true;
            else if (sensor.moisture > closeAbove) isOpen = false;
        }

        if (indicator != null) indicator.material.color = isBroken ? brokenColor : isOpen ? openColor : closedColor;

        // Nothing flows if it is closed, broken, or the tank is empty.
        if (!isOpen || isBroken || tank.level <= 0f) return;

        float flow = Flow() * Time.deltaTime;
        sensor.AddWater(waterPerSecond * flow);
        tank.Drain(tankCostPerSecond * flow);

        if (Random.value < breakChance * Time.deltaTime)
        {
            isBroken = true;
            isOpen = false;
        }
    }

    public void Repair()
    {
        isBroken = false;
    }

    // 1 at normal head. Valves on the lower terraces get more, and everything slows down as the tank empties.
    float Flow()
    {
        float surface = tank.transform.position.y + tankHeight * tank.level / 100f;
        float head = Mathf.Max(0f, surface - transform.position.y);
        return head / normalHead * pipeFactor;
    }
}
