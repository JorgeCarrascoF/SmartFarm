using System.Collections.Generic;
using UnityEngine;

// Holds the whole farm in one place and works out the alerts. The dashboard and the technician read from here.
public class FarmController : MonoBehaviour
{
    public SoilSensor[] sensors;
    public Valve[] valves;
    public WaterTank tank;

    public float alertBelow = 15f;                         // a plot this dry needs the technician

    public List<SoilSensor> dryPlots = new List<SoilSensor>();
    public bool tankAlert;

    void Update()
    {
        dryPlots.Clear();
        foreach (var s in sensors)
            if (s.moisture < alertBelow) dryPlots.Add(s);

        tankAlert = tank.Low;
    }
}
