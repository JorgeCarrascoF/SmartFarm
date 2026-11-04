using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One animal on the status board: its name and how hungry it is.
public class AnimalRow : MonoBehaviour
{
    public TMP_Text label;
    public Image hunger;

    public void Show(string text, Color hungerColor)
    {
        label.text = text;
        hunger.color = hungerColor;
    }
}
