# SmartBin Storage Intelligence

This document describes how SmartBin's adaptive storage intelligence identifies storage constraints, scores and prioritizes deleted files, plans optimization batches, and executes them with safety safeguards.

## 1. Storage Pressure Monitoring

SmartBin continuously monitors the user's hard drive space to determine system health.
- **Drive Scanning**: Leverages `DriveInfo` system APIs to calculate the total capacity, used space, and free space percentage of the storage volume.
- **Configurable States**:
  - `Normal`: Free-space percentage is above the configured Low threshold (default 15%). No automatic optimization recommended.
  - `Low`: Free-space percentage is below the Low threshold but above Critical (default 15% to 5%). Recommend and evaluate candidates.
  - `Critical`: Free-space percentage is below the Critical threshold (default 5%). Prioritize aggressive compression to reclaim space and prevent system errors.
- **Heuristics & Simulators**: Includes a fully interactive programmatic simulator that allows developers/users to toggle simulated storage constraints (`Normal`, `Low`, `Critical`) for verification without modifying the physical disk.

## 2. Recommendation Policy

The recommendation engine converts pressure metrics into explainable action plans:
```
Metrics & Pressure State ──> [ StoragePressurePolicy ] ──> Recommendation & Required Bytes
```

- Normal State: Returns `IsOptimizationRecommended = false` and required space = 0.
- Low State: Returns `IsOptimizationRecommended = true` with a target to restore free space back to the safety percentage (default 20%).
- Critical State: Returns `IsOptimizationRecommended = true` with an urgent rationale, indicating that compression should run immediately.

## 3. Candidate Scoring & Prioritization

To prevent wasting CPU and disk resources, SmartBin ranks deleted files using an explainable, deterministic scoring model:

$$\text{Priority Score} = \text{Size Factor} + \text{Age Factor} + \text{Compression Benefit Factor} + \text{Optimization Status Factor}$$

Each factor is normalized on a scale of 0 to 100, yielding a maximum score of 400.

### Scoring Factors:
1. **Size Factor (Max 100)**:
   - File size $\ge 100$ MB: 100 points
   - 10 MB to 100 MB: 85 points
   - 1 MB to 10 MB: 60 points
   - 10 KB to 1 MB: 30 points
   - $< 10$ KB: 10 points
2. **Age Factor (Max 100)**:
   - Deleted $\ge 30$ days ago: 100 points
   - 7 to 30 days: 60 points
   - 1 to 7 days: 30 points
   - $< 1$ day: 10 points
3. **Compression Benefit Factor (Max 100)**:
   - Estimated compression savings $\ge 50\%$: 100 points
   - 20% to 50%: 60 points
   - 5% to 20%: 35 points
   - Incompressible (or pre-compressed extension): 0 points
4. **Optimization Status Factor (Max 100)**:
   - Uncompressed: 100 points
   - Not Feasible: 10 points
   - Already Compressed: 0 points (cannot be optimized further)

### Explainability
Explainability is a core pillar of SmartBin. Users deserve to know exactly *why* a file is being modified. The `CandidateAnalyzer` records an explainable reasoning string (e.g. "Large file, deleted 42 days ago, high expected savings") displayed on the dashboard for every recommended file.

## 4. Optimization Planner

The planner takes a list of analyzed candidates, available free space, and target free space.
- Calculates `Required Reclaimed Bytes = Target Free Space - Available Free Space`.
- Filters candidates to only those eligible for optimization (`IsEligibleForOptimization == true`).
- Sorts candidates descending by `PriorityScore`.
- Iterates and adds items to the plan until expected savings reach or exceed the required target bytes.
- Separates **planning** from **execution** completely, preventing stale metadata.

## 5. Optimization Executor

The orchestrator executes the generated plan sequentially with active safeguards:
1. **Active Rechecks**: Before compressing each candidate, it re-queries the `StoragePressureMonitor`. If the available space has already met the target (e.g., because the user manually freed up space, or previous files reclaimed more space than estimated), the executor **STOPS** immediately, avoiding unnecessary CPU cycles.
2. **Revalidation**: Checks if the item is still present in the repository and still uncompressed.
3. **Atomic Run**: Compresses the file using stream-based ZIP compression, validates integrity, updates metadata in the SQLite DB, and deletes the uncompressed original only after the DB transaction succeeds.
