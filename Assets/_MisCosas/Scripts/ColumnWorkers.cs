using System.Collections.Generic;
using UnityEngine;

// The workers column of the status board: one WorkerRow for every worker of the farm.
public class ColumnWorkers : MonoBehaviour
{
    public WorkerRow rowPrefab;

    StatusBoard board;
    List<WorkerRow> rows = new List<WorkerRow>();

    public void Setup(StatusBoard board)
    {
        this.board = board;

        foreach (Transform child in transform) Destroy(child.gameObject);
        foreach (var worker in board.farm.workers) rows.Add(Instantiate(rowPrefab, transform));
    }

    public void Refresh()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            var worker = board.farm.workers[i];
            rows[i].Show(worker.name, worker.Tag);
        }
    }
}
