using UnityEngine;
using UnityEngine.AI;

// Spawns one avatar for every row of the database, in a ring around this object, and gives each its stats.
public class AvatarSpawner : MonoBehaviour
{
    public AvatarDatabase database;
    public FarmController farm;
    public float ringRadius = 4f;

    void Start()
    {
        int count = database.records.Count;
        for (int i = 0; i < count; i++)
        {
            var record = database.records[i];
            if (record.prefab == null)
            {
                Debug.LogWarning($"AvatarSpawner: {record.avatarName} has no prefab", database);
                continue;
            }

            float angle = i * Mathf.PI * 2f / count;
            Vector3 position = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 5f, NavMesh.AllAreas)) position = hit.position;

            var avatar = Instantiate(record.prefab, position, transform.rotation);
            avatar.name = record.avatarName;

            var worker = avatar.GetComponent<Technician>();
            worker.farm = farm;
            worker.Apply(record);
        }
    }
}
