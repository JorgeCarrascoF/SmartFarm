using TMPro;
using UnityEngine;

// One worker on the status board: his name and what he is doing.
public class WorkerRow : MonoBehaviour
{
    public TMP_Text nameLabel;
    public TMP_Text task;

    public void Show(string workerName, string activity)
    {
        nameLabel.text = workerName;
        task.text = activity;
    }
}
