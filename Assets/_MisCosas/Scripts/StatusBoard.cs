using TMPro;
using UnityEngine;
using UnityEngine.UI;

// A screen standing in the world that shows the state of the farm: the tank and the three columns.
public class StatusBoard : MonoBehaviour
{
    public FarmController farm;
    public DayNightCycle clock;          // optional, shows the time of day
    public TMP_Text title;               // optional

    public TMP_Text tankLabel;
    public Image tankBar;                // an Image of type Filled

    public ColumnTerraces terraces;
    public ColumnAnimals animals;
    public ColumnWorkers workers;

    public float refreshTime = 0.25f;
    public float lowBelow = 40f;         // a plot below this is yellow, below the farm's alert it is red
    public float hungerWarning = 40f;    // an animal above this is yellow, above its own limit it is red

    public Color okColor = new Color(0.3f, 0.85f, 0.4f);
    public Color lowColor = new Color(1f, 0.8f, 0.2f);
    public Color badColor = new Color(1f, 0.3f, 0.3f);
    public Color closedColor = new Color(0.6f, 0.65f, 0.7f);

    bool ready;
    float timer;

    void Update()
    {
        // Done in the first Update and not in Start, so the farm has already found its animals and the workers have registered.
        if (!ready)
        {
            terraces.Setup(this);
            animals.Setup(this);
            workers.Setup(this);
            ready = true;
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = refreshTime;

        if (title != null) title.text = clock != null ? $"SMART FARM   {clock.TimeText}" : "SMART FARM";

        tankLabel.text = $"Tank   {farm.tank.level:0} %";
        tankBar.fillAmount = farm.tank.level / 100f;
        tankBar.color = farm.tankAlert ? badColor : Color.cyan;

        terraces.Refresh();
        animals.Refresh();
        workers.Refresh();
    }
}
