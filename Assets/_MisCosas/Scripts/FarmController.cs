using System.Collections.Generic;
using UnityEngine;

// Holds the whole farm in one place, works out what needs doing and gives each job to the best free worker.
public class FarmController : MonoBehaviour
{
    public SoilSensor[] sensors;
    public Valve[] valves;
    public WaterTank tank;

    public float alertBelow = 15f;                         // the dashboard marks a plot this dry
    public float urgentBelow = 5f;                         // a plot this dry gets a worker, whatever the valve is doing
    public float distanceWeight = 100f;                    // every this many metres halve a worker's score

    public bool tankAlert;
    public int hungryAnimals;
    public int brokenValves;

    public Animal[] animals;
    public List<Technician> workers = new List<Technician>();
    public List<Job> jobs = new List<Job>();

    void Start()
    {
        animals = FindObjectsByType<Animal>(FindObjectsSortMode.None);
    }

    void Update()
    {
        tankAlert = tank.Low;

        hungryAnimals = 0;
        foreach (var a in animals)
            if (a.IsHungry) hungryAnimals++;

        brokenValves = 0;
        foreach (var v in valves)
            if (v.isBroken) brokenValves++;

        UpdateJobs();
        AssignJobs();
    }

    public void Register(Technician worker)
    {
        if (!workers.Contains(worker)) workers.Add(worker);
    }

    public void Complete(Job job)
    {
        jobs.Remove(job);
    }

    // A worker that disappears gives his job back.
    public void Release(Job job)
    {
        job.worker = null;
        if (job.animal != null) job.animal.held = false;
    }

    void UpdateJobs()
    {
        // Nobody took it and it is not needed any more (food got there first).
        jobs.RemoveAll(j => j.worker == null && !StillNeeded(j));

        if (tankAlert && !jobs.Exists(j => j.type == JobType.Tank))
            jobs.Add(new Job { type = JobType.Tank, position = tank.transform.position });

        foreach (var animal in animals)
            if (animal.IsHungry && !jobs.Exists(j => j.animal == animal))
                jobs.Add(new Job { type = JobType.Feed, animal = animal, position = animal.transform.position });

        foreach (var plot in sensors)
            if (plot.moisture < urgentBelow && !jobs.Exists(j => j.plot == plot))
                jobs.Add(new Job { type = JobType.Plot, plot = plot, position = plot.transform.position });

        foreach (var valve in valves)
            if (valve.isBroken && !jobs.Exists(j => j.valve == valve))
                jobs.Add(new Job { type = JobType.Repair, valve = valve, position = valve.transform.position });
    }

    bool StillNeeded(Job job)
    {
        switch (job.type)
        {
            case JobType.Tank: return tankAlert;
            case JobType.Feed: return job.animal.IsHungry;
            case JobType.Plot: return job.plot.moisture < urgentBelow;
            default: return job.valve.isBroken;
        }
    }

    void AssignJobs()
    {
        jobs.Sort((a, b) => Priority(a).CompareTo(Priority(b)));

        foreach (var job in jobs)
        {
            if (job.worker != null || job.unreachable) continue;

            // Animals wander, so look for the place where it is now.
            if (job.type == JobType.Feed) job.position = job.animal.transform.position;

            Technician best = null;
            float bestScore = 0f;
            foreach (var worker in workers)
            {
                if (!worker.IsFree) continue;
                float score = Score(worker, job);
                if (score > bestScore)
                {
                    best = worker;
                    bestScore = score;
                }
            }
            if (best == null) continue;                    // nobody is free, try again next frame

            if (best.Assign(job))
            {
                job.worker = best;
                if (job.type == JobType.Feed) job.animal.held = true;       // it waits for him
            }
            else
            {
                job.unreachable = true;
                Debug.LogWarning($"FarmController: no NavMesh near the {job.type} job at {job.position}", this);
            }
        }
    }

    // The tank first, then a plot about to die, then the hungriest animal, then the broken valve whose plot is driest.
    float Priority(Job job)
    {
        switch (job.type)
        {
            case JobType.Tank: return -10f;
            case JobType.Plot: return -8f + job.plot.moisture / 100f;
            case JobType.Feed: return -5f - job.animal.hunger / 100f;
            default: return job.valve.sensor.moisture;
        }
    }

    // Each job asks for a different stat, and a long walk counts against the worker.
    float Score(Technician worker, Job job)
    {
        float stat;
        switch (job.type)
        {
            case JobType.Tank: stat = worker.StrengthFactor; break;
            case JobType.Feed: stat = worker.AnimalFactor; break;
            default: stat = worker.IrrigationFactor; break;
        }

        float distance = Vector3.Distance(worker.transform.position, job.position);
        return stat / (1f + distance / distanceWeight);
    }
}
