using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One terrace on the status board: a name with the moisture, a drop for the irrigation and a gear for the valve.
public class PlotRow : MonoBehaviour
{
    public TMP_Text label;
    public Image irrigation;
    public Image valve;

    public void Show(string text, Color irrigationColor, Color valveColor)
    {
        label.text = text;
        irrigation.color = irrigationColor;
        valve.color = valveColor;
    }
}
