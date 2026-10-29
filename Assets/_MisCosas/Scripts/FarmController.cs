using System.Collections.Generic;
using UnityEngine;

// Holds the whole farm in one place, works out what needs doing and gives each job to the best free worker.
public class FarmController : MonoBehaviour
{
    public SoilSensor[] sensors;
    public Valve[] valves;
    public WaterTank tank;

    public float alertBelow = 15f;                         // a plot this dry needs a worker
    public float distanceWeight = 100f;                    // every this many metres halve a worker's score

    public List<SoilSensor> dryPlots = new List<SoilSensor>();
    public bool tankAlert;
    public int hungryAnimals;

    public Animal[] animals;
    public List<Technician> workers = new List<Technician>();
    public List<Job> jobs = new List<Job>();

    void Start()
    {
        animals = FindObjectsByType<Animal>(FindObjectsSortMode.None);
    }

    void Update()
    {
        dryPlots.Clear();
        foreach (var s in sensors)
            if (s.moisture < alertBelow) dryPlots.Add(s);

        tankAlert = tank.Low;

        hungryAnimals = 0;
        foreach (var a in animals)
            if (a.IsHungry) hungryAnimals++;

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
        // Nobody took it and it is not needed any more (a valve or a bowl of food got there first).
        jobs.RemoveAll(j => j.worker == null && !StillNeeded(j));

        if (tankAlert && !jobs.Exists(j => j.type == JobType.Tank))
            jobs.Add(new Job { type = JobType.Tank, position = tank.transform.position });

        foreach (var animal in animals)
            if (animal.IsHungry && !jobs.Exists(j => j.animal == animal))
                jobs.Add(new Job { type = JobType.Feed, animal = animal, position = animal.transform.position });

        foreach (var plot in dryPlots)
            if (!jobs.Exists(j => j.plot == plot))
                jobs.Add(new Job { type = JobType.Plot, plot = plot, position = plot.transform.position });
    }

    bool StillNeeded(Job job)
    {
        switch (job.type)
        {
            case JobType.Tank: return tankAlert;
            case JobType.Feed: return job.animal.IsHungry;
            default: return dryPlots.Contains(job.plot);
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

    // The tank first, then the hungriest animal, then the driest plot.
    float Priority(Job job)
    {
        switch (job.type)
        {
            case JobType.Tank: return -10f;
            case JobType.Feed: return -5f - job.animal.hunger / 100f;
            default: return job.plot.moisture;
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
