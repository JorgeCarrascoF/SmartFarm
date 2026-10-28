using UnityEngine;

// Plain on-screen readout of the farm. Sensor i is shown next to valve i.
public class Dashboard : MonoBehaviour
{
    public FarmController farm;

    void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 240, 40 + farm.sensors.Length * 22 + 50), "Smart Farm");

        float y = 36;
        for (int i = 0; i < farm.sensors.Length; i++)
        {
            float moisture = farm.sensors[i].moisture;
            string valve = farm.valves[i].isOpen ? "open" : "closed";
            string alert = moisture < farm.alertBelow ? "  DRY" : "";
            GUI.Label(new Rect(20, y, 220, 22), $"Plot {i + 1}: {moisture:0} %   valve {valve}{alert}");
            y += 22;
        }

        y += 8;
        GUI.Label(new Rect(20, y, 220, 22), $"Tank: {farm.tank.level:0} %" + (farm.tankAlert ? "  LOW" : ""));
        DrawBar(new Rect(20, y + 22, 200, 10), farm.tank.level / 100f);
    }

    static void DrawBar(Rect area, float fill)
    {
        GUI.color = Color.gray;
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.cyan;
        GUI.DrawTexture(new Rect(area.x, area.y, area.width * fill, area.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
