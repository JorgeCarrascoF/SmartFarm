using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Waits at his spawn point. A low tank comes first, then the driest plots, nearest first.
// Each job is claimed by one technician at a time. When he runs out of work he walks home.
[RequireComponent(typeof(NavMeshAgent))]
public class Technician : MonoBehaviour
{
    enum State { Waiting, Going, Working, Returning }
    enum Job { None, Plot, Tank }

    public FarmController farm;
    public float workTime = 4f;        // seconds spent at a plot or at the tank
    public float waterGiven = 30f;     // moisture added to a plot, 0 to just visit
    public float tankRefill = 60f;     // percent added to the tank
    public float walkSpeed = 3f;       // when there is nothing to do
    public float runSpeed = 8f;        // when he is on his way to a job

    static readonly HashSet<SoilSensor> claimedPlots = new HashSet<SoilSensor>();
    static bool tankClaimed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetClaims()
    {
        claimedPlots.Clear();
        tankClaimed = false;
    }

    NavMeshAgent agent;
    State state = State.Waiting;
    Job job = Job.None;
    SoilSensor plot;
    Object unreachable;                // last thing with no NavMesh nearby, so the warning is printed only once
    Vector3 home;
    float timer;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        home = transform.position;
        agent.speed = walkSpeed;
    }

    // The stats of this person scale how fast he moves and how much each job gets done.
    public void Apply(AvatarDatabase.Record record)
    {
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
    }

    void Update()
    {
        switch (state)
        {
            case State.Waiting:
                LookForWork();
                break;

            case State.Going:
                if (Arrived())
                {
                    timer = workTime;
                    state = State.Working;
                }
                break;

            case State.Working:
                timer -= Time.deltaTime;
                if (timer <= 0f) FinishJob();
                break;

            case State.Returning:
                // New work can show up while he walks home, so keep looking.
                if (!LookForWork() && Arrived()) state = State.Waiting;
                break;
        }
    }

    // The tank wins over the plots. Returns true if he set off for something.
    bool LookForWork()
    {
        if (farm.tankAlert && !tankClaimed && GoTo(farm.tank.transform.position, 30f, farm.tank))
        {
            tankClaimed = true;
            job = Job.Tank;
            return true;
        }

        SoilSensor next = NearestFreePlot();
        if (next != null && GoTo(next.transform.position, 15f, next))
        {
            claimedPlots.Add(next);
            plot = next;
            job = Job.Plot;
            return true;
        }

        return false;
    }

    void FinishJob()
    {
        if (job == Job.Tank)
        {
            farm.tank.Fill(tankRefill);
            tankClaimed = false;
        }
        else if (job == Job.Plot)
        {
            plot.AddWater(waterGiven);
            claimedPlots.Remove(plot);
        }
        job = Job.None;

        if (LookForWork()) return;

        agent.speed = walkSpeed;
        agent.SetDestination(home);
        state = State.Returning;
    }

    bool GoTo(Vector3 position, float reach, Object context)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, reach, NavMesh.AllAreas))
        {
            agent.speed = runSpeed;
            agent.SetDestination(hit.position);
            state = State.Going;
            return true;
        }

        if (context != unreachable)
        {
            unreachable = context;
            Debug.LogWarning($"Technician: no NavMesh within {reach} m of {context.name}", context);
        }
        return false;
    }

    // The closest dry plot that no other technician has taken, or null if there is none.
    SoilSensor NearestFreePlot()
    {
        SoilSensor best = null;
        float bestDistance = float.MaxValue;
        foreach (var candidate in farm.dryPlots)
        {
            if (claimedPlots.Contains(candidate)) continue;
            float distance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        return best;
    }

    void OnDestroy()
    {
        if (job == Job.Tank) tankClaimed = false;
        else if (job == Job.Plot) claimedPlots.Remove(plot);
    }

    bool Arrived()
    {
        return !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.2f;
    }
}
