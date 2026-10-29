using UnityEngine;
using UnityEngine.AI;

// Waits at his spawn point until the FarmController gives him a job. He goes there, does it, and walks home.
// If he gets a new job on the way home he takes it.
[RequireComponent(typeof(NavMeshAgent))]
public class Technician : MonoBehaviour
{
    enum State { Waiting, Going, Working, Returning }

    public FarmController farm;
    public float workTime = 4f;        // seconds spent at a plot or at the tank
    public float waterGiven = 30f;     // moisture added to a plot
    public float tankRefill = 60f;     // percent added to the tank
    public float walkSpeed = 3f;       // when there is nothing to do
    public float runSpeed = 8f;        // when he is on his way to a job

    public AvatarDatabase.Record stats;    // null for a worker placed by hand

    NavMeshAgent agent;
    State state = State.Waiting;
    Job job;
    Vector3 home;
    float timer;

    public bool IsFree => state == State.Waiting || state == State.Returning;
    public float StrengthFactor => stats != null ? stats.StrengthFactor : 1f;
    public float IrrigationFactor => stats != null ? stats.IrrigationFactor : 1f;
    public float AnimalFactor => stats != null ? stats.AnimalFactor : 1f;

    // Text for the dashboard.
    public string StatsText => stats == null ? "" : $"Spd {stats.speed}  Str {stats.strength}  Irr {stats.irrigation}  Ani {stats.animalHandling}";

    public string Activity
    {
        get
        {
            if (job == null) return state == State.Returning ? "going home" : "idle";
            string place = job.type == JobType.Tank ? "the tank" : job.type == JobType.Feed ? job.animal.name : job.plot.name;
            return (state == State.Working ? "working at " : "going to ") + place;
        }
    }

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        home = transform.position;
        agent.speed = walkSpeed;
    }

    // The stats of this person scale how fast he moves and how much each job gets done.
    public void Apply(AvatarDatabase.Record record)
    {
        stats = record;
        walkSpeed *= record.SpeedFactor;
        runSpeed *= record.SpeedFactor;
        tankRefill *= record.StrengthFactor;
        waterGiven *= record.IrrigationFactor;
        workTime /= (record.StrengthFactor + record.IrrigationFactor) / 2f;
        agent.speed = walkSpeed;

        // The Animator controller expects a run at 4 and a walk at 1.5, so slower people play the clips slower.
        var animation = GetComponent<CharacterAnimation>();
        if (animation != null) animation.speedScale = runSpeed / 4f;
    }

    void Start()
    {
        if (!agent.isOnNavMesh) Debug.LogWarning("Technician: not standing on the NavMesh, move him onto the blue area", this);
        farm.Register(this);
    }

    void Update()
    {
        switch (state)
        {
            case State.Going:
                if (Arrived())
                {
                    timer = job.type == JobType.Feed ? workTime / AnimalFactor : workTime;      // good handlers calm them quickly
                    state = State.Working;
                }
                break;

            case State.Working:
                timer -= Time.deltaTime;
                if (timer <= 0f) FinishJob();
                break;

            case State.Returning:
                if (Arrived()) state = State.Waiting;
                break;
        }
    }

    // Called by the FarmController. False if the job is not on the NavMesh.
    public bool Assign(Job newJob)
    {
        float reach = newJob.type == JobType.Tank ? 30f : 15f;
        if (!NavMesh.SamplePosition(newJob.position, out NavMeshHit hit, reach, NavMesh.AllAreas)) return false;

        job = newJob;
        agent.speed = runSpeed;
        agent.SetDestination(hit.position);
        state = State.Going;
        return true;
    }

    void FinishJob()
    {
        if (job.type == JobType.Tank) farm.tank.Fill(tankRefill);
        else if (job.type == JobType.Feed) job.animal.Feed();
        else job.plot.AddWater(waterGiven);

        farm.Complete(job);
        job = null;

        agent.speed = walkSpeed;
        agent.SetDestination(home);
        state = State.Returning;
    }

    void OnDestroy()
    {
        if (job != null && farm != null) farm.Release(job);
    }

    bool Arrived()
    {
        return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f;
    }
}
