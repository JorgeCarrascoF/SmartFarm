using System.Collections.Generic;
using UnityEngine;

// The terraces column of the status board: one PlotRow for every plot of the farm.
public class ColumnTerraces : MonoBehaviour
{
    public PlotRow rowPrefab;

    StatusBoard board;
    List<PlotRow> rows = new List<PlotRow>();

    public void Setup(StatusBoard board)
    {
        this.board = board;

        foreach (Transform child in transform) Destroy(child.gameObject);      // rows left from the editor
        foreach (var plot in board.farm.sensors) rows.Add(Instantiate(rowPrefab, transform));
    }

    public void Refresh()
    {
        var farm = board.farm;
        for (int i = 0; i < rows.Count; i++)
        {
            float moisture = farm.sensors[i].moisture;
            var valve = farm.valves[i];

            Color irrigation = moisture < farm.alertBelow ? board.badColor : moisture < board.lowBelow ? board.lowColor : board.okColor;
            Color valveColor = valve.isBroken ? board.badColor : valve.isOpen ? board.okColor : board.closedColor;

            rows[i].Show($"Terrace {i + 1}", irrigation, valveColor);
        }
    }
}
