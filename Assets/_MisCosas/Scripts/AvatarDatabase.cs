using System.Collections.Generic;
using UnityEngine;

// One row per person on the farm. The stats are rolled once with "Generate random records" and then saved,
// so the same people turn up every time the game starts.
[CreateAssetMenu(menuName = "Smart Farm/Avatar Database")]
public class AvatarDatabase : ScriptableObject
{
    [System.Serializable]
    public class Record
    {
        public string avatarName;
        public GameObject prefab;                   // the model that is spawned for this person
        [Range(1, 10)] public int speed = 5;
        [Range(1, 10)] public int strength = 5;
        [Range(1, 10)] public int irrigation = 5;
        [Range(1, 10)] public int animalHandling = 5;

        // 5 is a normal worker (1.0). 1 is 0.6 times as good, 10 is 1.5 times.
        static float Factor(int stat) { return Mathf.Lerp(0.6f, 1.5f, (stat - 1) / 9f); }

        public float SpeedFactor => Factor(speed);
        public float StrengthFactor => Factor(strength);
        public float IrrigationFactor => Factor(irrigation);
        public float AnimalFactor => Factor(animalHandling);
    }

    public List<Record> records = new List<Record>();

    [Header("Used by Generate random records")]
    public int people = 6;
    public int minStat = 2;
    public int maxStat = 9;
    public GameObject[] prefabs;
    public string[] names =
    {
        "Marta", "Luis", "Elena", "Pablo", "Carmen", "Diego", "Lucia", "Andres",
        "Rosa", "Hugo", "Irene", "Sergio", "Paula", "Mateo", "Nuria", "Oscar"
    };

    [ContextMenu("Generate random records")]
    void Generate()
    {
        records.Clear();
        var free = new List<string>(names);

        for (int i = 0; i < people; i++)
        {
            string chosen = "Worker " + (i + 1);
            if (free.Count > 0)
            {
                int pick = Random.Range(0, free.Count);
                chosen = free[pick];
                free.RemoveAt(pick);
            }

            records.Add(new Record
            {
                avatarName = chosen,
                prefab = prefabs.Length > 0 ? prefabs[Random.Range(0, prefabs.Length)] : null,
                speed = Random.Range(minStat, maxStat + 1),
                strength = Random.Range(minStat, maxStat + 1),
                irrigation = Random.Range(minStat, maxStat + 1),
                animalHandling = Random.Range(minStat, maxStat + 1)
            });
        }

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }
}
