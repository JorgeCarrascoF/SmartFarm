using UnityEngine;

public enum JobType { Plot, Tank }

// One thing that needs doing on the farm. The FarmController hands it to a single worker.
[System.Serializable]
public class Job
{
    public JobType type;
    public SoilSensor plot;            // only for plot jobs
    public Vector3 position;
    public Technician worker;          // null while nobody has it
    public bool unreachable;           // no NavMesh near it, so nobody can go
}
