using System.Collections.Generic;
using UnityEngine;

// The animals column of the status board: one AnimalRow for every animal of the farm.
public class ColumnAnimals : MonoBehaviour
{
    public AnimalRow rowPrefab;

    StatusBoard board;
    List<AnimalRow> rows = new List<AnimalRow>();

    public void Setup(StatusBoard board)
    {
        this.board = board;

        foreach (Transform child in transform) Destroy(child.gameObject);
        foreach (var animal in board.farm.animals) rows.Add(Instantiate(rowPrefab, transform));
    }

    public void Refresh()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var animal = board.farm.animals[i];
            Color color = animal.IsHungry ? board.badColor : animal.hunger > board.hungerWarning ? board.lowColor : board.okColor;
            rows[i].Show(animal.DisplayName, color);
        }
    }
}
